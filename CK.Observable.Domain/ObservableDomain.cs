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
    /// Base class for any observable domain without strongly typed root. This class should not be specialized:
    /// you must use specialized <see cref="ObservableChannel{T}"/> or <see cref="ObservableDomain{T1, T2, T3, T4}"/>
    /// for doamins with strongly typed roots.
    /// </summary>
    public class ObservableDomain : IDisposable
    {
        /// <summary>
        /// An artificial <see cref="CKExceptionData"/> that is added to
        /// <see cref="IObservableTransaction.Errors"/> whenever a transaction
        /// has not been committed.
        /// </summary>
        public static readonly CKExceptionData UncomittedTransaction = new CKExceptionData( "Uncommitted transaction.", "Not.An.Exception", "Not.An.Exception, No.Assembly", null, null, null, null, null, null );

        /// <summary>
        /// Default timeout before <see cref="ObtainDomainMonitor(int, bool)"/> creates a new temporary <see cref="IDisposableActivityMonitor"/>
        /// instead of reusing the default one.
        /// </summary>
        public const int LockedDomainMonitorTimeout = 1000;

        /// <summary>
        /// Current serialization version.
        /// </summary>
        public const int CurrentSerializationVersion = 2;

        /// <summary>
        /// The length in bytes of the <see cref="SecretKey"/>.
        /// </summary>
        public const int DomainSecretKeyLength = 512;

        static readonly Type[] _observableRootCtorParameters = new Type[] { typeof( ObservableDomain ) };

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

            /// <summary>
            /// Builds a long value based on the <see cref="ObservableObjectId.Index"/> and <see cref="PropertyId"/>
            /// to use it as a unique local key to track/dedup property changed event.
            /// </summary>
            /// <param name="o">The owning object.</param>
            /// <returns>The key to use for this property of the specified object.</returns>
            public long GetObjectPropertyId( ObservableObject o )
            {
                Debug.Assert( o.OId.IsValid );
                long r = o.OId.Index;
                return (r << 24) | (uint)PropertyId;
            }
        }

        [ThreadStatic]
        internal static ObservableDomain CurrentThreadDomain;

        internal readonly IExporterResolver _exporters;
        readonly ISerializerResolver _serializers;
        readonly IDeserializerResolver _deserializers;
        readonly TimeManager _timeManager;

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
        readonly List<int> _freeList;
        byte[] _domainSecret;

        int _internalObjectCount;
        InternalObject _firstInternalObject;
        InternalObject _lastInternalObject;

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

        int _currentObjectUniquifier;

        /// <summary>
        /// The root objects among all _objects.
        /// </summary>
        List<ObservableRootObject> _roots;

        IObservableTransaction _currentTran;
        int _transactionSerialNumber;

        // Available to objects.
        internal readonly ObservableDomainEventArgs DefaultEventArgs;

        // A reusable domain monitor is created on-demand and is protecte dby an exclusive lock.
        DomainActivityMonitor _domainMonitor;
        readonly object _domainMonitorLock;

        // This lock is used to allow one and only one Save at a time: this is to protect
        // the potential fake transaction that is used when saving.
        readonly object _saveLock;

        bool _deserializing;
        bool _disposed;

        /// <summary>
        /// Exposes the non null objects in _objects as a collection.
        /// </summary>
        class AllCollection : IObservableObjectCollection
        {
            readonly ObservableDomain _d;

            public AllCollection( ObservableDomain d )
            {
                _d = d;
            }

            public ObservableObject this[long id] => this[new ObservableObjectId( id, false )];

            public ObservableObject this[ObservableObjectId id]
            {
                get
                {
                    if( id.IsValid )
                    {
                        int idx = id.Index;
                        if( idx < _d._objectsListCount )
                        {
                            var o = _d._objects[idx];
                            if( o.OId == id ) return o;
                        }
                    }
                    return null;
                }
            }

            public int Count => _d._actualObjectCount;

            public T Get<T>( ObservableObjectId id, bool throwOnTypeMismacth = true ) where T : ObservableObject
            {
                var o = this[id];
                if( o == null ) return null;
                return throwOnTypeMismacth ? (T)o : o as T;
            }

            public T Get<T>( long id, bool throwOnTypeMismacth = true ) where T : ObservableObject => Get<T>( new ObservableObjectId( id, false ) );

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

            /// <summary>
            /// Called when a new object is being created.
            /// </summary>
            /// <param name="o">The oobject itself.</param>
            /// <param name="objectId">The assigned object identifier.</param>
            /// <param name="exporter">The export driver of the object. Can be null.</param>
            internal void OnNewObject( ObservableObject o, ObservableObjectId objectId, IObjectExportTypeDriver exporter )
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
            readonly IDisposableGroup _monitorGroup;
            readonly DateTime _startTime;
            CKExceptionData[] _errors;
            TransactionResult _result;
            bool _resultInitialized;

            public Transaction( ObservableDomain d, IActivityMonitor monitor, DateTime startTime, IDisposableGroup g )
            {
                _domain = d;
                Monitor = monitor;
                _previous = CurrentThreadDomain;
                CurrentThreadDomain = d;
                _startTime = startTime;
                _monitorGroup = g;
                _errors = Array.Empty<CKExceptionData>();
            }

            public DateTime StartTime => _startTime;

            public IActivityMonitor Monitor { get; }

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
                DateTime nextTimerDueDate = Util.UtcMinValue;
                try
                {
                    nextTimerDueDate = _domain._timeManager.ApplyChanges();
                }
                catch( Exception ex )
                {
                    Monitor.Error( ex );
                    AddError( CKExceptionData.CreateFrom( ex ) );
                }
                CurrentThreadDomain = _previous;
                if( _errors.Length != 0 )
                {
                    // On errors, resets the change tracker, sends the errors to the managers
                    // and creates an error TransactionResult. 
                    _result = new TransactionResult( _errors, _startTime, nextTimerDueDate );
                    _domain._changeTracker.Reset();
                    try
                    {
                        _domain.DomainClient?.OnTransactionFailure( Monitor, _domain, _errors );
                    }
                    catch( Exception ex )
                    {
                        Monitor.Error( "Error in IObservableTransactionManager.OnTransactionFailure.", ex );
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
                        Monitor.Fatal( "Error in IObservableTransactionManager.OnTransactionCommit.", ex );
                        _result = new TransactionResult( ctx ).WithClientError( ex );
                    }
                }
                var next = _result.NextDueTimeUtc;
                if( next != Util.UtcMinValue ) _domain._timeManager.SetNextDueTimeUtc( Monitor, next );
                _monitorGroup.Dispose();
                _domain._currentTran = null;
                _domain._lock.ExitWriteLock();
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
        /// Initializes a new <see cref="ObservableDomain"/> without any <see cref="DomainClient"/>.
        /// </summary>
        /// <param name="monitor">The monitor used to log the construction of this domain. Can not be null.</param>
        /// <param name="domainName">Name of the domain. Must not be null but can be empty.</param>
        public ObservableDomain( IActivityMonitor monitor, string domainName )
            : this( monitor, domainName, null )
        {
        }

        /// <summary>
        /// Initializes a new <see cref="ObservableDomain"/> with a <see cref="Monitor"/>,
        /// a <see cref="DomainClient"/> an optionals explicit exporter, serializer
        /// and deserializer handlers.
        /// </summary>
        /// <param name="monitor">The monitor used to log the construction of this domain. Cannot be null.</param>
        /// <param name="domainName">Name of the domain. Must not be null but can be empty.</param>
        /// <param name="client">The transaction manager to use. Can be null.</param>
        /// <param name="exporters">Optional exporters handler.</param>
        /// <param name="serializers">Optional serializers handler.</param>
        /// <param name="deserializers">Optional deserializers handler.</param>
        public ObservableDomain( IActivityMonitor monitor,
                                 string domainName,
                                 IObservableDomainClient client,
                                 IExporterResolver exporters = null,
                                 ISerializerResolver serializers = null,
                                 IDeserializerResolver deserializers = null )
            : this( monitor, domainName, client, true, exporters, serializers, deserializers )
        {
        }

        /// <summary>
        /// Initializes a previously <see cref="Save"/>d domain.
        /// </summary>
        /// <param name="monitor">The monitor used to log the construction of this domain. Can not be null.</param>
        /// <param name="domainName">Name of the domain. Must not be null but can be empty.</param>
        /// <param name="client">The transaction manager to use. Can be null.</param>
        /// <param name="s">The input stream.</param>
        /// <param name="leaveOpen">True to leave the stream opened.</param>
        /// <param name="encoding">Optional encoding for characters. Defaults to UTF-8.</param>
        /// <param name="exporters">Optional exporters handler.</param>
        /// <param name="serializers">Optional serializers handler.</param>
        /// <param name="deserializers">Optional deserializers handler.</param>
        public ObservableDomain( IActivityMonitor monitor,
                                 string domainName,
                                 IObservableDomainClient client,
                                 Stream s,
                                 bool leaveOpen = false,
                                 Encoding encoding = null,
                                 IExporterResolver exporters = null,
                                 ISerializerResolver serializers = null,
                                 IDeserializerResolver deserializers = null )
            : this( monitor, domainName, client, false, exporters, serializers, deserializers )
        {
            Load( monitor, s, leaveOpen, encoding );
            client?.OnDomainCreated( monitor, this, DateTime.UtcNow );
        }

        ObservableDomain( IActivityMonitor monitor,
                          string domainName,
                          IObservableDomainClient client,
                          bool callClientOnCreate,
                          IExporterResolver exporters,
                          ISerializerResolver serializers,
                          IDeserializerResolver deserializers )
        {
            if( monitor == null ) throw new ArgumentNullException( nameof( monitor ) );
            DomainName = domainName ?? throw new ArgumentNullException( nameof( domainName ) );
            _exporters = exporters ?? ExporterRegistry.Default;
            _serializers = serializers ?? SerializerRegistry.Default;
            _deserializers = deserializers ?? DeserializerRegistry.Default;
            DomainClient = client;
            _objects = new ObservableObject[512];
            _freeList = new List<int>();
            _properties = new Dictionary<string, PropInfo>();
            _propertiesByIndex = new List<PropInfo>();
            _changeTracker = new ChangeTracker();
            _exposedObjects = new AllCollection( this );
            _roots = new List<ObservableRootObject>();
            _timeManager = new TimeManager( this );
            DefaultEventArgs = new ObservableDomainEventArgs( this );
            // LockRecursionPolicy.NoRecursion: reentrancy must NOT be allowed.
            _lock = new ReaderWriterLockSlim( LockRecursionPolicy.NoRecursion );
            _saveLock = new Object();
            _domainMonitorLock = new Object();

            if( callClientOnCreate ) client?.OnDomainCreated( monitor, this, DateTime.UtcNow );
            if( _domainSecret == null ) _domainSecret = CreateSecret();
            monitor.Info( $"ObservableDomain {domainName} created." );
        }

        static byte[] CreateSecret()
        {
            using( var c = new System.Security.Cryptography.Rfc2898DeriveBytes( Guid.NewGuid().ToString(), Guid.NewGuid().ToByteArray(), 1000 ) )
            {
                return c.GetBytes( 512 );
            }
        }

        /// <summary>
        /// Empty transaction object: must be used during initialization (for <see cref="AddRoot{T}(InitializationTransaction)"/>
        /// to be called).
        /// </summary>
        private protected class InitializationTransaction : IObservableTransaction
        {
            readonly ObservableDomain _d;
            readonly ObservableDomain _previousThreadDomain;
            readonly IObservableTransaction _previousTran;
            readonly DateTime _startTime;
            readonly IActivityMonitor _monitor;
            readonly bool _enterWriteLock;

            /// <summary>
            /// Initializes a new <see cref="InitializationTransaction"/> required
            /// to call <see cref="AddRoot{T}(InitializationTransaction)"/>.
            /// </summary>
            /// <param name="m">The monitor to use while this transaction is the current one.</param>
            /// <param name="d">The observable domain.</param>
            /// <param name="enterWriteLock">False to not enter and exit the write lock.</param>
            public InitializationTransaction( IActivityMonitor m, ObservableDomain d, bool enterWriteLock = true )
            {
                _monitor = m;
                _startTime = DateTime.UtcNow;
                _d = d;
                if( _enterWriteLock = enterWriteLock ) d._lock.EnterWriteLock();
                _previousTran = d._currentTran;
                d._currentTran = this;
                _previousThreadDomain = CurrentThreadDomain;
                CurrentThreadDomain = d;
                d._deserializing = true;
            }
            IActivityMonitor IObservableTransaction.Monitor => _monitor;

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
                CurrentThreadDomain = _previousThreadDomain;
                _d._currentTran = _previousTran;
                if( _enterWriteLock ) _d._lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// This must be called only from <see cref="ObservableDomain{T}"/> (and other ObservableDomain generics) constructors.
        /// No event are collected: this is the initial state of the domain.
        /// </summary>
        /// <typeparam name="T">The root type.</typeparam>
        /// <returns>The instance.</returns>
        private protected T AddRoot<T>( InitializationTransaction initializationContext ) where T : ObservableRootObject
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
        /// These exposed objects are out of any transactions or reentrancy checks: they should not 
        /// be used outside of <see cref="BeginTransaction"/> (or other <see cref="Modify"/>, <see cref="ModifyAsync"/> methods)
        /// or <see cref="AcquireReadLock"/> scopes.
        /// </summary>
        public IObservableObjectCollection AllObjects => _exposedObjects;

        /// <summary>
        /// Gets all the internal objects that this domain contains.
        /// These exposed objects are out of any transactions or reentrancy checks: they should not 
        /// be used outside of <see cref="BeginTransaction"/> (or other <see cref="Modify"/>, <see cref="ModifyAsync"/> methods)
        /// or <see cref="AcquireReadLock"/> scopes.
        /// </summary>
        public IEnumerable<InternalObject> AllInternalObjects
        {
            get
            {
                var o = _firstInternalObject;
                while( o != null )
                {
                    yield return o;
                    o = o.Next;
                }
            }
        }

        /// <summary>
        /// Gets the root observable objects that this domain contains.
        /// These exposed objects are out of any transactions or reentrancy checks: they should not 
        /// be used outside of <see cref="BeginTransaction"/> (or other <see cref="Modify"/>, <see cref="ModifyAsync"/> methods)
        /// or <see cref="AcquireReadLock"/> scopes.
        /// </summary>
        public IReadOnlyList<ObservableRootObject> AllRoots => _roots;

        /// <summary>
        /// Gets the current transaction number.
        /// Incremented each time a transaction successfuly ended.
        /// </summary>
        public int TransactionSerialNumber => _transactionSerialNumber;

        class DomainActivityMonitor : ActivityMonitor, IDisposableActivityMonitor
        {
            readonly ObservableDomain _domain;

            public DomainActivityMonitor( string topic, ObservableDomain domain, int timeout )
                : base( $"Observable domain '{topic}'." )
            {
                if( (_domain = domain) == null ) this.Error( $"Failed to obtain the locked domain monitor in less than {timeout} ms." );
            }

            public void Dispose()
            {
                if( _domain == null )
                {
                    this.MonitorEnd();
                }
                else
                {
                    while( CloseGroup( new DateTimeStamp( LastLogTime, DateTime.UtcNow ) ) ) ;
                    Monitor.Exit( _domain._domainMonitorLock );
                }
            }
        }

        /// <summary>
        /// Gets the monitor to use (from the current transaction).
        /// </summary>
        internal IActivityMonitor CurrentMonitor => _currentTran.Monitor;

        /// <summary>
        /// Gets this domain name.
        /// </summary>
        public string DomainName { get; }

        /// <summary>
        /// Gets the secret key for this domain. It is a <see cref="System.Security.Cryptography.Rfc2898DeriveBytes"/> bytes
        /// array of length <see cref="DomainSecretKeyLength"/> derived from <see cref="Guid.NewGuid()"/>.
        /// </summary>
        public ReadOnlySpan<byte> SecretKey => _domainSecret.AsSpan();

        /// <summary>
        /// Gets the associated client (head of the Chain of Responsibility).
        /// Can be null.
        /// </summary>
        public IObservableDomainClient DomainClient { get; }

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
        /// Any exceptions raised by <see cref="IObservableDomainClient.OnTransactionStart(IActivityMonitor, ObservableDomain, DateTime)"/> are thrown
        /// by this method.
        /// </summary>
        /// <param name="monitor">Monitor to use. Cannot be null.</param>
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
            if( _disposed ) throw new ObjectDisposedException( ToString() );
            if( !_lock.TryEnterWriteLock( millisecondsTimeout ) )
            {
                monitor.Warn( $"Write lock not obtained in less than {millisecondsTimeout} ms." );
                return null;
            }
            return DoBeginTransaction( monitor, throwException: true ).Item1;
        }

        /// <summary>
        /// Returns the created IObservableTransaction XOR an IObservableDomainClient.OnTransactionStart exception.
        /// Write lock must be held before the call and kept until (but released on error).
        /// </summary>
        /// <param name="m">The monitor to use.</param>
        /// <param name="throwException">Whether to throw or return the potential IObservableDomainClient.OnTransactionStart exception.</param>
        /// <returns>The transaction XOR the IObservableDomainClient.OnTransactionStart exception.</returns>
        (IObservableTransaction, Exception) DoBeginTransaction( IActivityMonitor m, bool throwException )
        {
            Debug.Assert( m != null && _lock.IsWriteLockHeld );
            var group = m.OpenTrace( "Starting transaction." );
            try
            {
                var startTime = DateTime.UtcNow;
                DomainClient?.OnTransactionStart( m, this, startTime );
                return (_currentTran = new Transaction( this, m, startTime, group ), null);
            }
            catch( Exception ex )
            {
                m.Error( "While calling IObservableTransactionManager.OnTransactionStart().", ex );
                group.Dispose();
                _lock.ExitWriteLock();
                if( throwException ) throw;
                return (null, ex);
            }
        }

        /// <summary>
        /// Enables modifications to be done inside a transaction and a try/catch block.
        /// Any exceptions raised by <see cref="IObservableDomainClient.OnTransactionStart(IActivityMonitor,ObservableDomain, DateTime)"/> are thrown
        /// by this method, but any other exceptions are caught, logged, and appears in <see cref="TransactionResult"/>.
        /// </summary>
        /// <param name="monitor">Monitor to use. Cannot be null.</param>
        /// <param name="actions">
        /// The actions to execute inside the ObservableDomain's current transaction.
        /// Can be null: only pending timed events are executed if any.
        /// </param>
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
                return DoModifyAndCommit( actions, t );
            }
        }

        /// <summary>
        /// Modify the domain once a transaction has been opened and calls the <see cref="IObservableDomainClient"/>
        /// that have been registered: all this occurs in the lock and it is released at the end. This never throws
        /// since the transaction result contains any errors.
        /// </summary>
        /// <param name="actions">The actions to execute. Can be null.</param>
        /// <param name="t">The observable transaction. Cannot be null.</param>
        /// <returns>The transaction result. Will never be null.</returns>
        TransactionResult DoModifyAndCommit( Action actions, IObservableTransaction t )
        {
            Debug.Assert( t != null );
            try
            {
                _timeManager.RaiseElapsedEvent( t.StartTime, false );
                if( actions != null )
                {
                    actions();
                    _timeManager.RaiseElapsedEvent( DateTime.UtcNow, true );
                }
            }
            catch( Exception ex )
            {
                t.Monitor.Error( ex );
                t.AddError( CKExceptionData.CreateFrom( ex ) );
            }
            // The transaction commit updates the timers to return the NextDueTime.
            return t.Commit();
        }

        /// <summary>
        /// Modifies this ObservableDomain and then executes any pending post-actions.
        /// Any exceptions raised by <see cref="IObservableDomainClient.OnTransactionStart(IActivityMonitor,ObservableDomain, DateTime)"/> (at the start of the process)
        /// and by <see cref="TransactionResult.PostActions"/> (after the successful commit or the failure) are thrown by this method.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="actions">
        /// The actions to execute inside the ObservableDomain's current transaction.
        /// Can be null: only pending timed events are executed if any.
        /// </param>
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
            await tr.ExecutePostActionsAsync( monitor, throwException: true );
            return tr;
        }

        /// <summary>
        /// Safe version of <see cref="ModifyAsync(IActivityMonitor, Action, int)"/> that will never throw: any exception raised
        /// by <see cref="IObservableDomainClient.OnTransactionStart(IActivityMonitor, ObservableDomain, DateTime)"/>
        /// or <see cref="TransactionResult.ExecutePostActionsAsync(IActivityMonitor, bool)"/> is logged and returned.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="actions">
        /// The actions to execute inside the ObservableDomain's current transaction.
        /// Can be null: only pending timed events are executed if any.
        /// </param>
        /// <param name="millisecondsTimeout">
        /// The maximum number of milliseconds to wait for a read access before giving up.
        /// Wait indefinitely by default.
        /// </param>
        /// <returns>
        /// Returns the transaction result (that may be <see cref="TransactionResult.Empty"/>) and any exception outside of the observable transaction itself.
        /// </returns>
        public async Task<(TransactionResult, Exception)> SafeModifyAsync( IActivityMonitor monitor, Action actions, int millisecondsTimeout = -1 )
        {
            if( monitor == null ) throw new ArgumentNullException( nameof( monitor ) );
            if( _disposed ) throw new ObjectDisposedException( ToString() );
            TransactionResult tr = TransactionResult.Empty;
            if( _lock.TryEnterWriteLock( millisecondsTimeout ) )
            {
                var tEx = DoBeginTransaction( monitor, false );
                Debug.Assert( (tEx.Item1 != null) != (tEx.Item2 != null), "IObservableTransaction XOR IObservableDomainClient.OnTransactionStart() exception." );
                if( tEx.Item2 != null ) return (tr, tEx.Item2);
                tr = DoModifyAndCommit( actions, tEx.Item1 );
            }
            else monitor.Warn( $"WriteLock not obtained in {millisecondsTimeout} ms (returning TransactionResult.Empty)." );
            return (tr, await tr.ExecutePostActionsAsync( monitor, throwException: false ));
        }

        /// <summary>
        /// Exports this domain as a JSON object with the <see cref="TransactionSerialNumber"/>,
        /// the property name mappings, and the object graph itself that is compatible
        /// with @signature/json-graph-serialization package and requires a post processing to lift
        /// container (map, list and set) contents.
        /// </summary>
        /// <param name="w">The text writer.</param>
        /// <param name="milliSecondsTimeout">
        /// The maximum number of milliseconds to wait for a read access before giving up.
        /// Wait indefinitely by default.
        /// </param>
        /// <returns>True on success, false if timeout occurred.</returns>
        public bool Export( TextWriter w, int milliSecondsTimeout = -1 )
        {
            if( _disposed ) throw new ObjectDisposedException( ToString() );
            if( !_lock.TryEnterReadLock( milliSecondsTimeout ) ) return false;
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
                    target.EmitInt32( r.OId.Index );
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
        /// <param name="monitor">The monitor to use. Cannot be null.</param>
        /// <param name="stream">The output stream.</param>
        /// <param name="leaveOpen">True to leave the stream opened.</param>
        /// <param name="debugMode">True to activate <see cref="BinarySerializer.IsDebugMode"/>.</param>
        /// <param name="encoding">Optional encoding for characters. Defaults to UTF-8.</param>
        /// <param name="millisecondsTimeout">
        /// The maximum number of milliseconds to wait for a read access before giving up.
        /// Wait indefinitely by default.
        /// </param>
        /// <returns>True on success, false if timeout occurred.</returns>
        public bool Save( IActivityMonitor monitor, Stream stream, bool leaveOpen = false, bool debugMode = false, Encoding encoding = null, int millisecondsTimeout = -1 )
        {
            if( monitor == null ) throw new ArgumentNullException( nameof( monitor ) );
            if( stream == null ) throw new ArgumentNullException( nameof( stream ) );
            if( _disposed ) throw new ObjectDisposedException( ToString() );

            // Since we only need the read lock, whenever multiple threads Save() concurrently,
            // the monitor (of the fake transaction) is at risk. This is why we secure the Save with its own lock: since
            // only one Save at a time can be executed and no other "read with a monitor (even in a fake transaction)" exists.
            // Since this is clearly an edge case, we use a lock with the same timeout and we don't care of a potential 2x wait time.
            if( !Monitor.TryEnter( _saveLock, millisecondsTimeout ) ) return false;
            using( var w = new BinarySerializer( stream, _serializers, leaveOpen, encoding ) )
            {
                bool isWrite = _lock.IsWriteLockHeld;
                bool isRead = _lock.IsReadLockHeld;
                if( !isWrite && !isRead && !_lock.TryEnterReadLock( millisecondsTimeout ) )
                {
                    Monitor.Exit( _saveLock );
                    return false;
                }
                bool needFakeTran = _currentTran == null || _currentTran.Monitor != monitor;
                if( needFakeTran ) new InitializationTransaction( monitor, this, false );
                try
                {
                    using( isWrite ? monitor.OpenInfo( $"Transacted saving domain ({_actualObjectCount} objects, {_internalObjectCount} internal objects)." ) : null )
                    {
                        w.WriteSmallInt32( CurrentSerializationVersion ); // Version: 2 supports DebuMode, TimeManager & Internal objects.
                        w.DebugWriteMode( debugMode ? (bool?)debugMode : null );
                        w.Write( _currentObjectUniquifier );
                        w.Write( _domainSecret );
                        if( debugMode ) monitor.Trace( $"Domain {DomainName}: Tran #{_transactionSerialNumber}, {_actualObjectCount} objects." );
                        w.Write( DomainName );
                        w.Write( _transactionSerialNumber );
                        w.Write( _actualObjectCount );

                        w.DebugWriteSentinel();
                        w.WriteNonNegativeSmallInt32( _freeList.Count );
                        foreach( var i in _freeList ) w.WriteNonNegativeSmallInt32( i );

                        w.DebugWriteSentinel();
                        w.WriteNonNegativeSmallInt32( _properties.Count );
                        foreach( var p in _propertiesByIndex )
                        {
                            w.Write( p.Name );
                        }

                        w.DebugWriteSentinel();
                        Debug.Assert( _objectsListCount == _actualObjectCount + _freeList.Count );
                        for( int i = 0; i < _objectsListCount; ++i )
                        {
                            w.WriteObject( _objects[i] );
                        }

                        w.DebugWriteSentinel();
                        w.WriteNonNegativeSmallInt32( _roots.Count );
                        foreach( var r in _roots ) w.WriteNonNegativeSmallInt32( r.OId.Index );

                        w.DebugWriteSentinel();
                        w.WriteNonNegativeSmallInt32( _internalObjectCount );
                        var f = _firstInternalObject;
                        while( f != null )
                        {
                            w.WriteObject( f );
                            f = f.Next;
                        }
                        w.DebugWriteSentinel();
                        _timeManager.Save( monitor, w );
                        w.DebugWriteSentinel();
                        return true;
                    }
                }
                finally
                {
                    if( needFakeTran ) _currentTran.Dispose();
                    if( !isWrite && !isRead ) _lock.ExitReadLock();
                    Monitor.Exit( _saveLock );
                }
            }
        }

        void DoLoad( IActivityMonitor monitor, BinaryDeserializer r, string expectedName )
        {
            Debug.Assert( _lock.IsWriteLockHeld );
            _deserializing = true;
            try
            {
                int version = r.ReadSmallInt32();
                if( version < 0 || version > CurrentSerializationVersion )
                {
                    throw new InvalidDataException( $"Version must be between 0 and {CurrentSerializationVersion}. Version read: {version}." );
                }
                _currentObjectUniquifier = 0;
                if( version > 0 )
                {
                    if( version > 1 )
                    {
                        r.DebugReadMode();
                        _currentObjectUniquifier = r.ReadInt32();
                        _domainSecret = r.ReadBytes( DomainSecretKeyLength );
                    }
                    else
                    {
                        _domainSecret = CreateSecret();
                    }
                    var loaded = r.ReadString();
                    if( loaded != expectedName ) throw new InvalidDataException( $"Domain name mismatch: loading domain named '{loaded}' but expected '{expectedName}'." );
                }
                _transactionSerialNumber = r.ReadInt32();
                _actualObjectCount = r.ReadInt32();

                r.DebugCheckSentinel();
                _freeList.Clear();
                int count = r.ReadNonNegativeSmallInt32();
                while( --count >= 0 )
                {
                    _freeList.Add( r.ReadNonNegativeSmallInt32() );
                }

                r.DebugCheckSentinel();
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

                r.DebugCheckSentinel();
                #region Clearing exisiting objects, sizing _objects array.
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
                #endregion
                for( int i = 0; i < count; ++i )
                {
                    _objects[i] = (ObservableObject)r.ReadObject();
                }

                // Roots
                r.DebugCheckSentinel();
                _roots.Clear();
                count = r.ReadNonNegativeSmallInt32();
                while( --count >= 0 )
                {
                    _roots.Add( _objects[r.ReadNonNegativeSmallInt32()] as ObservableRootObject );
                }

                // Clears any internal objects.
                var internalObj = _firstInternalObject;
                while( internalObj != null )
                {
                    internalObj.OnDisposed( true );
                    internalObj = internalObj.Next;
                }
                _firstInternalObject = _lastInternalObject = null;

                // Clears any time event objects.
                _timeManager.Clear( monitor );

                if( version > 1 )
                {
                    // Reading InternalObjects.
                    r.DebugCheckSentinel();
                    count = r.ReadNonNegativeSmallInt32();
                    while( --count >= 0 )
                    {
                        r.ReadObject();
                    }

                    // Reading Timed events.
                    r.DebugCheckSentinel();
                    _timeManager.Load( monitor, r );
                }
                r.DebugCheckSentinel();
                r.ImplementationServices.ExecutePostDeserializationActions();
                OnLoaded();
                var next = _timeManager.ApplyChanges();
                if( next != Util.UtcMinValue ) _timeManager.SetNextDueTimeUtc( monitor, next );
            }
            finally
            {
                _deserializing = false;
            }
        }

        /// <summary>
        /// Loads previously <see cref="Save"/>d objects from a named domain into this domain: the <paramref name="expectedLoadedName"/> can be
        /// this <see cref="DomainName"/> or another name.
        /// </summary>
        /// <param name="monitor">The monitor to use. Cannot be null.</param>
        /// <param name="stream">The input stream.</param>
        /// <param name="expectedLoadedName">
        /// Name of the domain that is saved in <paramref name="stream"/> and must be loaded. It can differ from this <see cref="DomainName"/>.
        /// Must not be null or empty.
        /// </param>
        /// <param name="leaveOpen">True to leave the stream opened.</param>
        /// <param name="encoding">Optional encoding for characters. Defaults to UTF-8.</param>
        /// <param name="millisecondsTimeout">
        /// The maximum number of milliseconds to wait for a read access before giving up.
        /// Wait indefinitely by default.
        /// </param>
        /// <returns>True on success, false if timeout occurred.</returns>
        public bool Load( IActivityMonitor monitor, Stream stream, string expectedLoadedName, bool leaveOpen = false, Encoding encoding = null, int millisecondsTimeout = -1 )
        {
            if( monitor == null ) throw new ArgumentNullException( nameof( monitor ) );
            if( stream == null ) throw new ArgumentNullException( nameof( stream ) );
            if( String.IsNullOrEmpty( expectedLoadedName ) ) throw new ArgumentNullException( nameof( expectedLoadedName ) );
            if( _disposed ) throw new ObjectDisposedException( ToString() );
            bool isWrite = _lock.IsWriteLockHeld;
            if( !isWrite && !_lock.TryEnterWriteLock( millisecondsTimeout ) ) return false;
            Debug.Assert( !isWrite || _currentTran != null, "isWrite => _currentTran != null" );
            bool needFakeTran = _currentTran == null || _currentTran.Monitor != monitor;
            if( needFakeTran ) new InitializationTransaction( monitor, this, false );
            try
            {
                using( monitor.OpenInfo( $"Transacted loading domain." ) )
                using( var d = new BinaryDeserializer( stream, null, _deserializers, leaveOpen, encoding ) )
                {
                    d.Services.Add( this );
                    DoLoad( monitor, d, expectedLoadedName );
                    return true;
                }
            }
            finally
            {
                if( needFakeTran ) _currentTran.Dispose();
                if( !isWrite ) _lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Loads previously <see cref="Save"/>d objects into this domain.
        /// </summary>
        /// <param name="monitor">The monitor to use. Cannot be null.</param>
        /// <param name="stream">The input stream.</param>
        /// <param name="leaveOpen">True to leave the stream opened.</param>
        /// <param name="encoding">Optional encoding for characters. Defaults to UTF-8.</param>
        /// <param name="millisecondsTimeout">
        /// The maximum number of milliseconds to wait for a read access before giving up.
        /// Wait indefinitely by default.
        /// </param>
        /// <returns>True on success, false if timeout occurred.</returns>
        public bool Load( IActivityMonitor monitor, Stream stream, bool leaveOpen = false, Encoding encoding = null, int millisecondsTimeout = -1 )
        {
            return Load( monitor, stream, DomainName, leaveOpen, encoding, millisecondsTimeout );
        }

        /// <summary>
        /// Called after a <see cref="G:Load"/>.
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

        internal void Register( InternalObject o )
        {
            Debug.Assert( o != null && o.Domain == this && o.Prev == null && o.Next == null );
            CheckWriteLock( o );
            if( (o.Next = _firstInternalObject) == null ) _lastInternalObject = o;
            _firstInternalObject = o;
            ++_internalObjectCount;
        }

        internal void Unregister( InternalObject o )
        {
            Debug.Assert( o.Domain == this );
            if( _firstInternalObject == o ) _firstInternalObject = o.Next;
            else o.Prev.Next = o.Next;
            if( _lastInternalObject == o ) _lastInternalObject = o.Prev;
            else o.Next.Prev = o.Prev;
            --_internalObjectCount;
        }

        internal ObservableObjectId CreateId( int idx ) => new ObservableObjectId( idx, ObservableObjectId.ForwardUniquifier( ref _currentObjectUniquifier ) );

        internal ObservableObjectId Register( ObservableObject o )
        {
            Debug.Assert( o != null && o.Domain == this );
            CheckWriteLock( o );
            int idx;
            if( _freeList.Count > 0 )
            {
                idx = _freeList[_freeList.Count - 1];
                _freeList.RemoveAt( _freeList.Count - 1 );
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

            var id = CreateId( idx );
            if( !_deserializing )
            {
                _changeTracker.OnNewObject( o, id, o._exporter );
            }
            ++_actualObjectCount;
            return id;
        }

        internal void CheckBeforeDispose( IDisposableObject o )
        {
            Debug.Assert( !o.IsDisposed );
            CheckWriteLock( o ).CheckDisposed();
        }

        internal void Unregister( ObservableObject o )
        {
            if( !_deserializing ) _changeTracker.OnDisposeObject( o );
            _objects[o.OId.Index] = null;
            _freeList.Add( o.OId.Index );
            --_actualObjectCount;
        }

        /// <summary>
        /// Gets the <see cref="Observable.TimeManager"/> that is in charge of <see cref="ObservableReminder"/>
        /// and <see cref="ObservableTimer"/> objects.
        /// </summary>
        public TimeManager TimeManager => _timeManager;

        /// <summary>
        /// Obtains a monitor that is bound to this domain (it must be disposed once done with it).
        /// This implementation creates a new dedicated <see cref="IDisposableActivityMonitor"/> once and caches it
        /// or returns a new dedicated one if the shared one cannot be obtained before <paramref name="milliSecondTimeout"/>.
        /// <para>
        /// This monitor should be used only from contexts where no existing monitor exists: the <see cref="TimeManager"/> uses
        /// it when executing <see cref="ObservableTimedEventBase{TEventArgs}.Elapsed"/> events.
        /// </para>
        /// </summary>
        /// <returns>The cached monitor bound to this timer or null if <paramref name="createAutonomousOnTimeout"/> was false and the monitor has not been obtained.</returns>
        public IDisposableActivityMonitor ObtainDomainMonitor( int milliSecondTimeout = LockedDomainMonitorTimeout, bool createAutonomousOnTimeout = true )
        {
            if( !Monitor.TryEnter( _domainMonitorLock, milliSecondTimeout ) )
            {
                return createAutonomousOnTimeout ? new DomainActivityMonitor( $"Autonomous monitor for observable domain '{DomainName}'.", null, milliSecondTimeout ) : null;
            }
            if( _domainMonitor == null )
            {
                _domainMonitor = new DomainActivityMonitor( $"Observable Domain '{DomainName}'.", this, milliSecondTimeout );
            }
            return _domainMonitor;
        }

        /// <summary>
        /// 
        /// </summary>
        public void Dispose()
        {
            if( !_disposed )
            {
                _lock.EnterWriteLock();
                if( !_disposed )
                {
                    _disposed = true;
                    if( _domainMonitor != null )
                    {
                        if( Monitor.TryEnter( _domainMonitorLock, 0 ) )
                        {
                            _domainMonitor.MonitorEnd( "Domain disposed." );
                            Monitor.Exit( _domainMonitorLock );
                        }
                    }
                    _timeManager.CurrentTimer?.Dispose();
                    _lock.ExitWriteLock();
                    _lock.Dispose();
                }
            }
        }

        internal void SendCommand( IDisposableObject o, object command )
        {
            CheckWriteLock( o ).CheckDisposed();
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
            CheckWriteLock( o ).CheckDisposed();
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
            CheckWriteLock( o ).CheckDisposed();
            return _changeTracker.OnListRemoveAt( o, index );
        }

        internal ListSetAtEvent OnListSetAt( ObservableObject o, int index, object value )
        {
            if( _deserializing ) return null;
            CheckWriteLock( o ).CheckDisposed();
            return _changeTracker.OnListSetAt( o, index, value );
        }

        internal CollectionClearEvent OnCollectionClear( ObservableObject o )
        {
            if( _deserializing ) return null;
            CheckWriteLock( o ).CheckDisposed();
            return _changeTracker.OnCollectionClear( o );
        }

        internal ListInsertEvent OnListInsert( ObservableObject o, int index, object item )
        {
            if( _deserializing ) return null;
            CheckWriteLock( o ).CheckDisposed();
            return _changeTracker.OnListInsert( o, index, item );
        }

        internal CollectionMapSetEvent OnCollectionMapSet( ObservableObject o, object key, object value )
        {
            if( _deserializing ) return null;
            CheckWriteLock( o ).CheckDisposed();
            return _changeTracker.OnCollectionMapSet( o, key, value );
        }

        internal CollectionRemoveKeyEvent OnCollectionRemoveKey( ObservableObject o, object key )
        {
            if( _deserializing ) return null;
            CheckWriteLock( o ).CheckDisposed();
            return _changeTracker.OnCollectionRemoveKey( o, key );
        }

        IDisposableObject CheckWriteLock( IDisposableObject o )
        {
            if( !_lock.IsWriteLockHeld )
            {
                if( _currentTran == null ) throw new InvalidOperationException( "A transaction is required." );
                if( _lock.IsReadLockHeld ) throw new InvalidOperationException( "Concurrent access: only Read lock has been acquired." );
                throw new InvalidOperationException( "Concurrent access: no lock has been acquired." );
            }
            return o;
        }

        /// <summary>
        /// Small helper for tests: ensures that a domain that is Saved, Loaded and Saved again results
        /// in the exact same array of bytes.
        /// This throws an exception if serialized bytes differ or acquiring locks failed.
        /// </summary>
        /// <param name="monitor">Monitor to use. Cannot be null.</param>
        /// <param name="domain">The domain to check. Must not be null.</param>
        /// <param name="milliSecondsTimeout">Optional timeout to wait for read or write lock.</param>
        /// <param name="useDebugMode">False to not activate <see cref="BinarySerializer.IsDebugMode"/>.</param>
        public static void IdempotenceSerializationCheck( IActivityMonitor monitor, ObservableDomain domain, int milliSecondsTimeout = -1, bool useDebugMode = true )
        {
            using( var s = new MemoryStream() )
            {
                if( !domain.Save( monitor, s, true, millisecondsTimeout: milliSecondsTimeout, debugMode: useDebugMode ) ) throw new Exception( "First Save failed: Unable to acquire lock." );
                var originalBytes = s.ToArray();
                s.Position = 0;
                if( !domain.Load( monitor, s, true, millisecondsTimeout: milliSecondsTimeout ) ) throw new Exception( "Reload failed: Unable to acquire lock." );
                s.Position = 0;
                if( !domain.Save( monitor, s, true, millisecondsTimeout: milliSecondsTimeout, debugMode: useDebugMode ) ) throw new Exception( "Second Save failed: Unable to acquire lock." );
                var rewriteBytes = s.ToArray();
                if( !originalBytes.SequenceEqual( rewriteBytes ) )
                {
                    using( monitor.OpenError( "Reserialized bytes differ from original serialized bytes." ) )
                    {
                        if( useDebugMode )
                        {
                            monitor.Error( $"Original: ({originalBytes.LongLength}) {ByteArrayToString( originalBytes )}" );
                            monitor.Error( $"Reserialized: ({rewriteBytes.LongLength}) {ByteArrayToString( rewriteBytes )}" );
                        }
                        else
                        {
                            monitor.Error( $"Original: {originalBytes.LongLength} bytes" );
                            monitor.Error( $"Reserialized: {rewriteBytes.LongLength} bytes" );
                        }
                    }
                    throw new Exception( "Reserialized bytes differ from original serialized bytes." );
                }
            }
        }

        static string ByteArrayToString( byte[] ba )
        {
            StringBuilder hex = new StringBuilder( ba.Length * 2 );
            foreach( byte b in ba )
                hex.AppendFormat( "{0:x2}", b );
            return hex.ToString();
        }

    }
}
