using CK.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Observable
{
    /// <summary>
    /// Base class for any observable domain where <see cref="AllRoots"/> are not strongly typed.
    /// You may use specialized <see cref="ObservableChannel{T}"/> or <see cref="ObservableDomain{T1, T2, T3, T4}"/>
    /// for strongly typed roots.
    /// </summary>
    public partial class ObservableDomain
    {
        /// <summary>
        /// An artificial <see cref="CKExceptionData"/> that is added to
        /// <see cref="IObservableTransaction.Errors"/> whenever a transaction
        /// has not been committed.
        /// </summary>
        public static readonly CKExceptionData UncomittedTransaction = new CKExceptionData( "Uncommitted transaction.", "Not.An.Exception", "Not.An.Exception, No.Assembly", null, null, null, null, null, null );

        static readonly Type[] _observableRootCtorParameters = new Type[] { typeof( ObservableDomain ) };
        static readonly Action _actionVoid = () => { };

        class PropInfo
        {
            public readonly PropertyChangedEventArgs EventArg;
            public int PropertyId { get; }
            public string Name => EventArg.PropertyName;

            public PropInfo( int propertyId, string name )
            {
                EventArg = new PropertyChangedEventArgs( name );
                PropertyId = propertyId;
            }

            public long GetObjectPropertyId( ObservableObject o )
            {
                Debug.Assert( o.OId >= 0 );
                long r = o.OId;
                return (r << 24) | (uint)PropertyId;
            }
        }

        [ThreadStatic]
        internal static ObservableDomain CurrentThreadDomain;

        internal readonly IExporterResolver _exporters;
        readonly ISerializerResolver _serializers;
        readonly IDeserializerResolver _deserializers;
        readonly TimerHost _timerHost;

        /// <summary>
        /// Maps property names to PropInfo that contains the property index.
        /// </summary>
        readonly Dictionary<string, PropInfo> _properties;
        /// <summary>
        /// Map property index to PropInfo that contains the property name.
        /// </summary>
        readonly List<PropInfo> _propertiesByIndex;

        readonly ChangeTracker _changeTracker;
        readonly AllCollection _exposedObjects;
        readonly ReaderWriterLockSlim _lock;
        Stack<int> _freeList;

        ObservableObject[] _objects;

        /// <summary>
        /// Since we manage the array directly, this is
        /// the equivalent of a List{ObservableObject}.Count (and _objects.Length
        /// is the capacity):
        /// the null cells (the ones registered in _freeList) are included.
        /// </summary>
        int _objectsListCount;

        /// <summary>
        /// This is the actual number of objects, null cells of _objects are NOT included.
        /// </summary>
        int _actualObjectCount;

        /// <summary>
        /// The root objects among all _objects.
        /// </summary>
        List<ObservableRootObject> _roots;

        IObservableTransaction _currentTran;
        int _transactionSerialNumber;
        IActivityMonitor _domainBoundMonitor;
        IActivityMonitor _lockedDomainMonitor;

        bool _deserializing;

        /// <summary>
        /// Exposes the non null objects in _objects as a collection.
        /// </summary>
        class AllCollection : IReadOnlyCollection<ObservableObject>
        {
            readonly ObservableDomain _d;

            public AllCollection( ObservableDomain d )
            {
                _d = d;
            }

            public int Count => _d._actualObjectCount;

            public IEnumerator<ObservableObject> GetEnumerator() => _d._objects.Take( _d._objectsListCount )
                                                                               .Where( o => o != null )
                                                                               .GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        /// <summary>
        /// The change tracker handles the transfomation of actual changes into events that are
        /// optimized and serialized by the <see cref="Commit(ObservableDomain, Func{string, PropInfo}, DateTime, DateTime)"/> method.
        /// </summary>
        class ChangeTracker
        {
            class PropChanged
            {
                public readonly ObservableObject Object;
                public readonly PropInfo Info;
                public readonly object InitialValue;
                public object FinalValue;

                public long Key => Info.GetObjectPropertyId( Object );

                public PropChanged( ObservableObject o, PropInfo p, object initial, object final )
                {
                    Object = o;
                    Info = p;
                    InitialValue = initial;
                    FinalValue = final;
                }
            }

            readonly List<ObservableEvent> _changeEvents;
            readonly Dictionary<ObservableObject, List<PropertyInfo>> _newObjects;
            readonly Dictionary<long, PropChanged> _propChanged;
            readonly List<ObservableCommand> _commands;

            public ChangeTracker()
            {
                _changeEvents = new List<ObservableEvent>();
                _newObjects = new Dictionary<ObservableObject, List<PropertyInfo>>( PureObjectRefEqualityComparer<ObservableObject>.Default );
                _propChanged = new Dictionary<long, PropChanged>();
                _commands = new List<ObservableCommand>();
            }

            public SuccessfulTransactionContext Commit( ObservableDomain domain, Func<string, PropInfo> ensurePropertInfo, DateTime startTime, DateTime nextTimerDueDate )
            {
                _changeEvents.RemoveAll( e => e is ICollectionEvent c && c.Object.IsDisposed );
                foreach( var p in _propChanged.Values )
                {
                    if( !p.Object.IsDisposed )
                    {
                        _changeEvents.Add( new PropertyChangedEvent( p.Object, p.Info.PropertyId, p.Info.Name, p.FinalValue ) );
                        if( _newObjects.TryGetValue( p.Object, out var exportables ) )
                        {
                            Debug.Assert( exportables != null, "If the object is not exportable, there must be no property changed events." );
                            int idx = exportables.IndexOf( exp => exp.Name == p.Info.Name );
                            if( idx >= 0 ) exportables.RemoveAt( idx );
                        }
                    }
                }
                foreach( var kv in _newObjects )
                {
                    if( kv.Value == null || kv.Value.Count == 0 ) continue;
                    foreach( var exp in kv.Value )
                    {
                        object propValue = exp.GetValue( kv.Key );
                        var pInfo = ensurePropertInfo( exp.Name );
                        _changeEvents.Add( new PropertyChangedEvent( kv.Key, pInfo.PropertyId, pInfo.Name, propValue ) );
                    }
                }
                var result = new SuccessfulTransactionContext( domain, _changeEvents.ToArray(), _commands.ToArray(), startTime, nextTimerDueDate );
                Reset();
                return result;
            }

            /// <summary>
            /// Clears all events collected so far from the 3 internal lists.
            /// </summary>
            public void Reset()
            {
                _changeEvents.Clear();
                _newObjects.Clear();
                _propChanged.Clear();
                _commands.Clear();
            }

            /// <summary>
            /// Gets whether the object has been created in the current transaction:
            /// it belongs to the _newObjects dictionary.
            /// </summary>
            /// <param name="o">The potential new object.</param>
            /// <returns>True if this is a new object. False if the object has been created earlier.</returns>
            internal bool IsNewObject( ObservableObject o ) => _newObjects.ContainsKey( o );

            internal void OnNewObject( ObservableObject o, int objectId, IObjectExportTypeDriver exporter )
            {
                _changeEvents.Add( new NewObjectEvent( o, objectId ) );
                if( exporter != null )
                {
                    _newObjects.Add( o, exporter.ExportableProperties.ToList() );
                }
                else _newObjects.Add( o, null );
            }

            internal void OnDisposeObject( ObservableObject o )
            {
                if( IsNewObject( o ) )
                {
                    int idx = _changeEvents.IndexOf( e => e is NewObjectEvent n ? n.Object == o : false );
                    _changeEvents.RemoveAt( idx );
                    _newObjects.Remove( o );
                }
                else
                {
                    _changeEvents.Add( new DisposedObjectEvent( o ) );
                }
            }

            internal void OnNewProperty( PropInfo info )
            {
                _changeEvents.Add( new NewPropertyEvent( info.PropertyId, info.Name ) );
            }

            internal void OnPropertyChanged( ObservableObject o, PropInfo p, object before, object after )
            {
                PropChanged c;
                if( _propChanged.TryGetValue( p.GetObjectPropertyId( o ), out c ) )
                {
                    c.FinalValue = after;
                }
                else
                {
                    c = new PropChanged( o, p, before, after );
                    _propChanged.Add( c.Key, c );
                }
            }

            internal ListRemoveAtEvent OnListRemoveAt( ObservableObject o, int index )
            {
                var e = new ListRemoveAtEvent( o, index );
                _changeEvents.Add( e );
                return e;
            }

            internal ListSetAtEvent OnListSetAt( ObservableObject o, int index, object value )
            {
                var e = new ListSetAtEvent( o, index, value );
                _changeEvents.Add( e );
                return e;
            }

            internal CollectionClearEvent OnCollectionClear( ObservableObject o )
            {
                var e = new CollectionClearEvent( o );
                _changeEvents.Add( e );
                return e;
            }

            internal ListInsertEvent OnListInsert( ObservableObject o, int index, object item )
            {
                var e = new ListInsertEvent( o, index, item );
                _changeEvents.Add( e );
                return e;
            }

            internal CollectionMapSetEvent OnCollectionMapSet( ObservableObject o, object key, object value )
            {
                var e = new CollectionMapSetEvent( o, key, value );
                _changeEvents.Add( e );
                return e;
            }

            internal CollectionRemoveKeyEvent OnCollectionRemoveKey( ObservableObject o, object key )
            {
                var e = new CollectionRemoveKeyEvent( o, key );
                _changeEvents.Add( e );
                return e;
            }

            internal void OnSendCommand( in ObservableCommand command )
            {
                _commands.Add( command );
            }
        }

        /// <summary>
        /// Implements <see cref="IObservableTransaction"/>.
        /// </summary>
        class Transaction : IObservableTransaction
        {
            readonly ObservableDomain _previous;
            readonly ObservableDomain _domain;
            readonly IActivityMonitor _previousMonitor;
            readonly DateTime _startTime;
            CKExceptionData[] _errors;
            TransactionResult _result;
            bool _resultInitialized;

            public Transaction( ObservableDomain d, IActivityMonitor previousMonitor, DateTime startTime )
            {
                _domain = d;
                _previousMonitor = previousMonitor;
                _previous = CurrentThreadDomain;
                CurrentThreadDomain = d;
                _startTime = startTime;
                _errors = Array.Empty<CKExceptionData>();
            }

            public DateTime StartTime => _startTime;

            public IReadOnlyList<CKExceptionData> Errors => _errors;

            public void AddError( CKExceptionData d )
            {
                Debug.Assert( d != null );
                Array.Resize( ref _errors, _errors.Length + 1 );
                _errors[_errors.Length - 1] = d;
            }

            public TransactionResult Commit()
            {
                // If result has already been initialized, we exit immediately.
                if( _resultInitialized ) return _result;

                _resultInitialized = true;
                Debug.Assert( _domain._currentTran == this );
                Debug.Assert( _domain._lock.IsWriteLockHeld );
                DateTime nextTimerDueDate = _domain._timerHost.ApplyChanges();
                CurrentThreadDomain = _previous;
                _domain._currentTran = null;
                if( _errors.Length != 0 )
                {
                    // On errors, resets the change tracker, sends the errors to the managers
                    // and creates an error TransactionResult. 
                    _result = new TransactionResult( _errors, _startTime, nextTimerDueDate );
                    _domain._changeTracker.Reset();
                    try
                    {
                        _domain.DomainClient?.OnTransactionFailure( _domain, _errors );
                    }
                    catch( Exception ex )
                    {
                        _domain.Monitor.Error( "Error in IObservableTransactionManager.OnTransactionFailure.", ex );
                        _result = _result.WithClientError( ex );
                    }
                }
                else
                {
                    SuccessfulTransactionContext ctx = _domain._changeTracker.Commit( _domain, _domain.EnsurePropertyInfo, _startTime, nextTimerDueDate );
                    ++_domain._transactionSerialNumber;
                    try
                    {
                        _domain.DomainClient?.OnTransactionCommit( ctx );
                        _result = new TransactionResult( ctx );
                    }
                    catch( Exception ex )
                    {
                        _domain.Monitor.Fatal( "Error in IObservableTransactionManager.OnTransactionCommit.", ex );
                        _result = new TransactionResult( ctx ).WithClientError( ex );
                    }
                }
                var monitor = _domain.Monitor;
                _domain.Monitor = _previousMonitor;
                _domain._lock.ExitWriteLock();
                _domain.PostTransactionHook?.OnTransactionDone( monitor, _domain, _result );
                return _result;
            }

            public void Dispose()
            {
                if( _domain._currentTran == this )
                {
                    AddError( UncomittedTransaction );
                    Commit();
                }
            }
        }

        /// <summary>
        /// Initializes a new <see cref="ObservableDomain"/> with an autonomous <see cref="Monitor"/>
        /// and no <see cref="DomainClient"/>.
        /// </summary>
        /// <param name="domainName">Name of the domain. Must not be null but can be empty.</param>
        public ObservableDomain( string domainName )
            : this( domainName, null, null )
        {
        }

        /// <summary>
        /// Initializes a new <see cref="ObservableDomain"/> bound to a <see cref="Monitor"/> but without
        /// any <see cref="DomainClient"/>.
        /// </summary>
        /// <param name="domainName">Name of the domain. Must not be null but can be empty.</param>
        /// <param name="monitor">
        /// The monitor that will become the <see cref="Monitor"/> of this domain.
        /// Can be null: a new, dedicated, ActivityMonitor will be created.
        /// </param>
        public ObservableDomain( string domainName, IActivityMonitor monitor )
            : this( domainName, null, monitor )
        {
        }

        /// <summary>
        /// Initializes a new <see cref="ObservableDomain"/> with a <see cref="DomainClient"/>.
        /// </summary>
        /// <param name="domainName">Name of the domain. Must not be null but can be empty.</param>
        /// <param name="client">The associated transaction manager to use. Can be null.</param>
        public ObservableDomain( string domainName, IObservableDomainClient client )
            : this( domainName, client, null )
        {
        }

        /// <summary>
        /// Initializes a new <see cref="ObservableDomain"/> with a <see cref="Monitor"/>,
        /// a <see cref="DomainClient"/> an optionals explicit exporter, serializer
        /// and deserializer handlers.
        /// </summary>
        /// <param name="domainName">Name of the domain. Must not be null but can be empty.</param>
        /// <param name="client">The transaction manager to use. Can be null.</param>
        /// <param name="monitor">
        /// The monitor that will become the <see cref="Monitor"/> of this domain.
        /// Can be null: a new, dedicated, ActivityMonitor will be created.
        /// </param>
        /// <param name="exporters">Optional exporters handler.</param>
        /// <param name="serializers">Optional serializers handler.</param>
        /// <param name="deserializers">Optional deserializers handler.</param>
        public ObservableDomain(
            string domainName,
            IObservableDomainClient client,
            IActivityMonitor monitor,
            IExporterResolver exporters = null,
            ISerializerResolver serializers = null,
            IDeserializerResolver deserializers = null )
            : this( domainName, true, client, monitor, exporters, serializers, deserializers )
        {
        }

        ObservableDomain(
            string domainName,
            bool callClientOnCreate,
            IObservableDomainClient client,
            IActivityMonitor monitor,
            IExporterResolver exporters,
            ISerializerResolver serializers,
            IDeserializerResolver deserializers )
        {
            DomainName = domainName ?? throw new ArgumentNullException( nameof( domainName ) );
            Monitor = _domainBoundMonitor = monitor ?? new ActivityMonitor( $"Observable Domain {DomainName}." );
            _exporters = exporters ?? ExporterRegistry.Default;
            _serializers = serializers ?? SerializerRegistry.Default;
            _deserializers = deserializers ?? DeserializerRegistry.Default;
            DomainClient = client;
            _objects = new ObservableObject[512];
            _freeList = new Stack<int>();
            _properties = new Dictionary<string, PropInfo>();
            _propertiesByIndex = new List<PropInfo>();
            _changeTracker = new ChangeTracker();
            _exposedObjects = new AllCollection( this );
            _roots = new List<ObservableRootObject>();
            _timerHost = new TimerHost( this );
            // LockRecursionPolicy.NoRecursion: reentrancy must NOT be allowed.
            _lock = new ReaderWriterLockSlim( LockRecursionPolicy.NoRecursion );
            if( callClientOnCreate ) client?.OnDomainCreated( this, DateTime.UtcNow );
        }

        /// <summary>
        /// Initializes a previously <see cref="Save"/>d domain.
        /// </summary>
        /// <param name="domainName">Name of the domain. Must not be null but can be empty.</param>
        /// <param name="client">The transaction manager to use. Can be null.</param>
        /// <param name="monitor">
        /// The monitor that will become the <see cref="Monitor"/> of this domain.
        /// Can be null: a new, dedicated, ActivityMonitor will be created.
        /// </param>
        /// <param name="s">The input stream.</param>
        /// <param name="leaveOpen">True to leave the stream opened.</param>
        /// <param name="encoding">Optional encoding for characters. Defaults to UTF-8.</param>
        /// <param name="exporters">Optional exporters handler.</param>
        /// <param name="serializers">Optional serializers handler.</param>
        /// <param name="deserializers">Optional deserializers handler.</param>
        public ObservableDomain(
            string domainName,
            IObservableDomainClient client,
            IActivityMonitor monitor,
            Stream s,
            bool leaveOpen = false,
            Encoding encoding = null,
            IExporterResolver exporters = null,
            ISerializerResolver serializers = null,
            IDeserializerResolver deserializers = null )
            : this( domainName, false, client, monitor, exporters, serializers, deserializers )
        {
            Load( s, leaveOpen, encoding );
            client?.OnDomainCreated( this, DateTime.UtcNow );
        }

        /// <summary>
        /// Empty transaction object: must be used during initialization (for <see cref="AddRoot{T}(InitializationTransaction)"/>
        /// to be called).
        /// </summary>
        protected class InitializationTransaction : IObservableTransaction
        {
            readonly ObservableDomain _d;
            readonly ObservableDomain _previous;
            readonly DateTime _startTime;

            /// <summary>
            /// Initializes a new <see cref="InitializationTransaction"/> required
            /// to call <see cref="AddRoot{T}(InitializationTransaction)"/>.
            /// </summary>
            /// <param name="d">The observable domain.</param>
            public InitializationTransaction( ObservableDomain d )
            {
                _startTime = DateTime.UtcNow;
                _d = d;
                d._lock.EnterWriteLock();
                d._currentTran = this;
                _previous = CurrentThreadDomain;
                CurrentThreadDomain = d;
                d._deserializing = true;
            }

            DateTime IObservableTransaction.StartTime => _startTime;

            void IObservableTransaction.AddError( CKExceptionData d ) { }

            TransactionResult IObservableTransaction.Commit() => TransactionResult.Empty;

            IReadOnlyList<CKExceptionData> IObservableTransaction.Errors => Array.Empty<CKExceptionData>();

            /// <summary>
            /// Releases locks and restores intialization context.
            /// </summary>
            public void Dispose()
            {
                _d._deserializing = false;
                CurrentThreadDomain = _previous;
                _d._currentTran = null;
                _d._lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// This must be called only from <see cref="ObservableDomain{T}"/> (and other ObservableDomain generics) constructors.
        /// No event are collected: this is the initial state of the domain.
        /// </summary>
        /// <typeparam name="T">The root type.</typeparam>
        /// <returns>The instance.</returns>
        protected T AddRoot<T>( InitializationTransaction initializationContext ) where T : ObservableRootObject
        {
            var o = (T)typeof( T ).GetConstructor( BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                                                    null,
                                                    _observableRootCtorParameters,
                                                    null ).Invoke( new[] { this } );
            _roots.Add( o );
            return o;
        }

        /// <summary>
        /// Gets all the observable objects that this domain contains (roots included).
        /// These exposed objects are out of any transactions or reentrancy checks: any attempt
        /// to modify one of them will throw.
        /// </summary>
        public IReadOnlyCollection<ObservableObject> AllObjects => _exposedObjects;

        /// <summary>
        /// Gets the root observable objects that this domain contains.
        /// These exposed objects are out of any transactions or reentrancy checks: any attempt
        /// to modify one of them will throw.
        /// </summary>
        public IReadOnlyList<ObservableRootObject> AllRoots => _roots;

        /// <summary>
        /// Gets the current transaction number.
        /// Incremented each time a transaction successfuly ended.
        /// </summary>
        public int TransactionSerialNumber => _transactionSerialNumber;

        /// <summary>
        /// Gets the monitor that is bound to this domain if it can atomically be obtained or null if
        /// it has already been locked by another thread. 
        /// Note that the bound monitor is never null: an autonomous monitor is automatically created if none is
        /// provided to the constructors.
        /// </summary>
        /// <returns>The monitor or null.</returns>
        internal IActivityMonitor LockDomainBoundMonitor()
        {
            if( Interlocked.CompareExchange( ref _lockedDomainMonitor, _domainBoundMonitor, null ) == null )
            {
                return _lockedDomainMonitor;
            }
            return null;
        }

        /// <summary>
        /// Unlocks the domain monitor obtained by <see cref="LockDomainBoundMonitor"/>.
        /// </summary>
        internal void UnlockDomainBoundMonitor()
        {
            if( Interlocked.CompareExchange( ref _lockedDomainMonitor, null, _domainBoundMonitor ) != _domainBoundMonitor )
            {
                throw new Exception( nameof( UnlockDomainBoundMonitor ) );
            }
        }

        /// <summary>
        /// Gets the monitor to use inside a transaction. It is usually an external monitor that has
        /// been provided, but it can be the <see cref="LockDomainBoundMonitor"/> (as long as it has been locked).
        /// </summary>
        internal IActivityMonitor Monitor { get; private set; }

        /// <summary>
        /// Gets this domain name.
        /// </summary>
        public string DomainName { get; }

        /// <summary>
        /// Gets the associated client (head of the Chain of Responsibility).
        /// Can be null.
        /// </summary>
        public IObservableDomainClient DomainClient { get; }

        /// <summary>
        /// Gets os sets the optional <see cref="IPostTransactionHook"/>.
        /// Defaults to <see cref="StandardPostTransactionHook"/> that handles the <see cref="TransactionResult.NextDueTimeUtc"/>
        /// thanks to a <see cref="System.Threading.Timer"/>.
        /// </summary>
        public IPostTransactionHook PostTransactionHook { get; set; }

        /// <summary>
        /// Acquires a read lock: until the returned disposable is disposed
        /// objects can safely be read and any attempt to <see cref="BeginTransaction"/> (from
        /// other threads) wiil  be blocked.
        /// Any attempt to call <see cref="BeginTransaction"/> from this thread will
        /// throw a <see cref="LockRecursionException"/>
        /// </summary>
        /// <param name="millisecondsTimeout">
        /// The maximum number of milliseconds to wait for a read access before giving up.
        /// Wait indefinitely by default.
        /// </param>
        /// <returns>The disposable to release the read lock or null if timeout occurred.</returns>
        public IDisposable AcquireReadLock( int millisecondsTimeout = -1 )
        {
            if( !_lock.TryEnterReadLock( millisecondsTimeout ) ) return null;
            return Util.CreateDisposableAction( () => _lock.ExitReadLock() );
        }

        /// <summary>
        /// Starts a new transaction that must be <see cref="IObservableTransaction.Commit"/>, otherwise
        /// all changes are cancelled.
        /// This must not be called twice (without disposing or committing the existing one) otherwise
        /// an <see cref="InvalidOperationException"/> is thrown.
        /// Any exceptions raised by <see cref="IObservableDomainClient.OnTransactionStart(ObservableDomain, DateTime)"/> are thrown
        /// by this method.
        /// </summary>
        /// <param name="monitor">
        /// Monitor to use. Must not be null.
        /// It will be substituted to this <see cref="Monitor"/> during the transaction execution.
        /// </param>
        /// <param name="millisecondsTimeout">
        /// The maximum number of milliseconds to wait for a read access before giving up.
        /// Wait indefinitely by default.
        /// </param>
        /// <returns>The transaction object or null if the lock has not been taken.</returns>
        /// <remarks>
        /// <para>
        /// Only "Read" or "Write" modes are supported thanks to a <see cref="ReaderWriterLockSlim"/>.
        /// We may support the upgradeable lock with an "ExplicitWrite" mode that would require
        /// to call a method like EnsureWriteMode() before actually modifying the objects (this call will ensure that
        /// the lock is in writer mode or upgrade it).
        /// </para>
        /// <para>
        /// This would enable the calls to Modify() that don't modify anything to not block any reader at all (how
        /// much of these are there?). Even if this can be done, this is definitely dangerous: any domain method that
        /// forgets to call this EnsureWriteMode() before any modification will quickly enter the concurrency hell...
        /// </para>
        /// </remarks>
        public IObservableTransaction BeginTransaction( IActivityMonitor monitor, int millisecondsTimeout = -1 )
        {
            if( monitor == null ) throw new ArgumentNullException( nameof( monitor ) );
            if( !_lock.TryEnterWriteLock( millisecondsTimeout ) )
            {
                monitor.Warn( $"Write lock not obtained in less than {millisecondsTimeout} ms." );
                return null;
            }
            return DoBeginTransaction( monitor, throwExceptions: true ).Item1;
        }

        (IObservableTransaction,Exception) DoBeginTransaction( IActivityMonitor monitor, bool throwExceptions )
        {
            Debug.Assert( monitor != null && _lock.IsWriteLockHeld );
            var prevMonitor = Monitor;
            Monitor = monitor;
            try
            {
                var startTime = DateTime.UtcNow;
                DomainClient?.OnTransactionStart( this, startTime );
                return (_currentTran = new Transaction( this, prevMonitor, startTime ), null);
            }
            catch( Exception ex )
            {
                Monitor.Error( "While calling IObservableTransactionManager.OnTransactionStart().", ex );
                Monitor = prevMonitor;
                _lock.ExitWriteLock();
                if( throwExceptions ) throw;
                return (null, ex);
            }
        }

        /// <summary>
        /// Enables modifications to be done inside a transaction and a try/catch block.
        /// Any exceptions raised by <see cref="IObservableDomainClient.OnTransactionStart(ObservableDomain, DateTime)"/> are thrown
        /// by this method, but any other exceptions are caught, logged, and appears in <see cref="TransactionResult"/>.
        /// </summary>
        /// <param name="monitor">
        /// Monitor to use. Must not be null.
        /// It will be substituted to this <see cref="Monitor"/> during the transaction execution.
        /// </param>
        /// <param name="actions">Any action that can alter the objects of this domain.</param>
        /// <param name="millisecondsTimeout">
        /// The maximum number of milliseconds to wait for a read access before giving up.
        /// Wait indefinitely by default.
        /// </param>
        /// <returns>The transaction result. <see cref="TransactionResult.Empty"/> when the lock has not been taken.</returns>
        public TransactionResult Modify( IActivityMonitor monitor, Action actions, int millisecondsTimeout = -1 )
        {
            using( var t = BeginTransaction( monitor, millisecondsTimeout ) )
            {
                if( t == null ) return TransactionResult.Empty;
                return DoModify( actions, t );
            }
        }

        /// <summary>
        /// Modify the domain once a transaction has been opened and calls the <see cref="IObservableDomainClient"/>
        /// that have been registered: all this occurs in the lock and it is released at the end. This never throws
        /// since the transaction result contains any errors.
        /// </summary>
        /// <param name="actions">The actions to execute. Cannnot be null.</param>
        /// <param name="t">The observable transaction. Cannnot be null.</param>
        /// <returns>The transaction result. Will never be null.</returns>
        TransactionResult DoModify( Action actions, IObservableTransaction t )
        {
            Debug.Assert( actions != null && t != null );
            try
            {
                _timerHost.RaiseElapsedEvent( t.StartTime, throwException: true );
                actions();
                _timerHost.RaiseElapsedEvent( DateTime.UtcNow, throwException: true );
            }
            catch( Exception ex )
            {
                Monitor.Error( ex );
                t.AddError( CKExceptionData.CreateFrom( ex ) );
            }
            // The transaction commit updates the timers to return the NextDueTime.
            return t.Commit();
        }

        /// <summary>
        /// Modifies this ObservableDomain and then executes any pending post-actions.
        /// Any exceptions raised by <see cref="IObservableDomainClient.OnTransactionStart(ObservableDomain, DateTime)"/> (at the start of the process)
        /// and by <see cref="TransactionResult.PostActions"/> (after the successful commit or the failure) are thrown by this method.
        /// </summary>
        /// <param name="monitor">The ActivityMonitor.</param>
        /// <param name="actions">The actions to execute inside the ObservableDomain's modification context.</param>
        /// <param name="millisecondsTimeout">
        /// The maximum number of milliseconds to wait for a read access before giving up.
        /// Wait indefinitely by default.
        /// </param>
        /// <returns>
        /// The transaction result from <see cref="ObservableDomain.Modify"/>. <see cref="TransactionResult.Empty"/> when the
        /// lock has not been taken before <paramref name="millisecondsTimeout"/>.
        /// </returns>
        public async Task<TransactionResult> ModifyAsync( IActivityMonitor monitor, Action actions, int millisecondsTimeout = -1 )
        {
            var tr = Modify( monitor, actions, millisecondsTimeout );
            await tr.ExecutePostActionsAsync( monitor );
            return tr;
        }

        /// <summary>
        /// Entry point for timers. Reentrancies are silently ignored. This should be called based on the last <see cref="TransactionResult.NextDueTimeUtc"/>.
        /// By default, this never throws any exceptions: the exception that may be raised by <see cref="IObservableDomainClient.OnTransactionStart(ObservableDomain, DateTime)"/>
        /// or <see cref="TransactionResult.ExecutePostActionsAsync(IActivityMonitor, bool)"/> is logged in the provided monitor and returned by this method.
        /// </summary>
        /// <param name="monitor">The monitor to use. Must not be null.</param>
        /// <returns>The transaction result (that may be <see cref="TransactionResult.Empty"/>) and the potential exception.</returns>
        public async Task<(TransactionResult, Exception)> FromTimerModifyAsync( IActivityMonitor monitor )
        {
            if( monitor == null ) throw new ArgumentNullException( nameof( monitor ) );
            TransactionResult tr = TransactionResult.Empty;
            if( _lock.TryEnterWriteLock( 0 ) )
            {
                var tEx = DoBeginTransaction( monitor, false );
                if( tEx.Item1 != null )
                {
                    tr = DoModify( _actionVoid, tEx.Item1 );
                    return (tr, await tr.ExecutePostActionsAsync( monitor, false ));
                }
            }
            return (tr, null);
        }

        /// <summary>
        /// Entry point for timers. Reentrancies are silently ignored. This should be called based on the last <see cref="TransactionResult.NextDueTimeUtc"/>.
        /// By default, this never throws any exceptions: all exceptions are logged in the monitor bound to this domain and returned.
        /// This enables timers to avoid the allocation of a new ActivityMonitor: this method exclusively acquires the monitor bound to this domain and the
        /// two <paramref name="before"/> and <paramref name="after"/> hooks can use it.
        /// This also handles reentrancy since this acts as an exclusive lock on the domain monitor.
        /// </summary>
        /// <param name="before">Optional hook that is called before the operation.</param>
        /// <param name="after">Optional hook that is called after the operation (if no exception has been thrown).</param>
        /// <returns>The transaction result (that may be <see cref="TransactionResult.Empty"/>) and the potential exception.</returns>
        public async Task<(TransactionResult,Exception)> FromTimerModifyAsync( Func<IActivityMonitor,Task> before = null, Func<IActivityMonitor,TransactionResult, Task> after = null )
        {
            TransactionResult tr = TransactionResult.Empty;
            Exception error = null;
            var monitor = LockDomainBoundMonitor();
            if( monitor == null ) return (tr,error);
            try
            {
                await before?.Invoke( monitor );
                (tr,error) = await FromTimerModifyAsync( monitor );
                if( error == null && after != null ) 
                {
                    await after( monitor, tr );    
                }
            }
            catch( Exception ex )
            {
                monitor.Error( error = ex );
            }
            finally
            {
                UnlockDomainBoundMonitor();
            }
            return (tr, error);
        }

        /// <summary>
        /// Exports this domain as a JSON object with the <see cref="TransactionSerialNumber"/>,
        /// the property name mappings, and the object graph itself that is compatible
        /// with @signature/json-graph-serialization package and requires a post processing to lift
        /// container (map, list and set) contents.
        /// </summary>
        /// <param name="w">The text writer.</param>
        /// <param name="millisecondsTimeout">
        /// The maximum number of milliseconds to wait for a read access before giving up.
        /// Wait indefinitely by default.
        /// </param>
        /// <returns>True on success, false if timeout occurred.</returns>
        public bool Export( TextWriter w, int millisecondsTimeout = -1 )
        {
            if( !_lock.TryEnterReadLock( millisecondsTimeout ) ) return false;
            try
            {
                var target = new JSONExportTarget( w );
                target.EmitStartObject( -1, ObjectExportedKind.Object );
                target.EmitPropertyName( "N" );
                target.EmitInt32( _transactionSerialNumber );
                target.EmitPropertyName( "C" );
                target.EmitInt32( _actualObjectCount );
                target.EmitPropertyName( "P" );
                target.EmitStartObject( -1, ObjectExportedKind.List );
                foreach( var p in _properties )
                {
                    target.EmitString( p.Value.Name );
                }
                target.EmitEndObject( -1, ObjectExportedKind.List );

                target.EmitPropertyName( "O" );
                ObjectExporter.ExportRootList( target, _objects.Take( _objectsListCount ), _exporters );

                target.EmitPropertyName( "R" );
                target.EmitStartObject( -1, ObjectExportedKind.List );
                foreach( var r in _roots )
                {
                    target.EmitInt32( r.OId );
                }
                target.EmitEndObject( -1, ObjectExportedKind.List );

                target.EmitEndObject( -1, ObjectExportedKind.Object );
                return true;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Exports the whole domain state as a JSON object (simple helper that calls <see cref="Export(TextWriter,int)"/>).
        /// </summary>
        /// <param name="millisecondsTimeout">
        /// The maximum number of milliseconds to wait for a read access before giving up.
        /// Wait indefinitely by default.
        /// </param>
        /// <returns>The state as a string or null if timeout occurred.</returns>
        public string ExportToString( int millisecondsTimeout = -1 )
        {
            var w = new StringWriter();
            return Export( w, millisecondsTimeout ) ? w.ToString() : null;
        }

        /// <summary>
        /// Saves <see cref="AllObjects"/> of this domain.
        /// </summary>
        /// <param name="s">The output stream.</param>
        /// <param name="leaveOpen">True to leave the stream opened.</param>
        /// <param name="encoding">Optional encoding for characters. Defaults to UTF-8.</param>
        /// <param name="millisecondsTimeout">
        /// The maximum number of milliseconds to wait for a read access before giving up.
        /// Wait indefinitely by default.
        /// </param>
        /// <returns>True on success, false if timeout occurred.</returns>
        public bool Save( Stream s, bool leaveOpen = false, Encoding encoding = null, int millisecondsTimeout = -1 )
        {
            using( var w = new BinarySerializer( s, _serializers, leaveOpen, encoding ) )
            {
                bool isWrite = _lock.IsWriteLockHeld;
                if( !isWrite && !_lock.TryEnterReadLock( millisecondsTimeout ) ) return false;
                try
                {
                    using( isWrite ? Monitor.OpenInfo( $"Transacted saving domain ({_actualObjectCount} objects)." ) : null )
                    {
                        w.WriteSmallInt32( 1 ); // Version
                        w.Write( DomainName );
                        w.Write( _transactionSerialNumber );
                        w.Write( _actualObjectCount );
                        w.WriteNonNegativeSmallInt32( _freeList.Count );
                        foreach( var i in _freeList ) w.WriteNonNegativeSmallInt32( i );
                        w.WriteNonNegativeSmallInt32( _properties.Count );
                        foreach( var p in _propertiesByIndex )
                        {
                            w.Write( p.Name );
                        }
                        Debug.Assert( _objectsListCount == _actualObjectCount + _freeList.Count );
                        for( int i = 0; i < _objectsListCount; ++i )
                        {
                            w.WriteObject( _objects[i] );
                        }
                        w.WriteNonNegativeSmallInt32( _roots.Count );
                        foreach( var r in _roots ) w.WriteNonNegativeSmallInt32( r.OId );
                        return true;
                    }
                }
                finally
                {
                    if( !isWrite ) _lock.ExitReadLock();
                }
            }
        }

        /// <summary>
        /// Loads previously <see cref="Save"/>d objects into this domain.
        /// </summary>
        /// <param name="s">The input stream.</param>
        /// <param name="leaveOpen">True to leave the stream opened.</param>
        /// <param name="encoding">Optional encoding for characters. Defaults to UTF-8.</param>
        /// <param name="millisecondsTimeout">
        /// The maximum number of milliseconds to wait for a read access before giving up.
        /// Wait indefinitely by default.
        /// </param>
        /// <returns>True on success, false if timeout occurred.</returns>
        public bool Load( Stream s, bool leaveOpen = false, Encoding encoding = null, int millisecondsTimeout = -1 )
        {
            bool isWrite = _lock.IsWriteLockHeld;
            if( !isWrite && !_lock.TryEnterWriteLock( millisecondsTimeout ) ) return false;
            try
            {
                using( Monitor.OpenInfo( $"Transacted loading domain." ) )
                using( var d = new BinaryDeserializer( s, null, _deserializers, leaveOpen, encoding ) )
                {
                    d.Services.Add( this );
                    DoLoad( d );
                    return true;
                }
            }
            finally
            {
                if( !isWrite ) _lock.ExitWriteLock();
            }
        }

        void DoLoad( BinaryDeserializer r )
        {
            Debug.Assert( _lock.IsWriteLockHeld );
            _deserializing = true;
            try
            {
                int version = r.ReadSmallInt32();
                if( version > 0 )
                {
                    var check = r.ReadString();
                    if( DomainName != check ) throw new InvalidDataException( $"Domain name mismatch: reading '{check}' into domain named '{DomainName}'." );
                }
                _transactionSerialNumber = r.ReadInt32();
                _actualObjectCount = r.ReadInt32();

                _freeList.Clear();
                int count = r.ReadNonNegativeSmallInt32();
                while( --count >= 0 )
                {
                    _freeList.Push( r.ReadNonNegativeSmallInt32() );
                }

                _properties.Clear();
                _propertiesByIndex.Clear();
                count = r.ReadNonNegativeSmallInt32();
                for( int iProp = 0; iProp < count; iProp++ )
                {
                    string name = r.ReadString();
                    var p = new PropInfo( iProp, name );
                    _properties.Add( name, p );
                    _propertiesByIndex.Add( p );
                }
                for( int i = 0; i < _objectsListCount; ++i )
                {
                    var o = _objects[i];
                    if( o != null )
                    {
                        Debug.Assert( !o.IsDisposed );
                        o.OnDisposed( true );
                    }
                }
                Array.Clear( _objects, 0, _objectsListCount );
                _objectsListCount = count = _actualObjectCount + _freeList.Count;
                while( _objectsListCount > _objects.Length )
                {
                    Array.Resize( ref _objects, _objects.Length * 2 );
                }
                for( int i = 0; i < count; ++i )
                {
                    _objects[i] = (ObservableObject)r.ReadObject();
                }
                r.ImplementationServices.ExecutePostDeserializationActions();
                _roots.Clear();
                count = r.ReadNonNegativeSmallInt32();
                while( --count >= 0 )
                {
                    _roots.Add( _objects[r.ReadNonNegativeSmallInt32()] as ObservableRootObject );
                }
                OnLoaded();
            }
            finally
            {
                _deserializing = false;
            }
        }

        /// <summary>
        /// Called after a <see cref="Load"/>.
        /// Does nothing at this level.
        /// </summary>
        protected internal virtual void OnLoaded()
        {
        }

        /// <summary>
        /// Gets the active domain on the current thread (the last one for which a <see cref="BeginTransaction"/>
        /// has been done an not yet disposed) or throws an <see cref="InvalidOperationException"/> if there is none.
        /// </summary>
        /// <returns>The current domain.</returns>
        internal static ObservableDomain GetCurrentActiveDomain()
        {
            if( CurrentThreadDomain == null )
            {
                throw new InvalidOperationException( "A transaction is required (Observable objects can be created only inside a transaction)." );
            }
            return CurrentThreadDomain;
        }

        internal bool IsDeserializing => _deserializing;

        internal int Register( ObservableObject o )
        {
            CheckWriteLockAndObjectDisposed( o );
            Debug.Assert( o != null && o.Domain == this );
            int idx;
            if( _freeList.Count > 0 )
            {
                idx = _freeList.Pop();
            }
            else
            {
                idx = _objectsListCount++;
                if( idx == _objects.Length )
                {
                    Array.Resize( ref _objects, idx * 2 );
                }
            }
            _objects[idx] = o;
            if( !_deserializing )
            {
                _changeTracker.OnNewObject( o, idx, o._exporter );
            }
            ++_actualObjectCount;
            return idx;
        }

        internal void Unregister( ObservableObject o )
        {
            Debug.Assert( !o.IsDisposed );
            CheckWriteLockAndObjectDisposed( o );
            if( !_deserializing ) _changeTracker.OnDisposeObject( o );
            _objects[o.OId] = null;
            _freeList.Push( o.OId );
            --_actualObjectCount;
        }

        internal TimerHost TimerHost => _timerHost;

        internal void SendCommand( ObservableObject o, object command )
        {
            CheckWriteLockAndObjectDisposed( o );
            _changeTracker.OnSendCommand( new ObservableCommand( o, command ) );
        }

        internal PropertyChangedEventArgs OnPropertyChanged( ObservableObject o, string propertyName, object before, object after )
        {
            if( _deserializing
                || o._exporter == null
                || !o._exporter.ExportableProperties.Any( prop => prop.Name == propertyName ) )
            {
                return null;
            }
            CheckWriteLockAndObjectDisposed( o );
            PropInfo p = EnsurePropertyInfo( propertyName );
            _changeTracker.OnPropertyChanged( o, p, before, after );
            return p.EventArg;
        }

        PropInfo EnsurePropertyInfo( string propertyName )
        {
            if( !_properties.TryGetValue( propertyName, out var p ) )
            {
                p = new PropInfo( _properties.Count, propertyName );
                _changeTracker.OnNewProperty( p );
                _properties.Add( propertyName, p );
                _propertiesByIndex.Add( p );
            }

            return p;
        }

        internal ListRemoveAtEvent OnListRemoveAt( ObservableObject o, int index )
        {
            if( _deserializing ) return null;
            CheckWriteLockAndObjectDisposed( o );
            return _changeTracker.OnListRemoveAt( o, index );
        }

        internal ListSetAtEvent OnListSetAt( ObservableObject o, int index, object value )
        {
            if( _deserializing ) return null;
            CheckWriteLockAndObjectDisposed( o );
            return _changeTracker.OnListSetAt( o, index, value );
        }

        internal CollectionClearEvent OnCollectionClear( ObservableObject o )
        {
            if( _deserializing ) return null;
            CheckWriteLockAndObjectDisposed( o );
            return _changeTracker.OnCollectionClear( o );
        }

        internal ListInsertEvent OnListInsert( ObservableObject o, int index, object item )
        {
            if( _deserializing ) return null;
            CheckWriteLockAndObjectDisposed( o );
            return _changeTracker.OnListInsert( o, index, item );
        }

        internal CollectionMapSetEvent OnCollectionMapSet( ObservableObject o, object key, object value )
        {
            if( _deserializing ) return null;
            CheckWriteLockAndObjectDisposed( o );
            return _changeTracker.OnCollectionMapSet( o, key, value );
        }

        internal CollectionRemoveKeyEvent OnCollectionRemoveKey( ObservableObject o, object key )
        {
            if( _deserializing ) return null;
            CheckWriteLockAndObjectDisposed( o );
            return _changeTracker.OnCollectionRemoveKey( o, key );
        }

        void CheckWriteLockAndObjectDisposed( ObservableObject o )
        {
            if( !_lock.IsWriteLockHeld )
            {
                if( _currentTran == null ) throw new InvalidOperationException( "A transaction is required." );
                if( _lock.IsReadLockHeld ) throw new InvalidOperationException( "Concurrent access: only Read lock has been acquired." );
                throw new InvalidOperationException( "Concurrent access: no lock has been acquired." );
            }
            if( o.IsDisposed ) throw new ObjectDisposedException( o.GetType().FullName );
        }

    }
}
