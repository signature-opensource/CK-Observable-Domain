using CK.Core;
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
    public partial class ObservableDomain : IObservableDomain, IDisposable, IObservableDomainInspector
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
        public const int CurrentSerializationVersion = 8;

        /// <summary>
        /// The length in bytes of the <see cref="SecretKey"/>.
        /// </summary>
        public const int DomainSecretKeyLength = 512;

        /// <summary>
        /// Gets an opaque object that is a command (can be send to <see cref="DomainView.SendCommand(object, bool)"/>) that
        /// triggers a snapshot of the domain (if a <see cref="IObservableDomainClient"/> can honor it).
        /// </summary>
        public static readonly object SnapshotDomainCommand = DBNull.Value;

        [ThreadStatic]
        internal static ObservableDomain? CurrentThreadDomain;

        internal readonly IExporterResolver _exporters;
        readonly ISerializerResolver _serializers;
        readonly IDeserializerResolver _deserializers;
        readonly TimeManager _timeManager;
        readonly SidekickManager _sidekickManager;
        readonly ObservableDomainPostActionExecutor _domainPostActionExecutor;
        internal readonly Random _random;

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
        readonly SemaphoreSlim _domainMonitorLock;

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
                                                                               .Select( o => o! ) // Waiting for https://github.com/dotnet/roslyn/issues/37468
                                                                               .GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        /// <summary>
        /// The change tracker handles the transformation of actual changes into events that are
        /// optimized and serialized by the <see cref="Commit(ObservableDomain, Func{string, ObservablePropertyChangedEventArgs}, DateTime)"/> method.
        /// </summary>
        class ChangeTracker
        {
            class PropChanged
            {
                public readonly ObservableObject Object;
                public readonly ObservablePropertyChangedEventArgs Info;
                public object? FinalValue;

                public long Key => Info.GetObjectPropertyId( Object );

                public PropChanged( ObservableObject o, ObservablePropertyChangedEventArgs p, object? final )
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
            List<ObservableDomainCommand> _commands;

            public ChangeTracker()
            {
                _changeEvents = new List<ObservableEvent>();
                _newObjects = new Dictionary<ObservableObject, List<PropertyInfo>?>( PureObjectRefEqualityComparer<ObservableObject>.Default );
                _propChanged = new Dictionary<long, PropChanged>();
                _commands = new List<ObservableDomainCommand>();
            }

            public SuccessfulTransactionEventArgs Commit( ObservableDomain domain,
                                                          Func<string, ObservablePropertyChangedEventArgs> ensurePropertInfo,
                                                          DateTime startTime,
                                                          int tranNum )
            {
                _changeEvents.RemoveAll( e => e is ICollectionEvent c && c.Object.IsDestroyed );
                foreach( var p in _propChanged.Values )
                {
                    if( !p.Object.IsDestroyed )
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
                var result = new SuccessfulTransactionEventArgs( domain, domain.FindPropertyId, _changeEvents.ToArray(), _commands, startTime, tranNum );
                Reset();
                return result;
            }

            /// <summary>
            /// Clears all events collected so far from the 3 internal lists and allocates a new empty command list for the next transaction.
            /// </summary>
            public void Reset()
            {
                _changeEvents.Clear();
                _newObjects.Clear();
                _propChanged.Clear();
                _commands = new List<ObservableDomainCommand>();
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

            internal void OnPropertyChanged( ObservableObject o, ObservablePropertyChangedEventArgs p, object? after )
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

            internal ListInsertEvent OnListInsert( ObservableObject o, int index, object? item )
            {
                var e = new ListInsertEvent( o, index, item );
                _changeEvents.Add( e );
                return e;
            }

            internal CollectionMapSetEvent OnCollectionMapSet( ObservableObject o, object key, object? value )
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

            internal void OnSendCommand( in ObservableDomainCommand command ) => _commands.Add( command );

        }

        /// <summary>
        /// Implements <see cref="IObservableTransaction"/>.
        /// </summary>
        class Transaction : IObservableTransaction
        {
            readonly ObservableDomain? _previous;
            readonly ObservableDomain _domain;
            readonly IDisposableGroup _monitorGroup;
            readonly DateTime _startTime;
            CKExceptionData[] _errors;
            TransactionResult _result;
            bool _resultInitialized;
            bool _fromModifyAsync;

            public Transaction( ObservableDomain d, IActivityMonitor monitor, DateTime startTime, IDisposableGroup g, bool fromModifyAsync )
            {
                _domain = d;
                Monitor = monitor;
                _previous = CurrentThreadDomain;
                CurrentThreadDomain = d;
                _startTime = startTime;
                _monitorGroup = g;
                _errors = Array.Empty<CKExceptionData>();
                _fromModifyAsync = fromModifyAsync;
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

                SuccessfulTransactionEventArgs? ctx = null;
                if( _errors.Length != 0 )
                {
                    using( Monitor.OpenWarn( "Committing a Transaction on error. Calling DomainClient.OnTransactionFailure." ) )
                    {
                        // On errors, resets the change tracker, sends the errors to the Clients
                        // and creates an error TransactionResult. 
                        _result = new TransactionResult( _errors, _startTime );
                        _domain._changeTracker.Reset();
                        try
                        {
                            _domain.DomainClient?.OnTransactionFailure( Monitor, _domain, _errors );
                        }
                        catch( Exception ex )
                        {
                            Monitor.Error( "Error in DomainClient.OnTransactionFailure.", ex );
                            _result.SetClientError( ex );
                        }
                    }
                }
                else
                {
                    using( Monitor.OpenDebug( "Transaction has no error. Calling DomainClient.OnTransactionCommit." ) )
                    {
                        ctx = _domain._changeTracker.Commit( _domain, _domain.EnsurePropertyInfo, _startTime, ++_domain._transactionSerialNumber );
                        _domain._transactionCommitTimeUtc = ctx.CommitTimeUtc;
                        try
                        {
                            _result = new TransactionResult( ctx );
                            _domain.DomainClient?.OnTransactionCommit( ctx );
                        }
                        catch( Exception ex )
                        {
                            Monitor.Fatal( "Error in IObservableTransactionManager.OnTransactionCommit. This is a Critical error since the Domain state integrity may be compromised.", ex );
                            _result.SetClientError( ex );
                            ctx = null;
                        }
                    }
                }

                CurrentThreadDomain = _previous;
                _monitorGroup.Dispose();
                _domain._currentTran = null;

                using( Monitor.OpenDebug( "Leaving WriteLock. Raising SuccessfulTransaction event." ) )
                {
                    _domain._lock.ExitWriteLock();
                    // Back to Readable lock: publishes SuccessfulTransaction.
                    if( _result.Success )
                    {
                        Debug.Assert( ctx != null );

                        var errors = _domain.RaiseOnSuccessfulTransaction( ctx );
                        if( errors != null ) _result.SetSuccessfulTransactionErrors( errors );
                    }
                }
                // Before leaving the read lock (nobody can start a new transaction), let's enqueue
                // the transaction result.
                // This is why we must know here if we are called by ModifyAsync (handling is guaranteed) or by
                // a direct Modify in which case, the domain post actions are lost.
                // Since no post actions will be executed if an error occurs, we skip this.
                if( _result.Success && _fromModifyAsync )
                {
                    _result.Initialize( true );
                    _domain._domainPostActionExecutor.Enqueue( _result );
                }
                else
                {
                    _result.Initialize( false );
                }
                _domain._lock.ExitUpgradeableReadLock();
                // Outside of the lock: on success, sidekicks execute the Command objects.
                if( _result.Success )
                {
                    using( Monitor.OpenDebug( "Leaving UpgradeableReadLock and no error so far: submitting Commands to sidekicks." ) )
                    {
                        Debug.Assert( _result._postActions != null && _result._domainPostActions != null );
                        var errors = _domain._sidekickManager.ExecuteCommands( Monitor, _result, _result._postActions, _result._domainPostActions );
                        if( errors != null ) _result.SetCommandHandlingErrors( errors );
                    }
                }
                Monitor.Debug( () => "Committed: " + _result.ToString() );
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
        /// <param name="startTimer">Whether to initially start the <see cref="TimeManager"/>.</param>
        /// <param name="serviceProvider">The service providers that will be used to resolve the <see cref="ObservableDomainSidekick"/> objects.</param>
        public ObservableDomain( IActivityMonitor monitor, string domainName, bool startTimer, IServiceProvider? serviceProvider = null )
            : this(monitor, domainName, startTimer, null, serviceProvider)
        {
        }

        /// <summary>
        /// Initializes a new <see cref="ObservableDomain"/> with a <see cref="DomainClient"/> an optionals explicit exporter, serializer
        /// and deserializer handlers.
        /// </summary>
        /// <param name="monitor">The monitor used to log the construction of this domain. Cannot be null.</param>
        /// <param name="domainName">Name of the domain. Must not be null but can be empty.</param>
        /// <param name="startTimer">Whether to initially start the <see cref="TimeManager"/>.</param>
        /// <param name="client">The observable client (head of the Chain of Responsibility) to use. Can be null.</param>
        /// <param name="serviceProvider">The service providers that will be used to resolve the <see cref="ObservableDomainSidekick"/> objects.</param>
        public ObservableDomain( IActivityMonitor monitor,
                                 string domainName,
                                 bool startTimer,
                                 IObservableDomainClient? client,
                                 IServiceProvider? serviceProvider = null )
            : this(monitor, domainName, startTimer, client, true, serviceProvider, exporters: null, serializers: null, deserializers: null)
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
        /// <param name="startTimer">
        /// Ensures that the <see cref="ObservableDomain.TimeManager"/> is running or stopped.
        /// When null, it keeps its previous state (it is initially stopped at domain creation) and then its current state is persisted.
        /// </param>
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
                                 bool? startTimer = null,
                                 IExporterResolver? exporters = null,
                                 ISerializerResolver? serializers = null,
                                 IDeserializerResolver? deserializers = null )
            : this(monitor, domainName, false, client, false, serviceProvider, exporters, serializers, deserializers)
        {
            using( monitor.OpenInfo( $"Initializing new domain '{domainName}' from stream." ) )
            {
                try
                {
                    _currentTran = new InitializationTransaction( monitor, this );
                    DoLoad( monitor, s, domainName, leaveOpen, encoding, startTimer, mustStartTimer =>
                    {
                        client?.OnDomainCreated( monitor, this, ref mustStartTimer );
                        return mustStartTimer;
                    } );
                }
                finally
                {
                    _currentTran?.Dispose();
                }


            }
        }

        ObservableDomain( IActivityMonitor monitor,
                          string domainName,
                          bool startTimer,
                          IObservableDomainClient? client,
                          bool callClientOnCreate,
                          IServiceProvider? serviceProvider,
                          IExporterResolver? exporters,
                          ISerializerResolver? serializers,
                          IDeserializerResolver? deserializers)
        {
            if( monitor == null ) throw new ArgumentNullException( nameof( monitor ) );
            DomainName = domainName ?? throw new ArgumentNullException( nameof( domainName ) );
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
            _domainPostActionExecutor = new ObservableDomainPostActionExecutor( this );
            DefaultEventArgs = new ObservableDomainEventArgs( this );
            // LockRecursionPolicy.NoRecursion: reentrancy must NOT be allowed.
            _lock = new ReaderWriterLockSlim( LockRecursionPolicy.NoRecursion );
            _saveLock = new Object();
            _domainMonitorLock = new SemaphoreSlim( 1, 1 );
            _random = new Random();

            if( callClientOnCreate )
            {
                client?.OnDomainCreated( monitor, this, ref startTimer );
                // If the secret has not been restored, initializes a new one.
                if( _domainSecret == null ) _domainSecret = CreateSecret();
            }
            if( startTimer ) _timeManager.DoStartOrStop( monitor, true );
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
            readonly ObservableDomain? _previousThreadDomain;
            readonly IObservableTransaction? _previousTran;
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
                : base( topic )
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
                    _domain._domainMonitorLock.Release();
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
        /// should occur). A typical implementation is to capture any required domain object's state and use
        /// <see cref="SuccessfulTransactionEventArgs.PostActions"/> or <see cref="SuccessfulTransactionEventArgs.DomainPostActions"/>
        /// to post asynchronous actions (or to send commands thanks to <see cref="SuccessfulTransactionEventArgs.SendCommand(ObservableDomainCommand)"/>
        /// that will be processed by the sidekicks).
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
        /// </remarks>
        public IObservableTransaction? BeginTransaction( IActivityMonitor monitor, int millisecondsTimeout = -1 )
        {
            if( monitor == null ) throw new ArgumentNullException( nameof( monitor ) );
            CheckDisposed();
            return DoBeginTransaction( monitor, millisecondsTimeout, fromModifyAsync: false );
        }

        IObservableTransaction? DoBeginTransaction( IActivityMonitor monitor, int millisecondsTimeout, bool fromModifyAsync )
        {
            if( !TryEnterUpgradeableReadAndWriteLockAtOnce( millisecondsTimeout ) )
            {
                monitor.Warn( $"Write lock not obtained in less than {millisecondsTimeout} ms." );
                return null;
            }
            return DoCreateObservableTransaction( monitor, throwException: true, fromModifyAsync ).Item1;
        }


        /// <summary>
        /// Returns the created IObservableTransaction XOR an IObservableDomainClient.OnTransactionStart exception.
        /// Write lock must be held before the call and kept until (but released on error).
        /// </summary>
        /// <param name="m">The monitor to use.</param>
        /// <param name="throwException">Whether to throw or return the potential IObservableDomainClient.OnTransactionStart exception.</param>
        /// <returns>The transaction XOR the IObservableDomainClient.OnTransactionStart exception.</returns>
        (IObservableTransaction?, Exception?) DoCreateObservableTransaction( IActivityMonitor m, bool throwException, bool fromModifyAsync )
        {
            Debug.Assert( m != null && _lock.IsWriteLockHeld );
            var group = m.OpenTrace( "Starting transaction." );
            var startTime = DateTime.UtcNow;
            try
            {
                // This could throw and be handled just like other pre-transaction errors (when a buggy client throws during OnTransactionStart).
                // Depending on throwException parameter, it will be rethrown or returned (returning the exception is for MofifyNoThrow).
                // See DoDispose method for the discussion about disposal...
                CheckDisposed();
                DomainClient?.OnTransactionStart( m, this, startTime );
            }
            catch( Exception ex )
            {
                m.Error( "While calling IObservableTransactionManager.OnTransactionStart().", ex );
                group.Dispose();
                _lock.ExitWriteLock();
                if( throwException ) throw;
                return (null, ex);
            }
            // No OnTransactionStart error.
            return (_currentTran = new Transaction( this, m, startTime, group, fromModifyAsync ), null);
        }

        /// <summary>
        /// Enables modifications to be done inside a transaction and a try/catch block.
        /// Any exceptions raised by <see cref="IObservableDomainClient.OnTransactionStart(IActivityMonitor,ObservableDomain, DateTime)"/> are thrown
        /// by this method, but any other exceptions are caught, logged, and appears in <see cref="TransactionResult"/>.
        /// <para>
        /// Please note that, being synchronous, this method doesn't execute the post actions or domain post actions.
        /// If there are post action or domain post actions, they won't be executed.
        /// </para>
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
            return DoModify( monitor, actions, millisecondsTimeout, fromModifyAsync: false );
        }

        TransactionResult DoModify( IActivityMonitor monitor, Action? actions, int millisecondsTimeout, bool fromModifyAsync )
        {
            using( var t = DoBeginTransaction( monitor, millisecondsTimeout, fromModifyAsync ) )
            {
                if( t == null ) return TransactionResult.Empty;
                return DoModifyAndCommit( actions, t, fromTimer: false );
            }
        }

        /// <summary>
        /// Modify the domain once a transaction has been opened and calls the <see cref="IObservableDomainClient"/>
        /// that have been registered: all this occurs in the lock and it is released at the end.
        /// This never throws since the transaction result contains the errors.
        /// </summary>
        /// <param name="actions">The actions to execute. Can be null.</param>
        /// <param name="t">The observable transaction. Cannot be null.</param>
        /// <returns>The transaction result. Will never be null.</returns>
        TransactionResult DoModifyAndCommit( Action? actions, IObservableTransaction t, bool fromTimer )
        {
            Debug.Assert( t != null );
            try
            {
                if( _timeManager.IsRunning ) _timeManager.RaiseElapsedEvent( t.Monitor, t.StartTime, fromTimer );
                bool skipped = false;
                foreach( var tracker in _trackers )
                {
                    if( !tracker.BeforeModify( t.Monitor, t.StartTime ) )
                    {
                        skipped = true;
                        break;
                    }
                }
                bool updatedMinHeapDone = false;
                if( !skipped && actions != null )
                {
                    actions();
                    if( _sidekickManager.CreateWaitingSidekicks( t.Monitor, ex => t.AddError( CKExceptionData.CreateFrom( ex ) ), true ) )
                    {
                        var now = DateTime.UtcNow;
                        foreach( var tracker in _trackers ) tracker.AfterModify( t.Monitor, t.StartTime, now - t.StartTime );
                        if( _timeManager.IsRunning )
                        {
                            updatedMinHeapDone = true;
                            _timeManager.RaiseElapsedEvent( t.Monitor, now, fromTimer );
                        }
                    }
                }
                if( !updatedMinHeapDone )
                {
                    // If the time manager is not running, we must
                    // handle the changed timed events so that the
                    // active timed event min heap is up to date.
                    _timeManager.UpdateMinHeap();
                }
            }
            catch( Exception ex )
            {
                bool swallowError = false;
                Exception? exOnUnhandled = null;
                if( DomainClient != null )
                {
                    try
                    {
                        DomainClient?.OnUnhandledError( t.Monitor, this, ex, ref swallowError );
                    }
                    catch( Exception ex2 )
                    {
                        swallowError = false;
                        exOnUnhandled = ex2;
                    }
                }
                if( !swallowError )
                {
                    t.Monitor.Error( ex );
                    t.AddError( CKExceptionData.CreateFrom( ex ) );
                    if( exOnUnhandled != null )
                    {
                        t.Monitor.Error( exOnUnhandled );
                        t.AddError( CKExceptionData.CreateFrom( exOnUnhandled ) );
                    }
                }
            }
            return t.Commit();
        }

        /// <summary>
        /// Modifies this ObservableDomain, and on success executes the <see cref="SuccessfulTransactionEventArgs.PostActions"/> and
        /// send the <see cref="SuccessfulTransactionEventArgs.DomainPostActions"/> to <see cref="ObservableDomainPostActionExecutor"/>.
        /// <para>
        /// Any exceptions raised by <see cref="IObservableDomainClient.OnTransactionStart(IActivityMonitor,ObservableDomain, DateTime)"/> (at the start of the process)
        /// and by any post actions (after the successful commit) are thrown by this method.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="actions">
        /// The actions to execute inside the ObservableDomain's current transaction.
        /// Can be null: only timed events that have elapsed are executed if any.
        /// </param>
        /// <param name="millisecondsTimeout">
        /// The maximum number of milliseconds to wait for a write access before giving up.
        /// Wait indefinitely by default.
        /// </param>
        /// <param name="parallelDomainPostActions">
        /// False to wait for the success of the <see cref="SuccessfulTransactionEventArgs.PostActions"/> before
        /// allowing the <see cref="SuccessfulTransactionEventArgs.DomainPostActions"/> to run: when PostActions fail, all domain post actions are skipped.
        /// <para>
        /// By default, post actions are executed and domain post actions can immediately be executed by the <see cref="ObservableDomainPostActionExecutor"/> (as
        /// soon as all previous transaction's domain post actions have ran of course).
        /// </para>
        /// </param>
        /// <returns>
        /// The transaction result from <see cref="ObservableDomain.Modify"/>. <see cref="TransactionResult.Empty"/> when the
        /// lock has not been taken before <paramref name="millisecondsTimeout"/>.
        /// </returns>
        public async Task<TransactionResult> ModifyAsync( IActivityMonitor monitor, Action actions, int millisecondsTimeout = -1, bool parallelDomainPostActions = true )
        {
            var tr = DoModify( monitor, actions, millisecondsTimeout, fromModifyAsync: true );
            await tr.ExecutePostActionsAsync( monitor, parallelDomainPostActions, throwException: true ).ConfigureAwait( false );
            return tr;
        }

        /// <summary>
        /// Same as <see cref="ModifyAsync(IActivityMonitor, Action, int, bool)"/> but calls <see cref="TransactionResult.ThrowOnFailure()"/>:
        /// this methods always throw on any error (except the error of the <see cref="SuccessfulTransactionEventArgs.DomainPostActions"/>
        /// since it may happen later).
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
        /// <param name="parallelDomainPostActions">
        /// False to wait for the success of the <see cref="SuccessfulTransactionEventArgs.PostActions"/> before
        /// allowing the <see cref="SuccessfulTransactionEventArgs.DomainPostActions"/> to run: when PostActions fail, all domain post actions are skipped.
        /// <para>
        /// By default, post actions are executed and domain post actions can immediately be executed by the <see cref="ObservableDomainPostActionExecutor"/> (as
        /// soon as all previous transaction's domain post actions have ran of course).
        /// </para>
        /// </param>
        /// <returns>
        /// The transaction result from <see cref="ObservableDomain.Modify"/>. <see cref="TransactionResult.Empty"/> when the
        /// lock has not been taken before <paramref name="millisecondsTimeout"/>.
        /// This is necessarily a successful result since otherwise an exception is thrown (note that the domain post actions
        /// are executed later by the <see cref="ObservableDomainPostActionExecutor"/>).
        /// </returns>
        public async Task<TransactionResult> ModifyThrowAsync( IActivityMonitor monitor, Action actions, int millisecondsTimeout = -1, bool parallelDomainPostActions = true )
        {
            var r = await ModifyAsync( monitor, actions, millisecondsTimeout, parallelDomainPostActions ).ConfigureAwait( false );
            r.ThrowOnFailure();
            return r;
        }

        /// <summary>
        /// Safe version of <see cref="ModifyAsync(IActivityMonitor, Action, int, bool)"/> that will never throw: any exception raised
        /// by <see cref="IObservableDomainClient.OnTransactionStart(IActivityMonitor, ObservableDomain, DateTime)"/>
        /// or by post actions execution is logged and returned in the <see cref="TransactionResult"/>.
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
        /// <param name="parallelDomainPostActions">
        /// False to wait for the success of the <see cref="SuccessfulTransactionEventArgs.PostActions"/> before
        /// allowing the <see cref="SuccessfulTransactionEventArgs.DomainPostActions"/> to run: when PostActions fail, all domain post actions are skipped.
        /// <para>
        /// By default, post actions are executed and domain post actions can immediately be executed by the <see cref="ObservableDomainPostActionExecutor"/> (as
        /// soon as all previous transaction's domain post actions have ran of course).
        /// </para>
        /// </param>
        /// <returns>
        /// Returns any initial exception, the transaction result (that may be <see cref="TransactionResult.Empty"/>).
        /// </returns>
        public Task<(Exception? OnStartTransactionError, TransactionResult Transaction)> ModifyNoThrowAsync( IActivityMonitor monitor, Action actions, int millisecondsTimeout = -1, bool parallelDomainPostActions = true )
        {
            if( monitor == null ) throw new ArgumentNullException( nameof( monitor ) );
            CheckDisposed();
            return DoModifyNoThrowAsync( monitor, actions, millisecondsTimeout, false, parallelDomainPostActions );
        }

        internal async Task<(Exception?, TransactionResult)> DoModifyNoThrowAsync( IActivityMonitor monitor, Action actions, int millisecondsTimeout, bool fromTimer, bool parallelDomainPostActions )
        {
            TransactionResult tr = TransactionResult.Empty;
            if( TryEnterUpgradeableReadAndWriteLockAtOnce( millisecondsTimeout ) )
            {
                var tEx = DoCreateObservableTransaction( monitor, throwException: false, fromModifyAsync: true );
                Debug.Assert( (tEx.Item1 != null) != (tEx.Item2 != null), "The IObservableTransaction XOR IObservableDomainClient.OnTransactionStart() exception." );
                if( tEx.Item2 != null ) return (tEx.Item2, tr);

                tr = DoModifyAndCommit( actions, tEx.Item1!, fromTimer );
                await tr.ExecutePostActionsAsync( monitor, parallelDomainPostActions, throwException: false ).ConfigureAwait( false );
            }
            else monitor.Warn( $"WriteLock not obtained in {millisecondsTimeout} ms (returning TransactionResult.Empty)." );
            return (null, tr);
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

        /// <summary>
        /// Loads previously <see cref="Save"/>d objects from a named domain into this domain: the <paramref name="expectedLoadedName"/> can be
        /// this <see cref="DomainName"/> or another name but it must match the name in the stream otherwise an <see cref="InvalidDataException"/>
        /// is thrown.
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
        /// <param name="startTimer">
        /// Ensures that the <see cref="ObservableDomain.TimeManager"/> is running or stopped.
        /// When null, it keeps its previous state (it is initially stopped at domain creation) and then its current state is persisted.
        /// </param>
        /// <returns>True on success, false if timeout occurred.</returns>
        public bool Load( IActivityMonitor monitor, Stream stream, string expectedLoadedName, bool leaveOpen = false, Encoding? encoding = null, int millisecondsTimeout = -1, bool? startTimer = null )
        {
            if( monitor == null ) throw new ArgumentNullException( nameof( monitor ) );
            if( stream == null ) throw new ArgumentNullException( nameof( stream ) );
            if( expectedLoadedName == null ) throw new ArgumentNullException( nameof( expectedLoadedName ) );
            CheckDisposed();
            bool hasWriteLock = _lock.IsWriteLockHeld;
            if( !hasWriteLock && !_lock.TryEnterWriteLock( millisecondsTimeout ) ) return false;
            Debug.Assert( !hasWriteLock || _currentTran != null, "isWrite => _currentTran != null" );
            bool needFakeTran = _currentTran == null || _currentTran.Monitor != monitor;
            using( monitor.OpenInfo( $"Reloading domain '{DomainName}' (using {(needFakeTran ? "fake" : "current")} transaction) from stream." ) )
            {
                if( needFakeTran ) new InitializationTransaction( monitor, this, false );
                try
                {
                    DoLoad( monitor, stream, expectedLoadedName, leaveOpen, encoding, startTimer );
                    return true;
                }
                finally
                {
                    if( needFakeTran ) _currentTran.Dispose();
                    if( !hasWriteLock ) _lock.ExitWriteLock();
                }
            }
        }

        void DoLoad( IActivityMonitor monitor, Stream stream, string expectedLoadedName, bool leaveOpen, Encoding? encoding, bool? startTimer, Func<bool,bool>? beforeTimer = null )
        {
            try
            {
                using( var d = new BinaryDeserializer( stream, null, _deserializers, leaveOpen, encoding ) )
                {
                    d.Services.Add( this );
                    var mustStartTimer = DoRealLoad( monitor, d, expectedLoadedName, startTimer );
                    if( beforeTimer != null ) mustStartTimer = beforeTimer( mustStartTimer );
                    if( mustStartTimer )
                    {
                        _timeManager.DoStartOrStop( monitor, true );
                    }
                }
            }
            catch( Exception ex )
            {
                monitor.Error( ex );
                throw;
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
        /// <param name="startTimer">
        /// Ensures that the <see cref="ObservableDomain.TimeManager"/> is running or stopped.
        /// When null, it keeps its previous state (it is initially stopped at domain creation) and then its current state is persisted.
        /// </param>
        /// <returns>True on success, false if timeout occurred.</returns>
        public bool Load( IActivityMonitor monitor, Stream stream, bool leaveOpen = false, Encoding? encoding = null, int millisecondsTimeout = -1, bool? startTimer = null )
        {
            return Load( monitor, stream, DomainName, leaveOpen, encoding, millisecondsTimeout, startTimer );
        }

        /// <summary>
        /// Called after a <see cref="Load(IActivityMonitor, Stream, string, bool, Encoding?, int, bool?)"/>.
        /// Does nothing at this level.
        /// </summary>
        protected internal virtual void OnLoaded()
        {
        }

        internal void StartOrStopTimeManager( bool start )
        {
            CheckWriteLock( null );
            _timeManager.DoStartOrStop( _currentTran.Monitor, start );
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
        internal void SideEffectsRegister( IDestroyable o )
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
        void SideEffectUnregister( IDestroyable o )
        {
            if( o is IObservableDomainActionTracker tracker )
            {
                Debug.Assert( _trackers.Contains( tracker ) );
                _trackers.Remove( tracker );
            }
        }

        internal void CheckBeforeDestroy( IDestroyable o )
        {
            Debug.Assert( !o.IsDestroyed );
            CheckWriteLock( o );
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
            if( !_domainMonitorLock.Wait( milliSecondTimeout ) )
            {
                return createAutonomousOnTimeout ? new DomainActivityMonitor( $"Autonomous monitor for Observable Domain '{DomainName}'.", null, milliSecondTimeout ) : null;
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
        /// As usual with Dispose methods, this can be called multiple times.
        /// </summary>
        public void Dispose()
        {
            if( !_disposed )
            {
                _timeManager.Timer.QuickStopBeforeDispose();
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
        /// As usual with Dispose methods, this can be called multiple times.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        public void Dispose( IActivityMonitor monitor )
        {
            if( monitor == null ) throw new ArgumentNullException( nameof( monitor ) );
            if( !_disposed )
            {
                _timeManager.Timer.QuickStopBeforeDispose();
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
                bool executorRun = _domainPostActionExecutor.Stop();
                DomainClient?.OnDomainDisposed( monitor, this );
                DomainClient = null;
                _disposed = true;

                //UnloadDomain( monitor );
                _timeManager.Timer.Dispose();
                _sidekickManager.Clear( monitor );

                if( monitor != _domainMonitor && _domainMonitorLock.Wait( 0 ) )
                {
                    if( _domainMonitor != null ) _domainMonitor.MonitorEnd( "Disposing domain." );
                    _domainMonitorLock.Release();
                }
                if( executorRun )
                {
                    monitor.Debug( "Waiting for DomainPostActionExecutor stopped..." );
                    _domainPostActionExecutor.WaitStopped();
                    monitor.Debug( "...DomainPostActionExecutor stopped." );
                }
                monitor.Debug( "Exiting write lock. Domain disposed." );
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
                // However, the _lock.Dispose() call below MAY occur while a TryEnter has been successful and before
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
                //  - the domain is 'technically' functional, except that:
                //       - The AutoTimer has been disposed right above, it may throw an ObjectDisposedException and that is fine.
                //       - The DomainClient has been set to null: no more side effect (like transaction rollback) can occur.
                // ==> The domain doesn't act as expected anymore. We must throw an ObjectDisposedException to prevent such ambiguity.
                //
                // Conclusion:
                //   - We only protect, inside the lock, the Modify action: read only operations are free to run and end in this "in between".
                //     The good place to call CheckDisposed() is in DoBeginTransaction().
                //   - We comment the following line.
                //
                //_lock.Dispose();
            }
        }

        internal void SendCommand( IDestroyable o, ObservableDomainCommand command )
        {
            CheckWriteLock( o ).CheckDestroyed();
            _changeTracker.OnSendCommand( command );
        }

        /// <summary>
        /// Sends a <see cref="SnapshotDomainCommand"/>.
        /// This must be called from inside a transaction.
        /// </summary>
        public void SendSnapshotCommand()
        {
            CheckWriteLock( null );
            _changeTracker.OnSendCommand( new ObservableDomainCommand( SnapshotDomainCommand ) );
        }

        internal bool EnsureSidekicks( IDestroyable o )
        {
            CheckWriteLock( o ).CheckDestroyed();
            Debug.Assert( _currentTran != null );
            return _sidekickManager.CreateWaitingSidekicks( _currentTran.Monitor, ex => _currentTran.AddError( CKExceptionData.CreateFrom( ex ) ), false );
        }

        internal ObservablePropertyChangedEventArgs? OnPropertyChanged( ObservableObject o, string propertyName, object? after )
        {
            if( _deserializeOrInitializing )
            {
                return null;
            }
            CheckWriteLock( o ).CheckDestroyed();
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
            CheckWriteLock( o ).CheckDestroyed();
            return _changeTracker.OnListRemoveAt( o, index );
        }

        internal ListSetAtEvent? OnListSetAt( ObservableObject o, int index, object value )
        {
            if( _deserializeOrInitializing ) return null;
            CheckWriteLock( o ).CheckDestroyed();
            return _changeTracker.OnListSetAt( o, index, value );
        }

        internal CollectionClearEvent? OnCollectionClear( ObservableObject o )
        {
            if( _deserializeOrInitializing ) return null;
            CheckWriteLock( o ).CheckDestroyed();
            return _changeTracker.OnCollectionClear( o );
        }

        internal ListInsertEvent? OnListInsert( ObservableObject o, int index, object? item )
        {
            if( _deserializeOrInitializing ) return null;
            CheckWriteLock( o ).CheckDestroyed();
            return _changeTracker.OnListInsert( o, index, item );
        }

        internal CollectionMapSetEvent? OnCollectionMapSet( ObservableObject o, object key, object? value )
        {
            if( _deserializeOrInitializing ) return null;
            CheckWriteLock( o ).CheckDestroyed();
            return _changeTracker.OnCollectionMapSet( o, key, value );
        }

        internal CollectionRemoveKeyEvent? OnCollectionRemoveKey( ObservableObject o, object key )
        {
            if( _deserializeOrInitializing ) return null;
            CheckWriteLock( o ).CheckDestroyed();
            return _changeTracker.OnCollectionRemoveKey( o, key );
        }

        internal CollectionAddKeyEvent? OnCollectionAddKey( ObservableObject o, object key )
        {
            if( _deserializeOrInitializing ) return null;
            CheckWriteLock( o ).CheckDestroyed();
            return _changeTracker.OnCollectionAddKey( o, key );
        }

        IDestroyable CheckWriteLock( [AllowNull]IDestroyable o )
        {
            if( !_lock.IsWriteLockHeld )
            {
                if( _currentTran == null ) throw new InvalidOperationException( "A transaction is required." );
                if( _lock.IsReadLockHeld ) throw new InvalidOperationException( "Concurrent access: only Read lock has been acquired." );
                throw new InvalidOperationException( "Concurrent access: write lock must be acquired." );
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
            using( monitor.OpenInfo( $"Idempotence check of '{domain.DomainName}'." ) )
            using( var s = new MemoryStream() )
            {
                if( !domain.Save( monitor, s, true, millisecondsTimeout: milliSecondsTimeout, debugMode: useDebugMode ) ) throw new Exception( "First Save failed: Unable to acquire lock." );
                var originalBytes = s.ToArray();
                var originalTransactionSerialNumber = domain.TransactionSerialNumber;
                s.Position = 0;
                if( !domain.Load( monitor, s, true, millisecondsTimeout: milliSecondsTimeout, startTimer: null ) ) throw new Exception( "Reload failed: Unable to acquire lock." );

                using var checker = new BinarySerializer.CheckedWriteStream( originalBytes );
                if( !domain.Save( monitor, checker, true, millisecondsTimeout: milliSecondsTimeout, debugMode: useDebugMode ) ) throw new Exception( "Second Save failed: Unable to acquire lock." );
            }
        }

    }
}
