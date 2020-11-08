using CK.Core;
using CK.Text;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Observable
{

    /// <summary>
    /// Base class for any observable domain without strongly typed root. This class should not be specialized:
    /// you must use specialized <see cref="ObservableChannel{T}"/> or <see cref="ObservableDomain{T1, T2, T3, T4}"/>
    /// for domains with strongly typed roots.
    /// </summary>
    /// <remarks>
    /// This object is marked with <see cref="NotExportableAttribute"/> just to be safe if a domain reference appears the public API of
    /// an <see cref="ObservableObject"/>. This should not happen since ObservableObject (and their non-exportable cousins like <see cref="InternalObject"/>
    /// or <see cref="ObservableTimer"/> and <see cref="ObservableReminder"/>) has access to a "light" <see cref="DomainView"/> through a
    /// protected property <see cref="ObservableObject.Domain"/>.
    /// </remarks>
    [NotExportable( Error = "No interaction with the ObservableDomain must be made from the observable objects." )]
    public class ObservableDomain : IObservableDomain, IDisposable
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
        public const int CurrentSerializationVersion = 5;

        /// <summary>
        /// The length in bytes of the <see cref="SecretKey"/>.
        /// </summary>
        public const int DomainSecretKeyLength = 512;

        [ThreadStatic]
        internal static ObservableDomain? CurrentThreadDomain;

        internal readonly IExporterResolver _exporters;
        readonly ISerializerResolver _serializers;
        readonly IDeserializerResolver _deserializers;
        readonly IPostActionContextMarshaller? _postActionMarshaller;
        readonly TimeManager _timeManager;
        readonly SidekickManager _sidekickManager;

        /// <summary>
        /// Maps property names to PropInfo that contains the property index.
        /// </summary>
        readonly Dictionary<string, ObservablePropertyChangedEventArgs> _properties;
        /// <summary>
        /// Map property index to PropInfo that contains the property name.
        /// </summary>
        readonly List<ObservablePropertyChangedEventArgs> _propertiesByIndex;

        readonly ChangeTracker _changeTracker;
        readonly AllCollection _exposedObjects;
        readonly ReaderWriterLockSlim _lock;
        readonly List<int> _freeList;
        byte[] _domainSecret;

        class InternalObjectCollection : IReadOnlyCollection<InternalObject>
        {
            readonly ObservableDomain _domain;

            public InternalObjectCollection( ObservableDomain d ) => _domain = d;

            public int Count => _domain._internalObjectCount;

            public IEnumerator<InternalObject> GetEnumerator()
            {
                var o = _domain._firstInternalObject;
                while( o != null )
                {
                    yield return o;
                    o = o.Next;
                }
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        readonly InternalObjectCollection _exposedInternalObjects;
        int _internalObjectCount;
        InternalObject? _firstInternalObject;
        InternalObject? _lastInternalObject;

        ObservableObject?[] _objects;
        /// There are few tracker objects and they typically have a long
        /// lifetime (as they're often roots for specific objects like sensors).
        readonly List<IObservableDomainActionTracker> _trackers;

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

        IObservableTransaction? _currentTran;
        int _transactionSerialNumber;
        DateTime _transactionCommitTimeUtc;

        // Available to objects.
        internal readonly ObservableDomainEventArgs DefaultEventArgs;

        // A reusable domain monitor is created on-demand and is protected by an exclusive lock.
        DomainActivityMonitor _domainMonitor;
        readonly object _domainMonitorLock;

        // This lock is used to allow one and only one Save at a time: this is to protect
        // the potential fake transaction that is used when saving.
        readonly object _saveLock;

        bool _deserializeOrInitializing;
        bool _disposed;

        /// <summary>
        /// Exposes the non null objects in _objects as a collection.
        /// </summary>
        class AllCollection : IObservableAllObjectsCollection
        {
            readonly ObservableDomain _d;

            public AllCollection( ObservableDomain d )
            {
                _d = d;
            }

            public ObservableObject? this[long id] => this[new ObservableObjectId( id, false )];

            public ObservableObject? this[ObservableObjectId id]
            {
                get
                {
                    if( id.IsValid )
                    {
                        int idx = id.Index;
                        if( idx < _d._objectsListCount )
                        {
                            var o = _d._objects[idx];
                            if( o != null && o.OId == id ) return o;
                        }
                    }
                    return null;
                }
            }

            public int Count => _d._actualObjectCount;

            public T? Get<T>( ObservableObjectId id, bool throwOnTypeMismacth = true ) where T : ObservableObject
            {
                var o = this[id];
                if( o == null ) return null;
                return throwOnTypeMismacth ? (T)o : o as T;
            }

            public T? Get<T>( long id, bool throwOnTypeMismacth = true ) where T : ObservableObject => Get<T>( new ObservableObjectId( id, false ) );

            public IEnumerator<ObservableObject> GetEnumerator() => _d._objects.Take( _d._objectsListCount )
                                                                               .Where( o => o != null )
                                                                               .GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        /// <summary>
        /// The change tracker handles the transfomation of actual changes into events that are
        /// optimized and serialized by the <see cref="Commit(ObservableDomain, Func{string, ObservablePropertyChangedEventArgs}, DateTime, DateTime)"/> method.
        /// </summary>
        class ChangeTracker
        {
            class PropChanged
            {
                public readonly ObservableObject Object;
                public readonly ObservablePropertyChangedEventArgs Info;
                public object FinalValue;

                public long Key => Info.GetObjectPropertyId( Object );

                public PropChanged( ObservableObject o, ObservablePropertyChangedEventArgs p, object final )
                {
                    Object = o;
                    Info = p;
                    FinalValue = final;
                }
            }

            readonly List<ObservableEvent> _changeEvents;
            readonly Dictionary<ObservableObject, List<PropertyInfo>?> _newObjects;
            readonly Dictionary<long, PropChanged> _propChanged;
            // A new list is allocated each time since commands can be appended to it after the commit, during the
            // OnSuccessfulTransaction raising.
            List<object> _commands;

            public ChangeTracker()
            {
                _changeEvents = new List<ObservableEvent>();
                _newObjects = new Dictionary<ObservableObject, List<PropertyInfo>?>( PureObjectRefEqualityComparer<ObservableObject>.Default );
                _propChanged = new Dictionary<long, PropChanged>();
                _commands = new List<object>();
            }

            public SuccessfulTransactionEventArgs Commit( ObservableDomain domain, Func<string, ObservablePropertyChangedEventArgs> ensurePropertInfo, DateTime startTime, DateTime nextTimerDueDate )
            {
                _changeEvents.RemoveAll( e => e is ICollectionEvent c && c.Object.IsDisposed );
                foreach( var p in _propChanged.Values )
                {
                    if( !p.Object.IsDisposed )
                    {
                        _changeEvents.Add( new PropertyChangedEvent( p.Object, p.Info.PropertyId, p.Info.PropertyName, p.FinalValue ) );
                        if( _newObjects.TryGetValue( p.Object, out var exportables ) )
                        {
                            Debug.Assert( exportables != null, "If the object is not exportable, there must be no property changed events." );
                            int idx = exportables.IndexOf( exp => exp.Name == p.Info.PropertyName );
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
                        _changeEvents.Add( new PropertyChangedEvent( kv.Key, pInfo.PropertyId, pInfo.PropertyName, propValue ) );
                    }
                }
                var result = new SuccessfulTransactionEventArgs( domain, domain.FindPropertyId, _changeEvents.ToArray(), _commands, startTime, nextTimerDueDate );
                Reset();
                return result;
            }

            /// <summary>
            /// Clears all events collected so far from the 3 internal lists and allocates a new emty command list for the next transaction.
            /// </summary>
            public void Reset()
            {
                _changeEvents.Clear();
                _newObjects.Clear();
                _propChanged.Clear();
                _commands = new List<object>();
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
            /// <param name="o">The object itself.</param>
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

            internal void OnNewProperty( ObservablePropertyChangedEventArgs info )
            {
                _changeEvents.Add( new NewPropertyEvent( info.PropertyId, info.PropertyName ) );
            }

            internal void OnPropertyChanged( ObservableObject o, ObservablePropertyChangedEventArgs p, object after )
            {
                PropChanged c;
                if( _propChanged.TryGetValue( p.GetObjectPropertyId( o ), out c ) )
                {
                    c.FinalValue = after;
                }
                else
                {
                    c = new PropChanged( o, p, after );
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

            internal CollectionAddKeyEvent OnCollectionAddKey( ObservableObject o, object key )
            {
                var e = new CollectionAddKeyEvent( o, key );
                _changeEvents.Add( e );
                return e;
            }

            internal void OnSendCommand( in object command )
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

                Monitor.Debug( "Starting Commit." );
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
                SuccessfulTransactionEventArgs? ctx = null;
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
                        _result.SetClientError( ex );
                    }
                }
                else
                {
                    ctx = _domain._changeTracker.Commit( _domain, _domain.EnsurePropertyInfo, _startTime, nextTimerDueDate );
                    ++_domain._transactionSerialNumber;
                    _domain._transactionCommitTimeUtc = ctx.CommitTimeUtc;
                    try
                    {
                        _result = new TransactionResult( ctx, _domain._postActionMarshaller );
                        _domain.DomainClient?.OnTransactionCommit( ctx );
                    }
                    catch( Exception ex )
                    {
                        Monitor.Fatal( "Error in IObservableTransactionManager.OnTransactionCommit. This is a Critical error since the Domain state integrity may be compromised.", ex );
                        _result.SetClientError( ex );
                        ctx = null;
                    }
                }
                var next = _result.NextDueTimeUtc;
                if( next != Util.UtcMinValue ) _domain._timeManager.SetNextDueTimeUtc( Monitor, next );
                _monitorGroup.Dispose();
                _domain._currentTran = null;

                _domain._lock.ExitWriteLock();
                // Back to Readeable lock: publishes SuccessfulTransaction.
                if( _result.Success )
                {
                    Debug.Assert( ctx != null );
                    
                    var errors = _domain.RaiseOnSuccessfulTransaction( ctx );
                    if( errors != null ) _result.SetSuccessfulTransactionErrors( errors );
                }
                _domain._lock.ExitUpgradeableReadLock();
                // Outside of the lock: on success, the sidekicks execute the Command objects.
                if( _result.Success )
                {
                    var errors = _domain._sidekickManager.ExecuteCommands( Monitor, _result, _result._postActions );
                    if( errors != null ) _result.SetCommandHandlingErrors( errors );
                }
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
        /// <param name="monitor">The monitor used to log the construction of this domain. Cannot be null.</param>
        /// <param name="domainName">Name of the domain. Must not be null but can be empty.</param>
        /// <param name="serviceProvider">The service providers that will be used to resolve the <see cref="ObservableDomainSidekick"/> objects.</param>
        public ObservableDomain( IActivityMonitor monitor, string domainName, IServiceProvider? serviceProvider = null )
            : this( monitor, domainName, null, serviceProvider )
        {
        }

        /// <summary>
        /// Initializes a new <see cref="ObservableDomain"/> with a <see cref="DomainClient"/> an optionals explicit exporter, serializer
        /// and deserializer handlers.
        /// </summary>
        /// <param name="monitor">The monitor used to log the construction of this domain. Cannot be null.</param>
        /// <param name="domainName">Name of the domain. Must not be null but can be empty.</param>
        /// <param name="client">The observable client (head of the Chain of Responsibility) to use. Can be null.</param>
        /// <param name="serviceProvider">The service providers that will be used to resolve the <see cref="ObservableDomainSidekick"/> objects.</param>
        /// <param name="postActionsMarshaller">Optional marshaller for post actions execution.</param>
        public ObservableDomain( IActivityMonitor monitor,
                                 string domainName,
                                 IObservableDomainClient? client,
                                 IServiceProvider? serviceProvider = null,
                                 IPostActionContextMarshaller? postActionsMarshaller = null )
            : this( monitor, domainName, client, true, serviceProvider, postActionsMarshaller: postActionsMarshaller, exporters: null, serializers: null, deserializers: null )
        {
        }

        /// <summary>
        /// Initializes a previously <see cref="Save"/>d domain.
        /// </summary>
        /// <param name="monitor">The monitor used to log the construction of this domain. Cannot be null.</param>
        /// <param name="domainName">Name of the domain. Must not be null but can be empty.</param>
        /// <param name="client">The observable client (head of the Chain of Responsibility) to use. Can be null.</param>
        /// <param name="s">The input stream.</param>
        /// <param name="leaveOpen">True to leave the stream opened.</param>
        /// <param name="encoding">Optional encoding for characters. Defaults to UTF-8.</param>
        /// <param name="serviceProvider">The service providers that will be used to resolve the <see cref="ObservableDomainSidekick"/> objects.</param>
        /// <param name="loadHook">The load hook to apply. See loadHook parameter of <see cref="Load(IActivityMonitor, Stream, bool, Encoding?, int, Func{ObservableDomain, bool}?)"/>.</param>
        /// <param name="postActionsMarshaller">Optional marshaller for post actions execution.</param>
        /// <param name="exporters">Optional exporters handler.</param>
        /// <param name="serializers">Optional serializers handler.</param>
        /// <param name="deserializers">Optional deserializers handler.</param>
        public ObservableDomain( IActivityMonitor monitor,
                                 string domainName,
                                 IObservableDomainClient? client,
                                 Stream s,
                                 bool leaveOpen = false,
                                 Encoding? encoding = null,
                                 IServiceProvider? serviceProvider = null,
                                 Func<ObservableDomain, bool>? loadHook = null,
                                 IPostActionContextMarshaller? postActionsMarshaller = null,
                                 IExporterResolver? exporters = null,
                                 ISerializerResolver? serializers = null,
                                 IDeserializerResolver? deserializers = null )
            : this( monitor, domainName, client, false, serviceProvider, postActionsMarshaller, exporters, serializers, deserializers )
        {
            Load( monitor, s, leaveOpen, encoding, loadHook: loadHook );
            client?.OnDomainCreated( monitor, this );
        }

        ObservableDomain( IActivityMonitor monitor,
                          string domainName,
                          IObservableDomainClient? client,
                          bool callClientOnCreate,
                          IServiceProvider? serviceProvider,
                          IPostActionContextMarshaller? postActionsMarshaller,
                          IExporterResolver? exporters,
                          ISerializerResolver? serializers,
                          IDeserializerResolver? deserializers )
        {
            if( monitor == null ) throw new ArgumentNullException( nameof( monitor ) );
            DomainName = domainName ?? throw new ArgumentNullException( nameof( domainName ) );
            _postActionMarshaller = postActionsMarshaller;
            _exporters = exporters ?? ExporterRegistry.Default;
            _serializers = serializers ?? SerializerRegistry.Default;
            _deserializers = deserializers ?? DeserializerRegistry.Default;
            DomainClient = client;
            _objects = new ObservableObject?[512];
            _freeList = new List<int>();
            _properties = new Dictionary<string, ObservablePropertyChangedEventArgs>();
            _propertiesByIndex = new List<ObservablePropertyChangedEventArgs>();
            _changeTracker = new ChangeTracker();
            _exposedObjects = new AllCollection( this );
            _exposedInternalObjects = new InternalObjectCollection( this );
            _roots = new List<ObservableRootObject>();
            _trackers = new List<IObservableDomainActionTracker>();
            _timeManager = new TimeManager( this );
            _sidekickManager = new SidekickManager( this, serviceProvider ?? EmptyServiceProvider.Default );
            _transactionCommitTimeUtc = DateTime.UtcNow;
            DefaultEventArgs = new ObservableDomainEventArgs( this );
            // LockRecursionPolicy.NoRecursion: reentrancy must NOT be allowed.
            _lock = new ReaderWriterLockSlim( LockRecursionPolicy.NoRecursion );
            _saveLock = new Object();
            _domainMonitorLock = new Object();

            if( callClientOnCreate )
            {
                client?.OnDomainCreated( monitor, this );
                // If the secret has not been restored, initializes a new one.
                if( _domainSecret == null ) _domainSecret = CreateSecret();
            }
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
                m.OpenDebug( $"Opening new InitializationTransaction on '{d.DomainName}'." );
                _monitor = m;
                _startTime = DateTime.UtcNow;
                _d = d;
                if( _enterWriteLock = enterWriteLock ) d._lock.EnterWriteLock();
                _previousTran = d._currentTran;
                d._currentTran = this;
                _previousThreadDomain = CurrentThreadDomain;
                CurrentThreadDomain = d;
                d._deserializeOrInitializing = true;
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
                _monitor.CloseGroup();
                _d._deserializeOrInitializing = false;
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
        /// <param name="initializationContext">
        /// This is not directly used, this is just to express the fact that a InitializationTransaction must be acquired
        /// before adding roots (root constructors must execute in the context of this fake transaction).
        /// </param>
        /// <returns>The instance.</returns>
        private protected T AddRoot<T>( InitializationTransaction initializationContext ) where T : ObservableRootObject
        {
            var o = Activator.CreateInstance<T>();
            _roots.Add( o );
            return o;
        }

        /// <summary>
        /// Gets all the observable objects that this domain contains (roots included).
        /// These exposed objects are out of any transactions or reentrancy checks: they should not 
        /// be used outside of <see cref="BeginTransaction"/> (or other <see cref="Modify"/>, <see cref="ModifyAsync"/> methods)
        /// or <see cref="AcquireReadLock"/> scopes.
        /// </summary>
        public IObservableAllObjectsCollection AllObjects => _exposedObjects;

        /// <summary>
        /// Gets all the internal objects that this domain contains.
        /// These exposed objects are out of any transactions or reentrancy checks: they should not 
        /// be used outside of <see cref="BeginTransaction"/> (or other <see cref="Modify"/>, <see cref="ModifyAsync"/> methods)
        /// or <see cref="AcquireReadLock"/> scopes.
        /// </summary>
        public IReadOnlyCollection<InternalObject> AllInternalObjects => _exposedInternalObjects;

        /// <summary>
        /// Gets the root observable objects that this domain contains.
        /// These exposed objects are out of any transactions or reentrancy checks: they should not 
        /// be used outside of <see cref="BeginTransaction"/> (or other <see cref="Modify"/>, <see cref="ModifyAsync"/> methods)
        /// or <see cref="AcquireReadLock"/> scopes.
        /// </summary>
        public IReadOnlyList<ObservableRootObject> AllRoots => _roots;

        /// <summary>
        /// Gets the current transaction number.
        /// Incremented each time a transaction successfuly ended, default to 0 until the first transaction commit.
        /// </summary>
        public int TransactionSerialNumber => _transactionSerialNumber;

        /// <summary>
        /// Gets the last commit time. Defaults to <see cref="DateTime.UtcNow"/> at the very beginning,
        /// when no transaction has been comitted yet (and <see cref="TransactionSerialNumber"/> is 0).
        /// </summary>
        public DateTime TransactionCommitTimeUtc => _transactionCommitTimeUtc;

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
        /// Gets whether this domain has been disposed.
        /// </summary>
        public bool IsDisposed => _disposed;

        class DomainActivityMonitor : ActivityMonitor, IDisposableActivityMonitor
        {
            readonly ObservableDomain? _domain;

            public DomainActivityMonitor( string topic, ObservableDomain? domain, int timeout )
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
        internal IActivityMonitor CurrentMonitor
        {
            get
            {
                Debug.Assert( _currentTran != null );
                return _currentTran.Monitor;
            }
        }

        /// <summary>
        /// Gets the associated client (head of the Chain of Responsibility).
        /// </summary>
        public IObservableDomainClient? DomainClient { get; private set; }

        /// <summary>
        /// Raised whenever when a successful transaction has been successfully handled by the <see cref="ObservableDomain.DomainClient"/>.
        /// <para>
        /// When this is called, the <see cref="Domain"/>'s lock is held in read mode: objects can be read (but no write/modifications
        /// should occur).
        /// </para>
        /// <para>
        /// Exceptions raised by this method are collected in <see cref="TransactionResult.SuccessfulTransactionErrors"/>.
        /// </para>
        /// </summary>
        public event EventHandler<SuccessfulTransactionEventArgs> OnSuccessfulTransaction;

        List<CKExceptionData>? RaiseOnSuccessfulTransaction( in SuccessfulTransactionEventArgs result )
        {
            List<CKExceptionData>? errors = null;
            var h = OnSuccessfulTransaction;
            if( h != null )
            {
                foreach( var d in h.GetInvocationList() )
                {
                    try
                    {
                        ((EventHandler<SuccessfulTransactionEventArgs>)d).Invoke( this, result );
                    }
                    catch( Exception ex )
                    {
                        result.Monitor.Error( "Error while raising OnSuccessfulTransaction event.", ex );
                        if( errors == null ) errors = new List<CKExceptionData>();
                        errors.Add( CKExceptionData.CreateFrom( ex ) );
                    }
                }
            }
            _sidekickManager.OnSuccessfulTransaction( result, ref errors );
            return errors;
        }

        /// <summary>
        /// <para>
        /// Acquires a single-threaded read lock on this <see cref="ObservableDomain"/>:
        /// until the returned disposable is disposed, objects can safely be read, and any attempt
        /// to call <see cref="BeginTransaction"/> from other threads will be blocked.
        /// This immediately returns null if this domain is disposed.
        /// </para>
        /// <para>
        /// Changing threads (eg. by awaiting tasks) before the returned disposable is disposed
        /// will throw a <see cref="SynchronizationLockException"/>.
        /// </para>
        /// <para>
        /// Any attempt to call <see cref="BeginTransaction"/> from this thread will throw a <see cref="LockRecursionException"/>.
        /// </para>
        /// </summary>
        /// <param name="millisecondsTimeout">
        /// The maximum number of milliseconds to wait for a read access before giving up.
        /// Wait indefinitely by default.
        /// </param>
        /// <exception cref="LockRecursionException">
        /// When <see cref="BeginTransaction"/> is being called from the same thread inside the read lock.
        /// </exception>
        /// <exception cref="SynchronizationLockException">
        /// When the current thread has not entered the lock in read mode.
        /// Can be caused by other threads trying to use this lock (eg. after awaiting a task).
        /// </exception>
        /// <returns>A disposable that releases the read lock when disposed, or null if a timeout occurred (or this is disposed).</returns>
        public IDisposable? AcquireReadLock( int millisecondsTimeout = -1 )
        {
            CheckDisposed();
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
        /// The maximum number of milliseconds to wait for a write access before giving up.
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
        public IObservableTransaction? BeginTransaction( IActivityMonitor monitor, int millisecondsTimeout = -1 )
        {
            if( monitor == null ) throw new ArgumentNullException( nameof( monitor ) );
            CheckDisposed();
            if( !TryEnterUpgradeableReadAndWriteLockAtOnce( millisecondsTimeout ) )
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
                // This could throw and be handled just like other pre-transaction errors (when a buggy client throws during OnTransactionStart).
                // Depending on throwException parameter, it will be rethrown or returned (returning the exception is for MofifyNoThrow).
                // See DoDispose method for the discussion about disposal...
                CheckDisposed();
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
        public TransactionResult Modify( IActivityMonitor monitor, Action? actions, int millisecondsTimeout = -1 )
        {
            using( var t = BeginTransaction( monitor, millisecondsTimeout ) )
            {
                if( t == null ) return TransactionResult.Empty;
                return DoModifyAndCommit( actions, t );
            }
        }

        /// <summary>
        /// Modify the domain once a transaction has been opened and calls the <see cref="IObservableDomainClient"/>
        /// that have been registered: all this occurs in the lock and it is released at the end.
        /// This never throws since the transaction result contains any errors.
        /// </summary>
        /// <param name="actions">The actions to execute. Can be null.</param>
        /// <param name="t">The observable transaction. Cannot be null.</param>
        /// <returns>The transaction result. Will never be null.</returns>
        TransactionResult DoModifyAndCommit( Action? actions, IObservableTransaction t )
        {
            Debug.Assert( t != null );
            try
            {
                _timeManager.RaiseElapsedEvent( t.Monitor, t.StartTime, false );
                bool skipped = false;
                foreach( var tracker in _trackers )
                {
                    if( !tracker.BeforeModify( t.Monitor, t.StartTime ) )
                    {
                        skipped = true;
                        break;
                    }
                }
                if( !skipped && actions != null )
                {
                    actions();
                    if( _sidekickManager.CreateWaitingSidekicks( t.Monitor, ex => t.AddError( CKExceptionData.CreateFrom( ex ) ) ) )
                    {
                        var now = DateTime.UtcNow;
                        foreach( var tracker in _trackers ) tracker.AfterModify( t.Monitor, t.StartTime, now - t.StartTime );
                        _timeManager.RaiseElapsedEvent( t.Monitor, now, true );
                    }
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
        /// Modifies this ObservableDomain and executes the <see cref="SuccessfulTransactionEventArgs.PostActions"/>.
        /// Any exceptions raised by <see cref="IObservableDomainClient.OnTransactionStart(IActivityMonitor,ObservableDomain, DateTime)"/> (at the start of the process)
        /// and by any post actions (after the successful commit or failed transaction) are thrown by this method.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="actions">
        /// The actions to execute inside the ObservableDomain's current transaction.
        /// Can be null: only pending timed events are executed if any.
        /// </param>
        /// <param name="millisecondsTimeout">
        /// The maximum number of milliseconds to wait for a write access before giving up.
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
        /// Same as <see cref="ModifyAsync(IActivityMonitor, Action, int)"/> but calls <see cref="TransactionResult.ThrowOnTransactionFailure()"/>:
        /// this methods always throw on any error.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="actions">
        /// The actions to execute inside the ObservableDomain's current transaction.
        /// Can be null: only pending timed events are executed if any.
        /// </param>
        /// <param name="millisecondsTimeout">
        /// The maximum number of milliseconds to wait for a write access before giving up.
        /// Wait indefinitely by default.
        /// </param>
        public async Task ModifyThrowAsync( IActivityMonitor monitor, Action actions, int millisecondsTimeout = -1 )
        {
            (await ModifyAsync( monitor, actions, millisecondsTimeout )).ThrowOnTransactionFailure();
        }

        /// <summary>
        /// Safe version of <see cref="ModifyAsync(IActivityMonitor, Action, int)"/> that will never throw: any exception raised
        /// by <see cref="IObservableDomainClient.OnTransactionStart(IActivityMonitor, ObservableDomain, DateTime)"/>
        /// or <see cref="TransactionResult.ExecutePostActionsAsync(IActivityMonitor, bool)"/> is logged and returned along with the
        /// transaction result itself.
        /// <para>
        /// If this method can, of course, be called from the application code, it has been designed to be called from background threads,
        /// typically from the <see cref="TimeManager.AutoTimer"/>.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="actions">
        /// The actions to execute inside the ObservableDomain's current transaction.
        /// Can be null: only pending timed events are executed if any.
        /// </param>
        /// <param name="millisecondsTimeout">
        /// The maximum number of milliseconds to wait for a write access before giving up.
        /// Wait indefinitely by default.
        /// </param>
        /// <returns>
        /// Returns the transaction result (that may be <see cref="TransactionResult.Empty"/>) and any exception outside of the observable transaction itself.
        /// </returns>
        public async Task<(TransactionResult, Exception)> ModifyNoThrowAsync( IActivityMonitor monitor, Action actions, int millisecondsTimeout = -1 )
        {
            if( monitor == null ) throw new ArgumentNullException( nameof( monitor ) );
            CheckDisposed();
            TransactionResult tr = TransactionResult.Empty;
            Exception postActionError = null;
            if( TryEnterUpgradeableReadAndWriteLockAtOnce( millisecondsTimeout ) )
            {
                var tEx = DoBeginTransaction( monitor, false );
                Debug.Assert( (tEx.Item1 != null) != (tEx.Item2 != null), "The IObservableTransaction XOR IObservableDomainClient.OnTransactionStart() exception." );
                if( tEx.Item2 != null ) return (tr, tEx.Item2);
                tr = DoModifyAndCommit( actions, tEx.Item1 );
                Debug.Assert( tr.Errors.Count == 0 || !tr.HasPostActions, "Transaction Errors => No post actions." );
                postActionError = await tr.ExecutePostActionsAsync( monitor, throwException: false );
            }
            else monitor.Warn( $"WriteLock not obtained in {millisecondsTimeout} ms (returning TransactionResult.Empty)." );
            return (tr, postActionError );
        }

        bool TryEnterUpgradeableReadAndWriteLockAtOnce( int millisecondsTimeout )
        {
            var start = DateTime.UtcNow;
            if( _lock.TryEnterUpgradeableReadLock( millisecondsTimeout ) )
            {
                if( millisecondsTimeout > 0 )
                {
                    millisecondsTimeout -= ((int)(DateTime.UtcNow.Ticks - start.Ticks) / (int)TimeSpan.TicksPerMillisecond);
                    if( millisecondsTimeout < 0 ) millisecondsTimeout = 0;
                }
                if( _lock.TryEnterWriteLock( millisecondsTimeout ) )
                {
                    return true;
                }
                _lock.ExitUpgradeableReadLock();
            }
            return false;
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
            CheckDisposed();
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
                    target.EmitString( p.Value.PropertyName );
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
        public string? ExportToString( int millisecondsTimeout = -1 )
        {
            var w = new StringWriter();
            return Export( w, millisecondsTimeout ) ? w.ToString() : null;
        }

        /// <inheritdoc/>
        public bool Save( IActivityMonitor monitor,
                          Stream stream,
                          bool leaveOpen = false,
                          bool debugMode = false,
                          Encoding? encoding = null,
                          SaveDisposedObjectBehavior saveDisposed = SaveDisposedObjectBehavior.None,
                          int millisecondsTimeout = -1 )
        {
            if( monitor == null ) throw new ArgumentNullException( nameof( monitor ) );
            if( stream == null ) throw new ArgumentNullException( nameof( stream ) );
            CheckDisposed();

            // Since we only need the read lock, whenever multiple threads Save() concurrently,
            // the monitor (of the fake transaction) is at risk. This is why we secure the Save with its own lock: since
            // only one Save at a time can be executed and no other "read with a monitor (even in a fake transaction)" exists.
            // Since this is clearly an edge case, we use a lock with the same timeout and we don't care of a potential 2x wait time.
            if( !Monitor.TryEnter( _saveLock, millisecondsTimeout ) ) return false;

            int disposedObjectsCount = 0;
            List<(Type, int)>? disposedList = null;
            Action<IDisposableObject>? disposedTracker = null;
            if( saveDisposed != SaveDisposedObjectBehavior.None )
            {
                disposedList = new List<(Type, int)>();
                disposedTracker = o =>
                {
                    ++disposedObjectsCount;
                    var t = o.GetType();
                    int idx = disposedList.IndexOf( t => t.Item1 == o.GetType() );
                    if( idx >= 0 ) disposedList[idx] = (t, disposedList[idx].Item2 + 1);
                    else disposedList.Add( (t, 1) );
                };
            }

            using( var w = new BinarySerializer( stream, _serializers, leaveOpen, encoding, disposedTracker ) )
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
                    using( isWrite ? monitor.OpenInfo( $"Transacted saving domain ({_actualObjectCount} objects, {_internalObjectCount} internals, {_timeManager.AllObservableTimedEvents.Count} timed events)." ) : null )
                    {
                        // Version 2: supports DebugMode, TimeManager & Internal objects.
                        // Version 3: supports TransactionCommitTimeUtc.
                        w.WriteSmallInt32( CurrentSerializationVersion ); 
                        w.DebugWriteMode( debugMode ? (bool?)debugMode : null );
                        w.Write( _currentObjectUniquifier );
                        w.Write( _domainSecret );
                        if( debugMode ) monitor.Trace( $"Domain {DomainName}: Tran #{_transactionSerialNumber} - {_transactionCommitTimeUtc:o}, {_actualObjectCount} objects." );
                        w.Write( DomainName );
                        w.Write( _transactionSerialNumber );
                        w.Write( _transactionCommitTimeUtc );
                        w.Write( _actualObjectCount );

                        w.DebugWriteSentinel();
                        w.WriteNonNegativeSmallInt32( _freeList.Count );
                        foreach( var i in _freeList ) w.WriteNonNegativeSmallInt32( i );

                        w.DebugWriteSentinel();
                        w.WriteNonNegativeSmallInt32( _properties.Count );
                        foreach( var p in _propertiesByIndex )
                        {
                            w.Write( p.PropertyName );
                        }
                        w.DebugWriteSentinel();
                        Debug.Assert( _objectsListCount == _actualObjectCount + _freeList.Count );
                        for( int i = 0; i < _objectsListCount; ++i )
                        {
                            var o = _objects[i];
                            Debug.Assert( o == null || !o.IsDisposed, "Either it is a free cell (that appears in the free list) or the object is NOT disposed." );
                            w.WriteObject( o );
                        }

                        w.DebugWriteSentinel();
                        w.WriteNonNegativeSmallInt32( _roots.Count );
                        foreach( var r in _roots ) w.WriteNonNegativeSmallInt32( r.OId.Index );

                        w.DebugWriteSentinel();
                        w.WriteNonNegativeSmallInt32( _internalObjectCount );
                        var f = _firstInternalObject;
                        while( f != null )
                        {
                            Debug.Assert( !f.IsDisposed, "Disposed internal objects are removed from the list." );
                            w.WriteObject( f );
                            f = f.Next;
                        }
                        w.DebugWriteSentinel();
                        _timeManager.Save( monitor, w );
                        w.DebugWriteSentinel();
                        _sidekickManager.Save( w );
                        w.DebugWriteSentinel();

                        if( disposedList != null )
                        {
                            if( disposedObjectsCount == 0 ) monitor.Trace( "No disposed objects found." );
                            else
                            {
                                var message = $"Found {disposedObjectsCount} disposed objects: {disposedList.Select( t => $"{t.Item2} instances of '{t.Item1.Name}'" ).Concatenate()}.";
                                monitor.Log( saveDisposed == SaveDisposedObjectBehavior.LogWarning ? LogLevel.Warn : LogLevel.Error, message );
                                if( saveDisposed == SaveDisposedObjectBehavior.Throw )
                                {
                                    throw new CKException( message );
                                }
                            }
                        }
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

        void DoLoad( IActivityMonitor monitor, BinaryDeserializer r, string expectedName, Func<ObservableDomain, bool>? loadHook )
        {
            Debug.Assert( _lock.IsWriteLockHeld );
            _deserializeOrInitializing = true;
            try
            {
                #region Unload/Dispose existing objects.
                // Call Dispose(false) on all objects.
                for( int i = 0; i < _objectsListCount; ++i )
                {
                    var o = _objects[i];
                    if( o != null )
                    {
                        Debug.Assert( !o.IsDisposed );
                        // This may still call Dispose() on other objects.
                        // Disposing() an ObservableObject will call InternalUnregister() here,
                        // and may affect the counts and object/free lists during loading.
                        // At least, with false, the Disposed event is not called.
                        o.Dispose( shouldCleanup: false );
                    }
                }
                // Empty _objects completely.
                Array.Clear( _objects, 0, _objectsListCount );
                #endregion

                // Free sidekicks and IObservableDomainActionTracker.
                _trackers.Clear();
                _sidekickManager.Clear( monitor );

                int version = r.ReadSmallInt32();
                if( version < 5 || version > CurrentSerializationVersion )
                {
                    throw new InvalidDataException( $"Version must be between 5 and {CurrentSerializationVersion}. Version read: {version}." );
                }
                r.SerializationVersion = version;
                _currentObjectUniquifier = 0;
                r.DebugReadMode();
                _currentObjectUniquifier = r.ReadInt32();
                _domainSecret = r.ReadBytes( DomainSecretKeyLength );
                var loaded = r.ReadString();
                if( loaded != expectedName ) throw new InvalidDataException( $"Domain name mismatch: loading domain named '{loaded}' but expected '{expectedName}'." );

                _transactionSerialNumber = r.ReadInt32();
                _transactionCommitTimeUtc = r.ReadDateTime();
                _actualObjectCount = r.ReadInt32();

                r.DebugCheckSentinel();

                // Clears and read the new free list.
                _freeList.Clear();
                int count = r.ReadNonNegativeSmallInt32();
                while( --count >= 0 )
                {
                    _freeList.Add( r.ReadNonNegativeSmallInt32() );
                }

                r.DebugCheckSentinel();

                // Clears and read the properties index.
                _properties.Clear();
                _propertiesByIndex.Clear();
                count = r.ReadNonNegativeSmallInt32();
                for( int iProp = 0; iProp < count; iProp++ )
                {
                    string name = r.ReadString();
                    var p = new ObservablePropertyChangedEventArgs( iProp, name );
                    _properties.Add( name, p );
                    _propertiesByIndex.Add( p );
                }

                r.DebugCheckSentinel();

                // Clears any internal objects.
                var internalObj = _firstInternalObject;
                while( internalObj != null )
                {
                    internalObj.Dispose( false );
                    internalObj = internalObj.Next;
                }
                _firstInternalObject = _lastInternalObject = null;
                _internalObjectCount = 0;

                // Clears any time event objects.
                _timeManager.Clear( monitor );

                // Resize _objects array.
                _objectsListCount = count = _actualObjectCount + _freeList.Count;
                while( _objectsListCount > _objects.Length )
                {
                    Array.Resize( ref _objects, _objects.Length * 2 );
                }
                
                // Reads objects. This can read Internal and Timed objects.
                for( int i = 0; i < count; ++i )
                {
                    _objects[i] = (ObservableObject)r.ReadObject();
                    Debug.Assert( _objects[i] == null || !_objects[i].IsDisposed );
                }

                // Fill roots array.
                r.DebugCheckSentinel();
                _roots.Clear();
                count = r.ReadNonNegativeSmallInt32();
                while( --count >= 0 )
                {
                    _roots.Add( _objects[r.ReadNonNegativeSmallInt32()] as ObservableRootObject );
                }

                // Reading InternalObjects.
                r.DebugCheckSentinel();
                count = r.ReadNonNegativeSmallInt32();
                while( --count >= 0 )
                {
                    var o = (InternalObject)r.ReadObject();
                    Debug.Assert( !o.IsDisposed );
                    Register( o );
                }

                // Reading Timed events.
                r.DebugCheckSentinel();
                _timeManager.Load( monitor, r );
                r.DebugCheckSentinel();
                _sidekickManager.Load( r );
                r.DebugCheckSentinel();
                // This is where specialized typed ObservableDomain bind their roots.
                OnLoaded();
                // Calling PostDeserializationActions finalizes the object's graph.
                r.ImplementationServices.ExecutePostDeserializationActions();

                // Calls the loadHook if any.
                bool callUpdateTimers = true;
                if( loadHook != null )
                {
                    try
                    {
                        callUpdateTimers = loadHook( this );
                    }
                    catch( Exception ex )
                    {
                        monitor.Error( "Error while calling load hook.", ex );
                        throw;
                    }
                }
                if( !_sidekickManager.CreateWaitingSidekicks( monitor, ex => { } ) )
                {
                    monitor.Error( $"At least one critical error occurred while activating sidekicks. The error should be investigated since this may well be a blocking error." );
                }
                if( callUpdateTimers )
                {
                    using( monitor.OpenDebug( "Load hook returned true: raising potential timed events." ) )
                    {
                        _timeManager.SetNextDueTimeUtc( monitor, _timeManager.ApplyChanges() );
                    }
                }
            }
            finally
            {
                _deserializeOrInitializing = false;
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
        /// <param name="loadHook">
        /// Optional hook called right after the deserialization but before sidekicks activation and timed events processing:
        /// when this hook returns true (that is the default), the <see cref="TimeManager"/> is sollicitated to update the internal
        /// timer delay based on the loaded timers and reminders configurations: elapsed timers and reminders fire.
        /// <para>
        /// Returning false from this hook doesn't skip the sidekicks activation but skips timed events processing (no timers and reminders fire).
        /// </para>
        /// <returns>True on success, false if timeout occurred.</returns>
        public bool Load( IActivityMonitor monitor, Stream stream, string expectedLoadedName, bool leaveOpen = false, Encoding? encoding = null, int millisecondsTimeout = -1, Func<ObservableDomain, bool>? loadHook = null )
        {
            if( monitor == null ) throw new ArgumentNullException( nameof( monitor ) );
            if( stream == null ) throw new ArgumentNullException( nameof( stream ) );
            if( expectedLoadedName == null ) throw new ArgumentNullException( nameof( expectedLoadedName ) );
            CheckDisposed();
            bool isWrite = _lock.IsWriteLockHeld;
            if( !isWrite && !_lock.TryEnterWriteLock( millisecondsTimeout ) ) return false;
            Debug.Assert( !isWrite || _currentTran != null, "isWrite => _currentTran != null" );
            bool needFakeTran = _currentTran == null || _currentTran.Monitor != monitor;
            if( needFakeTran ) new InitializationTransaction( monitor, this, false );
            using( monitor.OpenInfo( $"Loading domain (using {(needFakeTran ? "fake" : "current")} transaction)." ) )
            {
                try
                {
                    using( var d = new BinaryDeserializer( stream, null, _deserializers, leaveOpen, encoding ) )
                    {
                        d.Services.Add( this );
                        DoLoad( monitor, d, expectedLoadedName, loadHook );
                        return true;
                    }
                }
                catch( Exception ex )
                {
                    monitor.Error( ex );
                    throw;
                }
                finally
                {
                    if( needFakeTran ) _currentTran.Dispose();
                    if( !isWrite ) _lock.ExitWriteLock();
                }
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
        /// <param name="loadHook">
        /// Optional hook called right after the deserialization but before sidekicks activation and timed events processing:
        /// when this hook returns true (that is the default), the <see cref="TimeManager"/> is sollicitated to update the internal
        /// timer delay based on the loaded timers and reminders configurations: elapsed timers and reminders fire.
        /// <para>
        /// Returning false from this hook doesn't skip the sidekicks activation but skips timed events processing (no timers and reminders fire).
        /// </para>
        /// </param>
        /// <returns>True on success, false if timeout occurred.</returns>
        public bool Load( IActivityMonitor monitor, Stream stream, bool leaveOpen = false, Encoding? encoding = null, int millisecondsTimeout = -1, Func<ObservableDomain,bool>? loadHook = null )
        {
            return Load( monitor, stream, DomainName, leaveOpen, encoding, millisecondsTimeout, loadHook );
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

        internal bool IsDeserializing => _deserializeOrInitializing;

        internal void Register( InternalObject o )
        {
            Debug.Assert( o != null && o.ActualDomain == this && o.Prev == null && o.Next == null );
            CheckWriteLock( o );
            if( (o.Prev = _lastInternalObject) == null ) _firstInternalObject = o;
            else _lastInternalObject.Next = o;
            _lastInternalObject = o;
            ++_internalObjectCount;
            SideEffectsRegister( o );
        }

        internal void Unregister( InternalObject o )
        {
            Debug.Assert( o.ActualDomain == this );
            if( _firstInternalObject == o ) _firstInternalObject = o.Next;
            else o.Prev.Next = o.Next;
            if( _lastInternalObject == o ) _lastInternalObject = o.Prev;
            else o.Next.Prev = o.Prev;
            --_internalObjectCount;
            SideEffectUnregister( o );
        }

        internal ObservableObjectId CreateId( int idx ) => new ObservableObjectId( idx, ObservableObjectId.ForwardUniquifier( ref _currentObjectUniquifier ) );

        internal ObservableObjectId Register( ObservableObject o )
        {
            Debug.Assert( o != null && o.ActualDomain == this );
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
            if( !_deserializeOrInitializing )
            {
                // Deserialiation ctors don't call this Register method, BUT this Register
                // can be called when initializing a domain (for Root objects): in such case we don't want
                // to declare new objects.
                _changeTracker.OnNewObject( o, id, o._exporter );
            }
            ++_actualObjectCount;
            SideEffectsRegister( o );
            return id;
        }

        /// <summary>
        /// Called by:
        /// - InternalObject: Register() is called from normal and deserialization ctors that calls this.
        /// - ObservableObject: normal constructor calls Register() but deserialization constructors don't, deserialization constructors must call this directly.
        /// </summary>
        /// <param name="o">The new object.</param>
        internal void SideEffectsRegister( IDisposableObject o )
        {
            Debug.Assert( !_trackers.Contains( o ) );
            if( o is IObservableDomainActionTracker tracker ) _trackers.Add( tracker );
            Debug.Assert( _currentTran != null, "A transaction has been opened." );
            _sidekickManager.DiscoverSidekicks( _currentTran.Monitor, o );
        }

        /// <summary>
        /// This is called from the "real" Dispose calls (Unregister observable/internal objects), not from
        /// the clear from DoLoad.
        /// </summary>
        /// <param name="o">The disposed object.</param>
        void SideEffectUnregister( IDisposableObject o )
        {
            if( o is IObservableDomainActionTracker tracker )
            {
                Debug.Assert( _trackers.Contains( tracker ) );
                _trackers.Remove( tracker );
            }
        }

        internal void CheckBeforeDispose( IDisposableObject o )
        {
            Debug.Assert( !o.IsDisposed );
            CheckWriteLock( o ).CheckDisposed();
        }

        internal void Unregister( ObservableObject o )
        {
            if( !_deserializeOrInitializing ) _changeTracker.OnDisposeObject( o );
            _objects[o.OId.Index] = null;
            _freeList.Add( o.OId.Index );
            --_actualObjectCount;
            SideEffectUnregister( o );
        }

        /// <summary>
        /// Gets the <see cref="Observable.TimeManager"/> that is in charge of <see cref="ObservableReminder"/>
        /// and <see cref="ObservableTimer"/> objects.
        /// </summary>
        public TimeManager TimeManager => _timeManager;

        ITimeManager IObservableDomain.TimeManager => _timeManager;

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
        public IDisposableActivityMonitor? ObtainDomainMonitor( int milliSecondTimeout = LockedDomainMonitorTimeout, bool createAutonomousOnTimeout = true )
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

        void CheckDisposed()
        {
            if( _disposed ) throw new ObservableDomainDisposedException( DomainName );
        }


        /// <summary>
        /// Disposes this domain.
        /// This method calls <see cref="ObtainDomainMonitor(int, bool)"/>. If possible, use <see cref="Dispose(IActivityMonitor)"/> with
        /// an available monitor.
        /// As usual with Dispose methods, this can be called mulple times.
        /// </summary>
        public void Dispose()
        {
            if( !_disposed )
            {
                _lock.EnterWriteLock();
                if( !_disposed )
                {
                    using( var monitor = ObtainDomainMonitor() )
                    {
                        DoDispose( monitor );
                    }
                }
            }
        }

        /// <summary>
        /// Disposes this domain.
        /// If the <see cref="Dispose()"/> without parameters is called, the <see cref="ObtainDomainMonitor(int, bool)"/> is used:
        /// if a monitor is available, it is better to use this overload.
        /// As usual with Dispose methods, this can be called mulple times.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        public void Dispose( IActivityMonitor monitor )
        {
            if( monitor == null ) throw new ArgumentNullException( nameof( monitor ) );
            if( !_disposed )
            {
                _lock.EnterWriteLock();
                if( !_disposed )
                {
                    DoDispose( monitor );
                }
            }
        }

        void DoDispose( IActivityMonitor monitor )
        {
            Debug.Assert( !_disposed );
            Debug.Assert( _lock.IsWriteLockHeld );
            using( monitor.OpenInfo( $"Disposing domain '{DomainName}'." ) )
            {
                DomainClient?.OnDomainDisposed( monitor, this );
                DomainClient = null;
                _disposed = true;
                _timeManager.Timer.Dispose();
                _sidekickManager.Clear( monitor );
                if( monitor != _domainMonitor && Monitor.TryEnter( _domainMonitorLock, 0 ) )
                {
                    if( _domainMonitor != null ) _domainMonitor.MonitorEnd( "Domain disposed." );
                    Monitor.Exit( _domainMonitorLock );
                }
                _lock.ExitWriteLock();
                // There is a race condition here. AcquireReadLock, BeginTransaction (and others)
                // may have also seen a false _disposed and then try to acquire the lock.
                // If the race is won by this Dispose() thread, then the write lock is taken, released and
                // the lock itself should be disposed...
                //
                // There is 2 possibilities:
                // 1 - If the other thread acquire the lock between the previous _lock.ExitWriteLock and
                //     the following _lock.Dispose(), the other threads may work on a disposed domain even
                //     if they had perfectly acquired the lock :(.
                // 2 - If the other thread continue their execution after the following _lock.Dispose(), they will
                //     try to acquire a disposed lock. An ObjectDisposedException should be thrown (that is somehow fine).
                //
                // The first solution seems be to accept 2 (the disposed exception of the lock) and to detect 1 by
                // checking _disposed after each acquire: if _disposed then we must release the lock and
                // throw the ObjectDisposedException...
                // However, the _lock.Dispose() call below MAY occur while a TryEnter has been succesful and before
                // the _disposed check and the release: this would result in an awful "Incorrect Lock Dispose" exception
                // since disposing a lock while it is held is an error.
                // ==> This solution that seems the cleanest and most reasonable one is NOT an option... 
                //
                // Another solution is to defer the actual Disposing. By using the timer for instance: the domain is "logically disposed"
                // but technically perfectly valid until a timer event calls a DoRealDispose(). If this call is made after a "long enough"
                // time, there is no more active thread in the domain since all the public API throws the ObjectDisposedException.
                //
                // Is this satisfying? No. Did I miss something? May be.
                //
                // A third solution is simply to...
                //   - not Dispose the _lock (and rely on the Garbage Collector to clean it)...
                //   - ...and to implement the checks of the first solution.
                // And we can notice that by doing this:
                //  - there is no risk to acquire a disposed lock.
                //  - the domain is 'technically' functionnal, except that:
                //       - The AutoTimer has been disposed right above, it may throw an ObjectDisposedException and that is fine.
                //       - The DomainClient has been set to null: no more side effect (like transaction rollback) can occur.
                // ==> The domain doesn't act as expected anymore. We must throw an ObjectDisposedException to prevent such ambiguity.
                //
                // Conclusion:
                //   - We only protect, inside the lock, the Modify action: readonly operations are free to run and end in this "in between".
                //     The good place to call CheckDisposed() is in DoBeginTransaction().
                //   - We comment the folowing line.
                //
                //_lock.Dispose();
            }
        }

        internal void SendCommand( IDisposableObject o, object command )
        {
            CheckWriteLock( o ).CheckDisposed();
            _changeTracker.OnSendCommand( command );
        }

        internal bool EnsureSidekicks( IDisposableObject o )
        {
            CheckWriteLock( o ).CheckDisposed();
            return _sidekickManager.CreateWaitingSidekicks( _currentTran.Monitor, ex => _currentTran.AddError( CKExceptionData.CreateFrom( ex ) ) );
        }

        internal ObservablePropertyChangedEventArgs? OnPropertyChanged( ObservableObject o, string propertyName, object after )
        {
            if( _deserializeOrInitializing )
            {
                return null;
            }
            CheckWriteLock( o ).CheckDisposed();
            ObservablePropertyChangedEventArgs p = EnsurePropertyInfo( propertyName );
            if( o._exporter != null && o._exporter.ExportableProperties.Any( prop => prop.Name == propertyName ) )
            {
                _changeTracker.OnPropertyChanged( o, p, after );
            }
            return p;
        }

        ObservablePropertyChangedEventArgs EnsurePropertyInfo( string propertyName )
        {
            if( !_properties.TryGetValue( propertyName, out var p ) )
            {
                p = new ObservablePropertyChangedEventArgs( _properties.Count, propertyName );
                _changeTracker.OnNewProperty( p );
                _properties.Add( propertyName, p );
                _propertiesByIndex.Add( p );
            }

            return p;
        }

        int? FindPropertyId( string propertyName )
        {
            if( !_properties.TryGetValue( propertyName, out var p ) ) return null;
            return p.PropertyId;
        }

        internal ListRemoveAtEvent? OnListRemoveAt( ObservableObject o, int index )
        {
            if( _deserializeOrInitializing ) return null;
            CheckWriteLock( o ).CheckDisposed();
            return _changeTracker.OnListRemoveAt( o, index );
        }

        internal ListSetAtEvent? OnListSetAt( ObservableObject o, int index, object value )
        {
            if( _deserializeOrInitializing ) return null;
            CheckWriteLock( o ).CheckDisposed();
            return _changeTracker.OnListSetAt( o, index, value );
        }

        internal CollectionClearEvent? OnCollectionClear( ObservableObject o )
        {
            if( _deserializeOrInitializing ) return null;
            CheckWriteLock( o ).CheckDisposed();
            return _changeTracker.OnCollectionClear( o );
        }

        internal ListInsertEvent? OnListInsert( ObservableObject o, int index, object item )
        {
            if( _deserializeOrInitializing ) return null;
            CheckWriteLock( o ).CheckDisposed();
            return _changeTracker.OnListInsert( o, index, item );
        }

        internal CollectionMapSetEvent? OnCollectionMapSet( ObservableObject o, object key, object value )
        {
            if( _deserializeOrInitializing ) return null;
            CheckWriteLock( o ).CheckDisposed();
            return _changeTracker.OnCollectionMapSet( o, key, value );
        }

        internal CollectionRemoveKeyEvent? OnCollectionRemoveKey( ObservableObject o, object key )
        {
            if( _deserializeOrInitializing ) return null;
            CheckWriteLock( o ).CheckDisposed();
            return _changeTracker.OnCollectionRemoveKey( o, key );
        }

        internal CollectionAddKeyEvent? OnCollectionAddKey( ObservableObject o, object key )
        {
            if( _deserializeOrInitializing ) return null;
            CheckWriteLock( o ).CheckDisposed();
            return _changeTracker.OnCollectionAddKey( o, key );
        }

        IDisposableObject CheckWriteLock( [AllowNull]IDisposableObject o )
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
        /// in the exact same sequence of bytes.
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
                var originalTransactionSerialNumber = domain.TransactionSerialNumber;
                s.Position = 0;
                if( !domain.Load( monitor, s, true, millisecondsTimeout: milliSecondsTimeout, loadHook: d => true ) ) throw new Exception( "Reload failed: Unable to acquire lock." );

                using var checker = new BinarySerializer.CheckedWriteStream( originalBytes );
                if( !domain.Save( monitor, checker, true, millisecondsTimeout: milliSecondsTimeout, debugMode: useDebugMode ) ) throw new Exception( "Second Save failed: Unable to acquire lock." );
            }
        }

    }
}
