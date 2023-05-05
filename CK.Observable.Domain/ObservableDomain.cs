using CK.BinarySerialization;
using CK.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
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
        /// <see cref="TransactionResult.Errors"/> whenever a transaction
        /// has not been committed.
        /// </summary>
        public static readonly CKExceptionData UncomittedTransaction = CKExceptionData.Create( "Uncommitted transaction." );

        /// <summary>
        /// Default timeout before <see cref="ObtainDomainMonitor(int, bool)"/> creates a new temporary <see cref="IDisposableActivityMonitor"/>
        /// instead of reusing the default one.
        /// </summary>
        public const int LockedDomainMonitorTimeout = 1000;

        /// <summary>
        /// The length in bytes of the <see cref="SecretKey"/>.
        /// </summary>
        public const int DomainSecretKeyLength = 512;

        /// <summary>
        /// Gets an opaque object that is a command (can be send to <see cref="DomainView.SendBroadcastCommand(object, bool)"/>) that
        /// triggers a snapshot of the domain (if a <see cref="IObservableDomainClient"/> can honor it).
        /// </summary>
        public static readonly object SnapshotDomainCommand = DBNull.Value;

        [ThreadStatic]
        internal static ObservableDomain? CurrentThreadDomain;

        internal readonly IExporterResolver _exporters;
        readonly TimeManager _timeManager;
        readonly SidekickManager _sidekickManager;
        readonly ObservableDomainPostActionExecutor _domainPostActionExecutor;
        internal readonly Random _random;
        Action<ITransactionDoneEvent>? _inspectorEvent;
        BinarySerializerContext _serializerContext;
        BinaryDeserializerContext _deserializerContext;
        PocoDirectory? _pocoDirectory;

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
        byte[]? _domainSecret;

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

        IInternalTransaction? _currentTran;
        int _transactionSerialNumber;
        DateTime _transactionCommitTimeUtc;

        // Available to objects.
        internal readonly ObservableDomainEventArgs DefaultEventArgs;

        // A reusable domain monitor is created on-demand and is protected by an exclusive lock.
        DomainActivityMonitor? _domainMonitor;
        readonly SemaphoreSlim _domainMonitorLock;

        // This lock is used to allow one and only one Save at a time: this is to protect
        // the potential fake transaction that is used when saving.
        readonly object _saveLock;

        private protected CurrentTransactionStatus _transactionStatus;

        static ObservableDomain()
        {
            BinaryDeserializer.DefaultSharedContext.AddDeserializationHook( t =>
            {
                // Do not handle "Observable" prefix in our base libraries: these base libraries
                // still use full prefix, only the observable objects in "application layers" should
                // use simpler "O" prefix.
                //
                // Note that this code doesn't handle nested Observable objects (the ones with a '+' in
                // their type name).
                //
                if( t.WrittenInfo.TypeName.StartsWith( "Observable", StringComparison.Ordinal )
                    && t.WrittenInfo.AssemblyName != "CK.Observable.Domain"
                    && t.WrittenInfo.AssemblyName != "CK.Observable.Device"
                    && t.WrittenInfo.AssemblyName != "CK.Observable.League" )
                {
                    var aqn = $"{t.WrittenInfo.TypeNamespace}.{t.WrittenInfo.TypeName}, {t.WrittenInfo.AssemblyName}";
                    if( Type.GetType( aqn, throwOnError: false ) == null )
                    {
                        // Before mutating, check that the O type exists.
                        var oTypeName = t.WrittenInfo.TypeName.Remove( 1, 9 );
                        aqn = $"{t.WrittenInfo.TypeNamespace}.{oTypeName}, {t.WrittenInfo.AssemblyName}";
                        if( Type.GetType( aqn, throwOnError: false ) != null )
                        {
                            // The O type exists.
                            // Use the resolution by type name instead of setting the target type:
                            // if other hooks set the type, it will have precedence.
                            t.SetLocalTypeName( oTypeName );
                        }
                    }
                }
            } );
        }

        /// <summary>
        /// Exposes the non null objects in _objects as a collection.
        /// </summary>
        sealed class AllCollection : IObservableAllObjectsCollection
        {
            readonly ObservableDomain _d;

            public AllCollection( ObservableDomain d )
            {
                _d = d;
            }

            public ObservableObject? this[long id] => this[new ObservableObjectId( id, false )];

            public ObservableObject? this[double id] => this[new ObservableObjectId( id, false )];

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

            public T? Get<T>( double id, bool throwOnTypeMismacth = true ) where T : ObservableObject => Get<T>( new ObservableObjectId( id, false ) );

            public IEnumerator<ObservableObject> GetEnumerator() => _d._objects.Take( _d._objectsListCount )
                                                                               .Where( o => o != null )
                                                                               .GetEnumerator()!;

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        /// <summary>
        /// Initializes a new <see cref="ObservableDomain"/> without any <see cref="DomainClient"/>.
        /// <para>
        /// Sidekicks are NOT instantiated by the constructors. If <see cref="HasWaitingSidekicks"/> is true, a null transaction
        /// can be done that will instantiate the required sidekicks (and initialize them with the <see cref="ISidekickClientObject{TSidekick}"/> objects
        /// if any).
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor used to log the construction of this domain. Cannot be null.</param>
        /// <param name="domainName">Name of the domain. Must not be null but can be empty.</param>
        /// <param name="startTimer">Whether to initially start the <see cref="TimeManager"/>.</param>
        /// <param name="serviceProvider">The service providers that will be used to resolve the <see cref="ObservableDomainSidekick"/> objects.</param>
        public ObservableDomain( IActivityMonitor monitor, string domainName, bool startTimer, IServiceProvider? serviceProvider = null )
            : this( monitor, domainName, startTimer, client: null, serviceProvider )
        {
        }

        /// <summary>
        /// Initializes a new <see cref="ObservableDomain"/> with a <see cref="DomainClient"/> an optionals explicit exporter, serializer
        /// and deserializer handlers.
        /// <para>
        /// Sidekicks are NOT instantiated by the constructors. If <see cref="HasWaitingSidekicks"/> is true, a null transaction
        /// can be done that will instantiate the required sidekicks (and initialize them with the <see cref="ISidekickClientObject{TSidekick}"/> objects
        /// if any).
        /// </para>
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
            : this( monitor, domainName, startTimer, client, CurrentTransactionStatus.Instantiating, serviceProvider, exporters: null )
        {
        }

        /// <summary>
        /// Initializes a previously <see cref="Save"/>d domain.
        /// <para>
        /// Sidekicks are NOT instantiated by the constructors. If <see cref="HasWaitingSidekicks"/> is true, a null transaction
        /// can be done that will instantiate the required sidekicks (and initialize them with the <see cref="ISidekickClientObject{TSidekick}"/> objects
        /// if any).
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor used to log the construction of this domain. Cannot be null.</param>
        /// <param name="domainName">Name of the domain. Must not be null but can be empty.</param>
        /// <param name="client">The observable client (head of the Chain of Responsibility) to use. Can be null.</param>
        /// <param name="stream">The input stream that must be valid.</param>
        /// <param name="serviceProvider">The service providers that will be used to resolve the <see cref="ObservableDomainSidekick"/> objects.</param>
        /// <param name="startTimer">
        /// Ensures that the <see cref="ObservableDomain.TimeManager"/> is running or stopped.
        /// When null, it keeps its restored state.
        /// </param>
        /// <param name="exporters">Optional exporters handler.</param>
        public ObservableDomain( IActivityMonitor monitor,
                                 string domainName,
                                 IObservableDomainClient? client,
                                 RewindableStream stream,
                                 IServiceProvider? serviceProvider = null,
                                 bool? startTimer = null,
                                 IExporterResolver? exporters = null )
            : this( monitor, domainName, startTimer: false, client, CurrentTransactionStatus.Deserializing, serviceProvider, exporters )
        {
            Throw.CheckNotNullArgument( stream );
            Throw.CheckData( stream.IsValid );

            var runtimeType = GetType();
            bool isNakedDomain = runtimeType == typeof( ObservableDomain );
            // This has been initialized and checked by the central constructor.
            Debug.Assert( _transactionStatus == CurrentTransactionStatus.Deserializing );
            Debug.Assert( isNakedDomain 
                          || new[] { typeof(ObservableDomain<> ), typeof( ObservableDomain<,> ), typeof( ObservableDomain<,,> ), typeof( ObservableDomain<,,,> ) }
                                .Contains( runtimeType.GetGenericTypeDefinition() ) );

            // Whether we are the naked domain or not, we deserialize from here:
            // specialized deserialization constructors will have their roots bound
            // by the deserialization and everything is in place.
            using( monitor.OpenInfo( $"Loading new {runtimeType} '{domainName}' from stream." ) )
            using( new InitializationTransaction( monitor, this, true ) )
            {
                DoLoad( monitor, stream, domainName, startTimer );
            }
            _transactionStatus = CurrentTransactionStatus.Regular;
        }

        ObservableDomain( IActivityMonitor monitor,
                          string domainName,
                          bool startTimer,
                          IObservableDomainClient? client,
                          CurrentTransactionStatus instantionKind,
                          IServiceProvider? serviceProvider,
                          IExporterResolver? exporters )
        {
            Throw.CheckNotNullArgument( monitor );
            // DomainName can be empty.
            Throw.CheckNotNullArgument( domainName );
            Debug.Assert( instantionKind == CurrentTransactionStatus.Instantiating || instantionKind == CurrentTransactionStatus.Deserializing );

            _pocoDirectory = serviceProvider?.GetService<PocoDirectory>( false );

            // This class should be sealed for the external world. But since ObservableDomain<T>...<T1,T2,T3,T4>
            // that are defined in this assembly needs to extend it, it cannot be sealed.
            // This may be refactored once with a public BaseObservableDomain base class (with private protected constructor) and a
            // public sealed ObservableDomain : BaseObservableDomain...
            var runtimeType = GetType();
            bool isNakedDomain = runtimeType == typeof( ObservableDomain );

            static bool IsAllowedSpecializedType( Type runtimeType )
            {
                if( !runtimeType.IsGenericType ) return false;
                var tGen = runtimeType.GetGenericTypeDefinition();
                return tGen == typeof( ObservableDomain<> )
                       || tGen == typeof( ObservableDomain<,> )
                       || tGen == typeof( ObservableDomain<,,> )
                       || tGen == typeof( ObservableDomain<,,,> );
            }

            if( !isNakedDomain && !IsAllowedSpecializedType( runtimeType ) )
            {
                Throw.InvalidOperationException( "ObservableDomain class must not be specialized." );
            }
            DomainName = domainName;
            _exporters = exporters ?? ExporterRegistry.Default;
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
            // The serializer context caches the serialization driver.
            _serializerContext = new BinarySerializerContext( BinarySerializer.DefaultSharedContext, serviceProvider );
            // The deserialization context exposes the services, including this domain, to the deserializer. 
            _deserializerContext = new BinaryDeserializerContext( BinaryDeserializer.DefaultSharedContext, serviceProvider );
            _deserializerContext.Services.Add( this );

            // If we are deserializing, we let the deserialization constructor conclude
            // and do nothing here.
            if( (_transactionStatus = instantionKind) == CurrentTransactionStatus.Instantiating )
            {
                // We are not called by the deserializer constructors: we are simply initializing a
                // new domain.
                _domainSecret = CreateSecret();
                if( startTimer ) _timeManager.DoStartOrStop( monitor, true );
                if( isNakedDomain )
                {
                    _transactionStatus = CurrentTransactionStatus.Regular;
                    monitor.Info( $"ObservableDomain '{DomainName}' created." );
                }
            }
        }

        static byte[] CreateSecret()
        {
            using( var c = new System.Security.Cryptography.Rfc2898DeriveBytes( Guid.NewGuid().ToString(), Guid.NewGuid().ToByteArray(), 1000 ) )
            {
                return c.GetBytes( DomainSecretKeyLength );
            }
        }

        /// <summary>
        /// Empty transaction object: must be used during initialization (for <see cref="CreateAndAddRoot{T}(InitializationTransaction)"/>
        /// to be called).
        /// <para>
        /// This ensures that a transaction exists and the thread static is set.
        /// </para>
        /// </summary>
        private protected sealed class InitializationTransaction : IInternalTransaction
        {
            readonly ObservableDomain _d;
            readonly ObservableDomain? _previousThreadDomain;
            readonly IInternalTransaction? _previousTran;
            readonly DateTime _startTime;
            readonly IActivityMonitor _monitor;
            readonly bool _enterWriteLock;

            /// <inheritdoc cref="InitializationTransaction"/>
            /// <param name="m">The monitor to use while this transaction is the current one.</param>
            /// <param name="d">The observable domain.</param>
            /// <param name="enterWriteLock">False to not enter and exit the write lock because it is already held.</param>
            public InitializationTransaction( IActivityMonitor m, ObservableDomain d, bool enterWriteLock )
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
            }
            IActivityMonitor IInternalTransaction.Monitor => _monitor;

            DateTime IInternalTransaction.StartTime => _startTime;

            void IInternalTransaction.AddError( Exception ex ) { }

            TransactionResult IInternalTransaction.Commit() => TransactionResult.EmptySuccess;
            
            /// <summary>
            /// Releases locks and restores initialization context.
            /// </summary>
            public void Dispose()
            {
                _monitor.CloseGroup();
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
        private protected T CreateAndAddRoot<T>( InitializationTransaction initializationContext ) where T : ObservableRootObject
        {
            Debug.Assert( _currentTran == initializationContext );
            var o = Activator.CreateInstance<T>();
            _roots.Add( o );
            return o;
        }

        /// <summary>
        /// Gets all the observable objects that this domain contains (roots included).
        /// These exposed objects are out of any transactions or reentrancy checks: they should not 
        /// be used outside of ModifyAsync or Read methods.
        /// </summary>
        public IObservableAllObjectsCollection AllObjects => _exposedObjects;

        /// <summary>
        /// Gets all the internal objects that this domain contains.
        /// These exposed objects are out of any transactions or reentrancy checks: they should not 
        /// be used outside of ModifyAsync or Read methods.
        /// </summary>
        public IReadOnlyCollection<InternalObject> AllInternalObjects => _exposedInternalObjects;

        /// <summary>
        /// Gets the root observable objects that this domain contains.
        /// These exposed objects are out of any transactions or reentrancy checks: they should not 
        /// be used outside of ModifyAsync or Read methods.
        /// </summary>
        public IReadOnlyList<ObservableRootObject> AllRoots => _roots;

        /// <summary>
        /// Gets the current transaction number.
        /// Incremented each time a transaction successfully ended, default to 0 until the first transaction is successfully committed.
        /// </summary>
        public int TransactionSerialNumber => _transactionSerialNumber;

        /// <summary>
        /// Gets the current commit time. Defaults to <see cref="DateTime.UtcNow"/> at the very beginning,
        /// when no transaction has been committed yet (and <see cref="TransactionSerialNumber"/> is 0).
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
        /// Gets the PocoDirectory. <see cref="HasPocoDirectory"/> must be true otherwise
        /// an <see cref="InvalidOperationException"/> is raised.
        /// This requires a ServiceProvider to be provided to the ObservableDomain constructor (of course,
        /// a PocoDirectory must be available).
        /// </summary>
        public PocoDirectory PocoDirectory
        {
            get
            {
                Throw.CheckState( "PocoDirectory requires a ServiceProvider to be provided to the ObservableDomain constructor.", _pocoDirectory != null );
                return _pocoDirectory;
            }
        }

        /// <summary>
        /// Gets whether the <see cref="PocoDirectory"/> is available.
        /// </summary>
        public bool HasPocoDirectory => _pocoDirectory != null;

        /// <summary>
        /// Gets whether this domain has been disposed.
        /// </summary>
        public bool IsDisposed => _transactionStatus == CurrentTransactionStatus.Disposing;

        // This is an internal only getter.
        internal CurrentTransactionStatus CurrentTransactionStatus => _transactionStatus;

        /// <summary>
        /// Gets whether one or more sidekick are waiting to be instantiated.
        /// </summary>
        public bool HasWaitingSidekicks => _sidekickManager.HasWaitingSidekick;

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
                    while( CloseGroup() ) ;
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
        /// When this is called, the Domain's lock is held in read mode: objects can be read (but no write/modifications
        /// should occur). A typical implementation is to capture any required domain object's state and use
        /// <see cref="TransactionDoneEventArgs.PostActions"/> or <see cref="TransactionDoneEventArgs.DomainPostActions"/>
        /// to post asynchronous actions (or to send commands thanks to <see cref="TransactionDoneEventArgs.SendCommand(in ObservableDomainCommand)"/>
        /// that will be processed by the sidekicks).
        /// </para>
        /// <para>
        /// Note that this is called on a successfully failed roll backed transaction: use <see cref="TransactionDoneEventArgs.RollbackedInfo"/>
        /// for information on the rolled back transaction.
        /// </para>
        /// <para>
        /// Exceptions raised by this method are collected in <see cref="TransactionResult.TransactionDoneErrors"/>.
        /// </para>
        /// </summary>
        public event EventHandler<TransactionDoneEventArgs>? TransactionDone;

        List<CKExceptionData>? RaiseTransactionEventResult( in TransactionDoneEventArgs result )
        {
            List<CKExceptionData>? errors = null;
            _inspectorEvent?.Invoke( result );
            var h = TransactionDone;
            if( h != null )
            {
                foreach( var d in h.GetInvocationList() )
                {
                    try
                    {
                        ((EventHandler<TransactionDoneEventArgs>)d).Invoke( this, result );
                    }
                    catch( Exception ex )
                    {
                        result.Monitor.Error( "Error while raising OnSuccessfulTransaction event.", ex );
                        if( errors == null ) errors = new List<CKExceptionData>();
                        errors.Add( CKExceptionData.CreateFrom( ex ) );
                    }
                }
            }
            _sidekickManager.OnTransactionDoneEvent( result, ref errors );
            return errors;
        }

        event Action<ITransactionDoneEvent>? IObservableDomainInspector.TransactionDone
        {
            add => _inspectorEvent += value;
            remove => _inspectorEvent -= value;
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
        /// <returns>True on success, false if timeout occurred or if this domain is disposed.</returns>
        public bool Export( TextWriter w, int milliSecondsTimeout = -1 )
        {
            if( _transactionStatus == CurrentTransactionStatus.Disposing
                || !_lock.TryEnterReadLock( milliSecondsTimeout ) )
            {
                return false;
            }
            try
            {
                if( _transactionStatus == CurrentTransactionStatus.Disposing ) return false;
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
                    target.EmitString( p.Value.PropertyName! );
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
        /// <para>
        /// This can be called directly or inside one of the ModifyAsync methods:
        /// <list type="bullet">
        ///     <item>
        ///     When called directly, sidekicks are not instantiated and <see cref="HasWaitingSidekicks"/> is true.
        ///     </item>
        ///     <item>
        ///     When called in a ModifyAsync context, sidekicks are instantiated and their side effects occur, changes
        ///     are tracked and events are available.
        ///     </item>
        /// </list>
        /// </para>
        /// <para>
        /// The stream must be valid and is left open (since we did not open it, we don't close it).
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use. Cannot be null.</param>
        /// <param name="stream">The rewindable input stream.</param>
        /// <param name="expectedLoadedName">
        /// Name of the domain that is saved in <paramref name="stream"/> and must be loaded. It can differ from this <see cref="DomainName"/>.
        /// Must not be null or empty.
        /// </param>
        /// <param name="millisecondsTimeout">
        /// The maximum number of milliseconds to wait for a read access before giving up.
        /// Wait indefinitely by default.
        /// </param>
        /// <param name="startTimer">
        /// Ensures that the <see cref="ObservableDomain.TimeManager"/> is running or stopped.
        /// When null, it keeps its previous state (it is initially stopped at domain creation) and then its current state is persisted.
        /// </param>
        /// <returns>True on success, false if timeout occurred or if this domain is disposed.</returns>
        public bool Load( IActivityMonitor monitor, RewindableStream stream, string expectedLoadedName, int millisecondsTimeout = -1, bool? startTimer = null )
        {
            Throw.CheckNotNullArgument( monitor );
            Throw.CheckNotNullArgument( stream );
            Throw.CheckData( stream.IsValid );
            Throw.CheckNotNullArgument( expectedLoadedName );
            if( _transactionStatus == CurrentTransactionStatus.Disposing ) return false;

            bool hasWriteLock = _lock.IsWriteLockHeld;
            if( !hasWriteLock && !_lock.TryEnterWriteLock( millisecondsTimeout ) ) return false;

            Debug.Assert( !hasWriteLock || _currentTran != null, "isWrite => _currentTran != null" );
            bool needFakeTran = _currentTran == null || _currentTran.Monitor != monitor;
            using( monitor.OpenInfo( $"Reloading domain '{DomainName}' (using {(needFakeTran ? "fake" : "current")} transaction) from rewindable '{stream.Kind}'." ) )
            {
                if( needFakeTran ) new InitializationTransaction( monitor, this, false );
                Debug.Assert( _currentTran != null );
                var realTran = _currentTran as Transaction;
                // Resets the flag to kindly handle totally stupid more than one Load in a transaction:
                // only the last successful one will be considered.
                if( realTran != null ) realTran._lastLoadStatus = CurrentTransactionStatus.Regular;
                try
                {
                    var status = DoLoad( monitor, stream, expectedLoadedName, startTimer );
                    if( realTran != null ) realTran._lastLoadStatus = status;
                    return true;
                }
                finally
                {
                    if( needFakeTran )
                    {
                        Debug.Assert( _currentTran != null );
                        _currentTran.Dispose();
                    }
                    if( !hasWriteLock ) _lock.ExitWriteLock();
                }
            }
        }

        /// <summary>
        /// Loads previously <see cref="Save"/>d objects into this domain.
        /// <para>
        /// This can be called directly or inside one of the ModifyAsync methods:
        /// <list type="bullet">
        ///     <item>
        ///     When called directly, sidekicks are not instantiated and <see cref="HasWaitingSidekicks"/> is true.
        ///     </item>
        ///     <item>
        ///     When called in a ModifyAsync context, sidekicks are instantiated and their side effects occur, changes
        ///     are tracked and events are available.
        ///     </item>
        /// </list>
        /// </para>
        /// <para>
        /// The stream must be valid and is left open (since we did not open it, we don't close it).
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use. Cannot be null.</param>
        /// <param name="stream">The input stream.</param>
        /// <param name="millisecondsTimeout">
        /// The maximum number of milliseconds to wait for a read access before giving up.
        /// Wait indefinitely by default.
        /// </param>
        /// <param name="startTimer">
        /// Ensures that the <see cref="ObservableDomain.TimeManager"/> is running or stopped.
        /// When null, it keeps its previous state (it is initially stopped at domain creation) and then its current state is persisted.
        /// </param>
        /// <returns>True on success, false if timeout occurred.</returns>
        public bool Load( IActivityMonitor monitor, RewindableStream stream, int millisecondsTimeout = -1, bool? startTimer = null )
        {
            return Load( monitor, stream, DomainName, millisecondsTimeout, startTimer );
        }

        /// <summary>
        /// Called by a deserializer's post action.
        /// Does nothing at this level (since we have no roots).
        /// </summary>
        private protected virtual void BindRoots()
        {
        }

        internal void StartOrStopTimeManager( bool start )
        {
            CheckWriteLock( null );
            Debug.Assert( _currentTran != null );
            _timeManager.DoStartOrStop( _currentTran.Monitor, start );
        }

        /// <summary>
        /// Gets the active domain on the current thread or throws an <see cref="InvalidOperationException"/> if there is none.
        /// </summary>
        /// <returns>The current domain.</returns>
        internal static ObservableDomain GetCurrentActiveDomain()
        {
            if( CurrentThreadDomain == null )
            {
                Throw.InvalidOperationException( "A transaction is required (Observable objects can be created only inside a transaction)." );
            }
            return CurrentThreadDomain;
        }

        internal void Register( InternalObject o )
        {
            Debug.Assert( o != null && o.ActualDomain == this && o.Prev == null && o.Next == null );
            CheckWriteLock( o );
            if( (o.Prev = _lastInternalObject) == null ) _firstInternalObject = o;
            else _lastInternalObject!.Next = o;
            _lastInternalObject = o;
            ++_internalObjectCount;
            SideEffectsRegister( o );
        }

        internal void Unregister( InternalObject o )
        {
            Debug.Assert( o.ActualDomain == this );
            if( _firstInternalObject == o ) _firstInternalObject = o.Next;
            else o.Prev!.Next = o.Next;
            if( _lastInternalObject == o ) _lastInternalObject = o.Prev;
            else o.Next!.Prev = o.Prev;
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
            if( _transactionStatus == CurrentTransactionStatus.Regular )
            {
                // Deserialization ctors don't call this Register method, BUT this Register
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
        /// This is called from Destroy calls (Unregister observable/internal objects), not from
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
            if( _transactionStatus == CurrentTransactionStatus.Regular ) _changeTracker.OnDisposeObject( o );
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void CheckDisposed()
        {
            if( _transactionStatus == CurrentTransactionStatus.Disposing ) ThrowOnDisposedDomain();
        }

        void ThrowOnDisposedDomain()
        {
            throw new ObservableDomainDisposedException( DomainName );
        }


        /// <summary>
        /// Disposes this domain.
        /// This method calls <see cref="ObtainDomainMonitor(int, bool)"/>. If possible, use <see cref="Dispose(IActivityMonitor)"/> with
        /// an available monitor.
        /// As usual with Dispose methods, this can be called multiple times.
        /// </summary>
        public void Dispose()
        {
            if( _transactionStatus != CurrentTransactionStatus.Disposing )
            {
                _timeManager.Timer.QuickStopBeforeDispose();
                _lock.EnterWriteLock();
                if( _transactionStatus != CurrentTransactionStatus.Disposing )
                {
                    using( var monitor = ObtainDomainMonitor() )
                    {
                        Debug.Assert( monitor != null );
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
            Throw.CheckNotNullArgument( monitor );
            if( _transactionStatus != CurrentTransactionStatus.Disposing )
            {
                _timeManager.Timer.QuickStopBeforeDispose();
                _lock.EnterWriteLock();
                if( _transactionStatus != CurrentTransactionStatus.Disposing )
                {
                    DoDispose( monitor );
                }
            }
        }

        void DoDispose( IActivityMonitor monitor )
        {
            Debug.Assert( _lock.IsWriteLockHeld );
            Debug.Assert( _transactionStatus != CurrentTransactionStatus.Disposing );
            using( monitor.OpenInfo( $"Disposing domain '{DomainName}'." ) )
            {
                bool executorRun = _domainPostActionExecutor.Stop();
                if( executorRun )
                {
                    monitor.Debug( "The running DomainPostActionExecutor has been asked to stop." );
                }
                DomainClient?.OnDomainDisposed( monitor, this );
                DomainClient = null;
                _transactionStatus = CurrentTransactionStatus.Disposing;

                // We call OnUnload on all the Observable and Internal objects
                // so they can free any external resources.
                UnloadDomain( monitor, true );
                _timeManager.Timer.Dispose();

                if( monitor != _domainMonitor && _domainMonitorLock.Wait( 0 ) )
                {
                    if( _domainMonitor != null ) _domainMonitor.MonitorEnd( "Disposing domain." );
                    _domainMonitorLock.Release();
                }
                monitor.Debug( "Exiting write lock." );
                _lock.ExitWriteLock();
                if( executorRun )
                {
                    monitor.Debug( "Waiting for DomainPostActionExecutor stopped." );
                    _domainPostActionExecutor.WaitStopped();
                }
                monitor.Info( $"Domain '{DomainName}' disposed." );
                // There is a race condition here. Read, ModifyAsync (and others)
                // may have also seen a false _transactionStatus and then try to acquire the lock.
                // If the race is won by this Dispose() thread, then the write lock is taken, released and
                // the lock itself should be disposed...
                //
                // There is 2 possibilities:
                // 1 - If the other thread acquire the lock between the _lock.ExitWriteLock (above) and
                //     the _lock.Dispose() below, the other threads may work on a disposed domain even
                //     if they had perfectly acquired the lock :(.
                // 2 - If the other thread continue their execution after the following _lock.Dispose(), they will
                //     try to acquire a disposed lock. An ObjectDisposedException should be thrown (that is somehow fine).
                //
                // The first solution seems to accept 2 (the disposed exception of the lock) and to detect 1 by
                // checking _disposed after each acquire: if CurrentTransactionStatus.Disposing then we must release the lock and
                // throw the ObjectDisposedException...
                // However, the _lock.Dispose() call below MAY occur while a TryEnter has been successful and before
                // the _transactionStatus check and the release: this would result in an awful "Incorrect Lock Dispose" exception
                // since disposing a lock while it is held is an error.
                // ==> This solution that seems the cleanest and most reasonable one is eventually NOT an option... 
                //
                // Another solution is to defer the actual Disposing. By using the timer for instance: the domain is "logically disposed"
                // but technically perfectly valid until a timer event calls a DoRealDispose(). If this call is made after a "long enough"
                // time, there is no more active thread in the domain since all the public API throws the ObjectDisposedException.
                //
                // Is this satisfying? No. Did I miss something? May be.
                //
                // A third solution is simply to...
                //   - not Dispose the _lock (and rely on the Garbage Collector to clean it)...
                //   - ...and to call CheckDisposed each time the lock is taken.
                // And we can notice that by doing this:
                //  - there is no risk to acquire a disposed lock.
                //  - the domain is 'technically' functional, except that:
                //       - The AutoTimer has been disposed right above, it may throw an ObjectDisposedException and that is fine.
                //       - The DomainClient has been set to null: no more side effect (like transaction rollback) can occur.
                // ==> As long as CheckDisposed is called right after each lock and throws an ObjectDisposedException, it's safe.
                //
                // Conclusion: We comment the following line.
                //
                //_lock.Dispose();
            }
        }

        internal void SendCommand( IDestroyable o, in ObservableDomainCommand command )
        {
            if( _transactionStatus != CurrentTransactionStatus.Regular )
            {
                Debug.Assert( _currentTran != null );
                Debug.Assert( nameof( DomainView.CurrentTransactionStatus ) == "CurrentTransactionStatus" );
                _currentTran.Monitor.Warn( $"Command '{command}' is sent while CurrentTransactionStatus is {_transactionStatus}. It is ignored." );
            }
            else
            {
                CheckWriteLock( o ).CheckDestroyed();
                _changeTracker.OnSendCommand( command );
            }
        }

        /// <summary>
        /// Sends a <see cref="SnapshotDomainCommand"/>.
        /// This must be called from inside a transaction.
        /// </summary>
        public void SendSnapshotCommand()
        {
            if( CurrentTransactionStatus.IsRegular() )
            {
                CheckWriteLock( null );
                _changeTracker.OnSendCommand( new ObservableDomainCommand( SnapshotDomainCommand ) );
            }
            else
            {
                Debug.Assert( _currentTran != null );
                _currentTran.Monitor.Warn( $"SendSnapshotCommand() called while CurrentTransactionStatus is {CurrentTransactionStatus}. It is ignored." );
            }
        }

        internal void EnsureSidekicks( IDestroyable o )
        {
            if( CurrentTransactionStatus.IsRegular() )
            {
                CheckWriteLock( o ).CheckDestroyed();
                Debug.Assert( _currentTran != null && _currentTran is not InitializationTransaction );
                _sidekickManager.CreateWaitingSidekicks( _currentTran.Monitor, _currentTran.AddError, false );
            }
        }

        internal ObservablePropertyChangedEventArgs? OnPropertyChanged( ObservableObject o, string propertyName, object? after )
        {
            if( !CurrentTransactionStatus.IsRegular() ) return null;
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
            if( !CurrentTransactionStatus.IsRegular() ) return null;
            CheckWriteLock( o ).CheckDestroyed();
            return _changeTracker.OnListRemoveAt( o, index );
        }

        internal ListSetAtEvent? OnListSetAt( ObservableObject o, int index, object value )
        {
            if( !CurrentTransactionStatus.IsRegular() ) return null;
            CheckWriteLock( o ).CheckDestroyed();
            return _changeTracker.OnListSetAt( o, index, value );
        }

        internal CollectionClearEvent? OnCollectionClear( ObservableObject o )
        {
            if( !CurrentTransactionStatus.IsRegular() ) return null;
            CheckWriteLock( o ).CheckDestroyed();
            return _changeTracker.OnCollectionClear( o );
        }

        internal ListInsertEvent? OnListInsert( ObservableObject o, int index, object? item )
        {
            if( !CurrentTransactionStatus.IsRegular() ) return null;
            CheckWriteLock( o ).CheckDestroyed();
            return _changeTracker.OnListInsert( o, index, item );
        }

        internal CollectionMapSetEvent? OnCollectionMapSet( ObservableObject o, object key, object? value )
        {
            if( !CurrentTransactionStatus.IsRegular() ) return null;
            CheckWriteLock( o ).CheckDestroyed();
            return _changeTracker.OnCollectionMapSet( o, key, value );
        }

        internal CollectionRemoveKeyEvent? OnCollectionRemoveKey( ObservableObject o, object key )
        {
            if( !CurrentTransactionStatus.IsRegular() ) return null;
            CheckWriteLock( o ).CheckDestroyed();
            return _changeTracker.OnCollectionRemoveKey( o, key );
        }

        internal CollectionAddKeyEvent? OnCollectionAddKey( ObservableObject o, object key )
        {
            if( !CurrentTransactionStatus.IsRegular() ) return null;
            CheckWriteLock( o ).CheckDestroyed();
            return _changeTracker.OnCollectionAddKey( o, key );
        }

        /// <summary>
        /// Checks that the writer lock is acquired.
        /// If not, it can be because no transaction has been started: InvalidOperationException( "A transaction is required." ),
        /// or because only a read lock is acquired: InvalidOperationException( "Concurrent access: only Read lock has been acquired." )
        /// and finally: InvalidOperationException( "Concurrent access: write lock must be acquired." ).
        /// <para>
        /// On success, the _currentTran is necessary not null.
        /// </para>
        /// </summary>
        [return: NotNullIfNotNull( "o" )]
        IDestroyable? CheckWriteLock( IDestroyable? o )
        {
            if( !_lock.IsWriteLockHeld )
            {
                // Since the lock is not held, we may be disposing or disposed.
                if( _transactionStatus == CurrentTransactionStatus.Disposing ) Throw.ObjectDisposedException( $"Domain {DomainName}" );
                if( _currentTran == null ) Throw.InvalidOperationException( "A transaction is required." );
                if( _lock.IsReadLockHeld ) Throw.InvalidOperationException( "Concurrent access: only Read lock has been acquired." );
                Throw.InvalidOperationException( "Concurrent access: write lock must be acquired." );
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
        /// <param name="restoreSidekicks">
        /// True to restore sidekicks. This is done outside of any transaction and should be avoided as much as possible:
        /// Sidekicks instantiation can have side effects that make no sense if a transaction is not available as it is
        /// always the case anywhere else but here.
        /// </param>
        /// <param name="milliSecondsTimeout">Optional timeout to wait for read or write lock.</param>
        /// <param name="useDebugMode">False to not activate <see cref="BinarySerializer.IsDebugMode"/>.</param>
        /// <returns>The current <see cref="LostObjectTracker"/>.</returns>
        public static LostObjectTracker IdempotenceSerializationCheck( IActivityMonitor monitor,
                                                                       ObservableDomain domain,
                                                                       bool restoreSidekicks = false,
                                                                       int milliSecondsTimeout = -1,
                                                                       bool useDebugMode = true )
        {
            using( monitor.OpenInfo( $"Idempotence check of '{domain.DomainName}'." ) )
            using( var s = new MemoryStream() )
            {
                if( !domain.Save( monitor, s, millisecondsTimeout: milliSecondsTimeout, debugMode: useDebugMode ) )
                {
                    Throw.Exception( "First Save failed: Unable to acquire lock." );
                }
                var originalBytes = s.ToArray();
                var originalTransactionSerialNumber = domain.TransactionSerialNumber;
                s.Position = 0;
                if( !domain.Load( monitor, RewindableStream.FromStream( s ), millisecondsTimeout: milliSecondsTimeout, startTimer: null ) )
                {
                    Throw.Exception( "Reload failed: Unable to acquire lock." );
                }
                if( restoreSidekicks && domain._sidekickManager.HasWaitingSidekick )
                {
                    domain._sidekickManager.CreateWaitingSidekicks( monitor, Util.ActionVoid, true );
                }
                using var checker = BinarySerializer.CreateCheckedWriteStream( originalBytes );
                if( !domain.Save( monitor, checker, millisecondsTimeout: milliSecondsTimeout, debugMode: useDebugMode ) )
                {
                    Throw.Exception( "Second Save failed: Unable to acquire lock." );
                }
                return domain.CurrentLostObjectTracker!;
            }
        }

    }
}
