using CK.BinarySerialization;
using CK.Core;
using CK.PerfectEvent;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Observable.League;

public partial class ObservableLeague
{
    /// <summary>
    /// This is one of the top class to understand how ObservableLeague works.
    /// This shell manages its ObservableDomain: it is the IObservableDomainLoader (that can load or unload the
    /// domain) and also the IManagedDomain with which <see cref="Coordinator"/>'s <see cref="ODomain"/> interact (like synchronizing the
    /// domain options).
    /// <para>
    /// This is the primary IObservableDomainShell: when the domain is loaded, IndependentShell that are the publicly exposed
    /// objects relay their calls to it. Casting this base non generic Shell into Shell{T}, Shell{T1,T2}, etc. adapts the different calls
    /// (MofifyAsync with Actions that take IObservableDomian{T}, IObservableDomian{T1,T2}, etc.) without further casts: this is why this
    /// "hidden" Shell also implements IObservableDomainShell (and Shell{T} implements IObservableDomainShell{T}, etc.).
    /// </para>
    /// <para>
    /// In the current implementation, the IndependentShell are not "strict" regarding their disposal: a disposed Shell continues to
    /// relay its calls to this Shell. This may be changed in the future (calling any stuff on a disposed IndependentShell may throw).
    /// </para>
    /// <para>
    /// This shell exists even when the domain is unloaded: its <see cref="Shell.Client"/> remains the same.
    /// </para>
    /// </summary>
    class Shell : IObservableDomainLoader, IObservableDomainShell, IInternalManagedDomain
    {
        readonly private protected DomainClient Client;
        readonly SemaphoreSlim? _loadLock;
        readonly IActivityMonitor _initialMonitor;
        readonly IObservableDomainAccess<OCoordinatorRoot> _coordinator;
        readonly IObservableDomainInitializer? _domainInitializer;
        Type? _domainType;
        Type[] _rootTypes;
        int _refCount;
        ObservableDomain? _domain;

        DateTime _nextActiveTime;
        bool _preLoaded;
        DomainLifeCycleOption _lifeCycleOption;

        private protected class IndependentShell : IObservableDomainShell, IObservableDomainInspector
        {
            // Exposes the Shell without disposed guard.
            readonly protected IObservableDomainShell Shell;
            readonly IActivityMonitor _monitor;
            Action<ITransactionDoneEvent>? _onSuccess;
            bool _isDisposed;

            public IndependentShell( Shell s, IActivityMonitor m )
            {
                Shell = s;
                _monitor = m;
            }

            internal IObservableDomainShell SafeShell
            {
                get
                {
                    ThrowOnDispose();
                    return Shell;
                }
            }

            void ThrowOnDispose()
            {
                if( _isDisposed ) throw new ObjectDisposedException( nameof( Shell ) );
            }

            string IObservableDomainShellBase.DomainName => Shell.DomainName;

            bool IObservableDomainShellBase.IsDestroyed => Shell.IsDestroyed;

            Task<bool> IObservableDomainShellBase.SaveAsync( IActivityMonitor monitor ) => Shell.SaveAsync( monitor );

            ValueTask<bool> IObservableDomainShellBase.DisposeAsync( IActivityMonitor monitor )
            {
                // Unconditionally unsubscribe.
                DomainInspector.TransactionDone -= OnSuccessfulTransactionRelay;
                _isDisposed = true;
                return Shell.DisposeAsync( monitor );
            }

            ValueTask IAsyncDisposable.DisposeAsync() => Shell.DisposeAsync( _monitor ).AsNonGenericValueTask();

            string? IObservableDomainShell.ExportToString( int millisecondsTimeout ) => Shell.ExportToString( millisecondsTimeout );

            Task<TransactionResult> IObservableDomainShell.ModifyAsync( IActivityMonitor monitor,
                                                                        Action<IActivityMonitor,
                                                                        IObservableDomain> actions,
                                                                        bool throwException,
                                                                        int millisecondsTimeout,
                                                                        bool considerRolledbackAsFailure,
                                                                        bool parallelDomainPostActions,
                                                                        bool waitForDomainPostActionsCompletion)
            {
                return Shell.ModifyAsync( monitor,
                                          actions,
                                          throwException,
                                          millisecondsTimeout,
                                          considerRolledbackAsFailure,
                                          parallelDomainPostActions,
                                          waitForDomainPostActionsCompletion );
            }

            Task<TransactionResult> IObservableDomainShell.ModifyThrowAsync( IActivityMonitor monitor,
                                                                             Action<IActivityMonitor, IObservableDomain> actions,
                                                                             int millisecondsTimeout,
                                                                             bool considerRolledbackAsFailure,
                                                                             bool parallelDomainPostActions,
                                                                             bool waitForDomainPostActionsCompletion )
            {
                return Shell.ModifyThrowAsync( monitor,
                                               actions,
                                               millisecondsTimeout,
                                               considerRolledbackAsFailure,
                                               parallelDomainPostActions,
                                               waitForDomainPostActionsCompletion );
            }

            Task<TResult> IObservableDomainShell.ModifyThrowAsync<TResult>( IActivityMonitor monitor,
                                                                            Func<IActivityMonitor, IObservableDomain, TResult> actions,
                                                                            int millisecondsTimeout,
                                                                            bool parallelDomainPostActions,
                                                                            bool waitForDomainPostActionsCompletion )
            {
                return Shell.ModifyThrowAsync( monitor,
                                               actions,
                                               millisecondsTimeout,
                                               parallelDomainPostActions,
                                               waitForDomainPostActionsCompletion );
            }

            Task<TransactionResult> IObservableDomainShell.TryModifyAsync( IActivityMonitor monitor,
                                                                           Action<IActivityMonitor, IObservableDomain> actions,
                                                                           int millisecondsTimeout,
                                                                           bool considerRolledbackAsFailure,
                                                                           bool parallelDomainPostActions,
                                                                           bool waitForDomainPostActionsCompletion )
            {
                return Shell.TryModifyAsync( monitor,
                                             actions,
                                             millisecondsTimeout,
                                             considerRolledbackAsFailure,
                                             parallelDomainPostActions,
                                             waitForDomainPostActionsCompletion );
            }

            bool IObservableDomainShell.TryRead( IActivityMonitor monitor, Action<IActivityMonitor, IObservableDomain> reader, int millisecondsTimeout )
            {
                return Shell.TryRead( monitor, reader, millisecondsTimeout );
            }

            bool IObservableDomainShell.TryRead<T>( IActivityMonitor monitor,
                                                    Func<IActivityMonitor, IObservableDomain, T> reader,
                                                    [MaybeNullWhen( false )] out T result,
                                                    int millisecondsTimeout )
            {
                return Shell.TryRead( monitor, reader, out result, millisecondsTimeout );
            }

            T IObservableDomainShell.Read<T>( IActivityMonitor monitor, Func<IActivityMonitor, IObservableDomain, T> reader )
            {
                return Shell.Read( monitor, reader );
            }

            void IObservableDomainShell.Read( IActivityMonitor monitor, Action<IActivityMonitor, IObservableDomain> reader )
            {
                Shell.Read( monitor, reader );
            }

            #region Inspector guarded relays.
            public IObservableDomainInspector DomainInspector
            {
                get
                {
                    ThrowOnDispose();
                    return this;
                }
            }

            ObservableDomain.LostObjectTracker? IObservableDomainInspector.CurrentLostObjectTracker
            {
                get
                {
                    ThrowOnDispose();
                    return DomainInspector.CurrentLostObjectTracker;
                }
            }

            ObservableDomain.LostObjectTracker? IObservableDomainInspector.EnsureLostObjectTracker( IActivityMonitor monitor, int millisecondsTimeout )
            {
                ThrowOnDispose();
                return DomainInspector.EnsureLostObjectTracker( monitor, millisecondsTimeout );
            }

            Task<bool> IObservableDomainInspector.GarbageCollectAsync( IActivityMonitor monitor, int millisecondsTimeout )
            {
                ThrowOnDispose();
                return DomainInspector.GarbageCollectAsync( monitor, millisecondsTimeout );
            }

            event Action<ITransactionDoneEvent>? IObservableDomainInspector.TransactionDone
            {
                add
                {
                    ThrowOnDispose();
                    bool mustReg = _onSuccess == null;
                    _onSuccess += value;
                    if( mustReg ) DomainInspector.TransactionDone += OnSuccessfulTransactionRelay;
                }
                remove
                {
                    ThrowOnDispose();
                    _onSuccess -= value;
                    if( _onSuccess == null ) DomainInspector.TransactionDone -= OnSuccessfulTransactionRelay;
                }
            }

            void OnSuccessfulTransactionRelay( ITransactionDoneEvent e ) => _onSuccess?.Invoke( e );

            #endregion
        }

        private protected Shell( IActivityMonitor monitor,
                                 IObservableDomainAccess<OCoordinatorRoot> coordinator,
                                 string domainName,
                                 IStreamStore store,
                                 IObservableDomainInitializer? initializer,
                                 IServiceProvider serviceProvider,
                                 IReadOnlyList<string> rootTypeNames,
                                 Type[] rootTypes,
                                 Type? domainType )
        {
            if( (_domainType = domainType) != null )
            {
                _loadLock = new SemaphoreSlim( 1 );
            }
            _coordinator = coordinator;
            _rootTypes = rootTypes;
            RootTypeNames = rootTypeNames;
            _initialMonitor = monitor;
            _domainInitializer = initializer;
            ServiceProvider = serviceProvider;
            Client = new DomainClient( domainName, store, this );
        }

        /// <summary>
        /// Synthesizes the Shell type (with the root types).
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="coordinator">The coordinator access.</param>
        /// <param name="domainName">The name of the domain.</param>
        /// <param name="store">The persistent store.</param>
        /// <param name="initializer">The optional domain initializer.</param>
        /// <param name="serviceProvider">The service provider used to instantiate <see cref="ObservableDomainSidekick"/> objects.</param>
        /// <param name="rootTypeNames">The root types.</param>
        /// <returns>The shell for the domain.</returns>
        internal static Shell Create( IActivityMonitor monitor,
                                      IObservableDomainAccess<OCoordinatorRoot> coordinator,
                                      string domainName,
                                      IStreamStore store,
                                      IObservableDomainInitializer? initializer,
                                      IServiceProvider serviceProvider,
                                      IReadOnlyList<string> rootTypeNames )
        {
            Type? domainType = null;
            Type[] rootTypes;
            if( rootTypeNames.Count == 0 )
            {
                domainType = typeof( ObservableDomain );
                rootTypes = Type.EmptyTypes;
            }
            else
            {
                bool success = true;
                rootTypes = new Type[rootTypeNames.Count];
                for( int i = 0; i < rootTypeNames.Count; ++i )
                {
                    var t = SimpleTypeFinder.WeakResolver( rootTypeNames[i], false );
                    if( t == null )
                    {
                        monitor.Error( $"Unable to resolve root type '{rootTypeNames[i]}' for domain '{domainName}'." );
                        success = false;
                    }
                    else
                    {
                        rootTypes[i] = t;
                    }
                }
                if( success )
                {
                    Type shellType = rootTypes.Length switch
                    {
                        1 => typeof( Shell<> ).MakeGenericType( rootTypes ),
                        2 => typeof( Shell<,> ).MakeGenericType( rootTypes ),
                        3 => typeof( Shell<,,> ).MakeGenericType( rootTypes ),
                        _ => typeof( Shell<,,,> ).MakeGenericType( rootTypes )
                    };
                    return (Shell)Activator.CreateInstance( shellType, monitor, coordinator, domainName, store, initializer, serviceProvider, rootTypeNames, rootTypes )!;
                }
            }
            // The domainType is null if the type resolution failed.
            return new Shell( monitor, coordinator, domainName, store, initializer, serviceProvider, rootTypeNames, rootTypes, domainType );
        }

        public string DomainName => Client.DomainName;

        public bool IsDestroyed { get; private set; }

        public IReadOnlyList<Type> RootTypes => _rootTypes;

        public IReadOnlyList<string> RootTypeNames { get; }

        public bool IsLoadable => _domainType != null;

        public bool IsLoaded => _refCount != 0;

        public (int TransactionNumber, IReadOnlyList<JsonEventCollector.TransactionEvent>? Events) GetTransactionEvents( int transactionNumber )
        {
            return Client.JsonEventCollector.GetTransactionEvents( transactionNumber );
        }

        public PerfectEvent<JsonEventCollector.TransactionEvent> DomainChanged => Client.JsonEventCollector.LastEventChanged;

        internal bool ClosingLeague { get; private set; }

        internal ValueTask OnClosingLeagueAsync( IActivityMonitor monitor )
        {
            ClosingLeague = true;
            return _preLoaded ? DoShellDisposeAsync( monitor ).AsNonGenericValueTask() : default;
        }

        /// <summary>
        /// Gets the options. This is set directly when the <see cref="Coordinator"/>'s <see cref="ODomain.Options"/>
        /// value changes.
        /// The different values are hold by this Client or directly by this shell.
        /// </summary>
        public ManagedDomainOptions Options
        {
            get => new ManagedDomainOptions( loadOption: _lifeCycleOption,
                                             c: Client.CompressionKind,
                                             skipTransactionCount: Client.SkipTransactionCount,
                                             snapshotSaveDelay: TimeSpan.FromMilliseconds( Client.SnapshotSaveDelay ),
                                             snapshotKeepDuration: Client.SnapshotKeepDuration,
                                             snapshotMaximalTotalKiB: Client.SnapshotMaximalTotalKiB,
                                             eventKeepDuration: Client.JsonEventCollector.KeepDuration,
                                             eventKeepLimit: Client.JsonEventCollector.KeepLimit,
                                             housekeepingRate: Client.HousekeepingRate );
        }

        void IInternalManagedDomain.Destroy( IActivityMonitor monitor, IManagedLeague league )
        {
            IsDestroyed = true;
            league.OnDestroy( monitor, this );
        }

        // This is called on PostActions of the Coordinator's domain modify.
        // If the domain must be loaded/created, the error is thrown so that the exception is captured and
        // a Coordinator.ModifyThrowAsync() actually throws.
        public async Task SynchronizeOptionsAsync( IActivityMonitor monitor, ManagedDomainOptions? options, DateTime? nextActiveTime )
        {
            if( options != null )
            {
                _lifeCycleOption = options.LifeCycleOption;
                Client.CompressionKind = options.CompressionKind;
                Client.SkipTransactionCount = options.SkipTransactionCount;
                Client.SnapshotSaveDelay = (int)options.SnapshotSaveDelay.TotalMilliseconds;
                Client.SnapshotKeepDuration = options.SnapshotKeepDuration;
                Client.SnapshotMaximalTotalKiB = options.SnapshotMaximalTotalKiB;
                Client.HousekeepingRate = options.HousekeepingRate;
                Client.JsonEventCollector.KeepDuration = options.ExportedEventKeepDuration;
                Client.JsonEventCollector.KeepLimit = options.ExportedEventKeepLimit;
            }
            if( nextActiveTime.HasValue ) _nextActiveTime = nextActiveTime.Value;
            bool shouldBeLoaded = IsLoadable
                                    && (_lifeCycleOption == DomainLifeCycleOption.Always
                                        || (_lifeCycleOption == DomainLifeCycleOption.HonorTimedEvent && _nextActiveTime != Util.UtcMinValue));
            if( _preLoaded != shouldBeLoaded )
            {
                _preLoaded = shouldBeLoaded;
                using( monitor.OpenDebug( $"The domain must be {(shouldBeLoaded ? "" : "un")}loaded (LifeCycleOption: {_lifeCycleOption}, NextActiveTime: {_nextActiveTime})." ) )
                {
                    if( shouldBeLoaded )
                    {
                        await DoShellLoadAsync( monitor, throwError: true, startTimer: null ).ConfigureAwait( false );
                    }
                    else
                    {
                        await DoShellDisposeAsync( monitor ).ConfigureAwait( false );
                    }
                }
            }
        }

        protected ObservableDomain LoadedDomain => _domain!;

        protected IServiceProvider ServiceProvider { get; }

        async Task<bool> IObservableDomainShellBase.SaveAsync( IActivityMonitor m )
        {
            return await ExplicitSnapshotDomainAsync( m ).ConfigureAwait( false )
                   && await Client.SaveSnapshotAsync( m, false ).ConfigureAwait( false );
        }

        enum SaveCommandOption
        {
            /// <summary>
            /// Default behavior is to trigger a snapshot, save it and triggers
            /// the housekeeping.
            /// </summary>
            SnapshotSaveAndHousekeep,

            /// <summary>
            /// Snapshot and save but don't trigger housekeeping.
            /// </summary>
            SnapshotAndSave,

            /// <summary>
            /// Snapshot only.
            /// </summary>
            SnapshotOnly
        }

        async Task<bool> ExplicitSnapshotDomainAsync( IActivityMonitor m )
        {
            // Today:
            SaveCommandOption saveOption = SaveCommandOption.SnapshotSaveAndHousekeep;

            using( m.OpenTrace( $"Snapshotting ObservableDomain {DomainName} manually." ) )
            {
                var d = _domain;
                if( d == null )
                {
                    m.Error( $"ObservableDomain {DomainName} is not loaded." );
                    return false;
                }
                if( d.IsDisposed )
                {
                    m.Error( $"ObservableDomain {DomainName} is disposed." );
                    return false;
                }
                if( Client.SkipTransactionCount == 0 && saveOption == SaveCommandOption.SnapshotOnly )
                {
                    m.Warn( $"ObservableDomain {DomainName} uses a {nameof( Client.SkipTransactionCount )} of 0. A snapshot is made on every transaction." );
                    return true;
                }

                var transaction = await d.TryModifyAsync( m, () => d.SendSnapshotCommand(), considerRolledbackAsFailure: false );
                if( !transaction.Success )
                {
                    m.Error( $"An unspecified error occurred while snapshotting the ObservableDomain {DomainName}." );
                    return false;
                }
                m.Trace( $"ObservableDomain {DomainName}: snapshot taken." );
                return true;
            }
        }

        ValueTask<bool> IObservableDomainShellBase.DisposeAsync( IActivityMonitor monitor ) => DoShellDisposeAsync( monitor );

        ValueTask IAsyncDisposable.DisposeAsync()
        {
            return DoShellDisposeAsync( new ActivityMonitor( $"Temporary monitor for disposing domain '{DomainName}'." ) )
                    .AsNonGenericValueTask();
        }

        async ValueTask<bool> DoShellDisposeAsync( IActivityMonitor monitor )
        {
            if( !IsLoadable ) return false;
            await _loadLock!.WaitAsync().ConfigureAwait( false );
            if( --_refCount < 0 )
            {
                monitor.Warn( "Disposing an already disposed ObservableLeague.Shell." );
                _loadLock.Release();
                return false;
            }
            bool disposedDomain = false;
            if( _refCount == 0 )
            {
                try
                {
                    if( _domain != null )
                    {
                        await ExplicitSnapshotDomainAsync( monitor ).ConfigureAwait( false );
                        if( IsDestroyed )
                        {
                            await Client.ArchiveSnapshotAsync( monitor ).ConfigureAwait( false );
                        }
                        else
                        {
                            await Client.SaveSnapshotAsync( monitor, doHouseKeeping: ClosingLeague ).ConfigureAwait( false );
                        }
                        if( !IsDestroyed && !ClosingLeague )
                        {
                            await _coordinator.ModifyThrowAsync( monitor, ( m, d ) =>
                            {
                                var domain = d.Root.Domains[DomainName];
                                domain.IsLoaded = false;
                                domain.NextActiveTime = _nextActiveTime;
                            }, parallelDomainPostActions: false ).ConfigureAwait( false );
                        }
                        disposedDomain = true;
                        Client.JsonEventCollector.Detach();
                        _domain.Dispose( monitor );
                    }
                }
                finally
                {
                    _domain = null;
                }
            }
            _loadLock.Release();
            return disposedDomain;
        }

        /// <summary>
        /// Primary load method (overloads call this one after having checked the root types).
        /// </summary>
        /// <param name="monitor">The monitor.</param>
        /// <param name="startTimer">TimeManager activation.</param>
        /// <returns>The shell.</returns>
        public async Task<IObservableDomainShell?> LoadAsync( IActivityMonitor monitor, bool? startTimer = null )
        {
            if( !IsLoadable || IsDestroyed || ClosingLeague ) return null;
            await DoShellLoadAsync( monitor, false, startTimer );
            if( _domain == null ) return null;
            if( _initialMonitor == monitor ) return this;
            return CreateIndependentShell( monitor );
        }

        async Task<bool> DoShellLoadAsync( IActivityMonitor monitor, bool throwError, bool? startTimer = null )
        {
            bool updateDone = false;
            await _loadLock!.WaitAsync();
            if( ++_refCount == 1 )
            {
                Debug.Assert( _domain == null );
                try
                {
                    var d = await Client.InitializeAsync( monitor, startTimer, createOnLoadError: true, CreateDomain, _domainInitializer );
                    await _coordinator.ModifyThrowAsync( monitor, ( m, d ) =>
                    {
                        var domain = d.Root.Domains[DomainName];
                        domain.IsLoaded = true;
                        domain.NextActiveTime = _nextActiveTime;
                    } );
                    updateDone = true;
                    _domain = d;
                }
                catch( Exception ex )
                {
                    Client.JsonEventCollector.Detach();
                    monitor.Error( $"Unable to instantiate and load '{DomainName}'.", ex );
                    _refCount = 0;
                    if( throwError )
                    {
                        _loadLock.Release();
                        throw;
                    }
                }
            }
            _loadLock.Release();

            return updateDone;
        }

        private protected virtual ObservableDomain CreateDomain( IActivityMonitor monitor, bool startTimer )
        {
            return new ObservableDomain( monitor, DomainName, startTimer, Client, ServiceProvider );
        }

        internal protected virtual ObservableDomain DeserializeDomain( IActivityMonitor monitor, RewindableStream stream, bool? startTimer )
        {
            return new ObservableDomain( monitor, DomainName, Client, stream, ServiceProvider, startTimer );
        }

        private protected virtual IObservableDomainShell CreateIndependentShell( IActivityMonitor monitor ) => new IndependentShell( this, monitor );

        public async Task<IObservableDomainShell<T>?> LoadAsync<T>( IActivityMonitor monitor, bool? startTimer = null ) where T : ObservableRootObject
        {
            if( !IsLoadable || IsDestroyed || ClosingLeague ) return null;
            if( _rootTypes.Length != 1 || !typeof( T ).IsAssignableFrom( _rootTypes[0] ) )
            {
                monitor.Error( $"Typed domain error: Domain {DomainName} cannot be loaded as a ObservableDomain<{typeof( T ).FullName}> (actual type is '{_domainType}')." );
                return null;
            }
            return (IObservableDomainShell<T>?)await LoadAsync( monitor, startTimer );
        }

        public async Task<IObservableDomainShell<T1, T2>?> LoadAsync<T1, T2>( IActivityMonitor monitor, bool? startTimer = null )
            where T1 : ObservableRootObject
            where T2 : ObservableRootObject
        {
            if( !IsLoadable || IsDestroyed || ClosingLeague ) return null;
            if( _rootTypes.Length != 2
                || !typeof( T1 ).IsAssignableFrom( _rootTypes[0] )
                || !typeof( T2 ).IsAssignableFrom( _rootTypes[1] ) )
            {
                monitor.Error( $"Typed domain error: Domain {DomainName} cannot be loaded as a ObservableDomain<{typeof( T1 ).FullName},{typeof( T2 ).FullName}> (actual type is '{_domainType}')." );
                return null;
            }
            return (IObservableDomainShell<T1, T2>?)await LoadAsync( monitor, startTimer );
        }

        public async Task<IObservableDomainShell<T1, T2, T3>?> LoadAsync<T1, T2, T3>( IActivityMonitor monitor, bool? startTimer = null )
            where T1 : ObservableRootObject
            where T2 : ObservableRootObject
            where T3 : ObservableRootObject
        {
            if( !IsLoadable || IsDestroyed || ClosingLeague ) return null;
            if( _rootTypes.Length != 3
                || !typeof( T1 ).IsAssignableFrom( _rootTypes[0] )
                || !typeof( T2 ).IsAssignableFrom( _rootTypes[1] )
                || !typeof( T3 ).IsAssignableFrom( _rootTypes[2] ) )
            {
                monitor.Error( $"Typed domain error: Domain {DomainName} cannot be loaded as a ObservableDomain<{typeof( T1 ).Name},{typeof( T2 ).Name},{typeof( T3 ).Name}> (actual type is '{_domainType}')." );
                return null;
            }
            return (IObservableDomainShell<T1, T2, T3>?)await LoadAsync( monitor, startTimer );
        }

        public async Task<IObservableDomainShell<T1, T2, T3, T4>?> LoadAsync<T1, T2, T3, T4>( IActivityMonitor monitor, bool? startTimer = null )
            where T1 : ObservableRootObject
            where T2 : ObservableRootObject
            where T3 : ObservableRootObject
            where T4 : ObservableRootObject
        {
            if( !IsLoadable || IsDestroyed || ClosingLeague ) return null;
            if( _rootTypes.Length != 4
                || !typeof( T1 ).IsAssignableFrom( _rootTypes[0] )
                || !typeof( T2 ).IsAssignableFrom( _rootTypes[1] )
                || !typeof( T3 ).IsAssignableFrom( _rootTypes[2] )
                || !typeof( T4 ).IsAssignableFrom( _rootTypes[3] ) )
            {
                monitor.Error( $"Typed domain error: Domain {DomainName} cannot be loaded as a ObservableDomain<{typeof( T1 ).Name},{typeof( T2 ).Name},{typeof( T3 ).Name},{typeof( T4 ).Name}> (actual type is '{_domainType}')." );
                return null;
            }
            return (IObservableDomainShell<T1, T2, T3, T4>?)await LoadAsync( monitor, startTimer );
        }

        #region IObservableDomainShell (non generic) implementation

        IObservableDomainInspector IObservableDomainShellBase.DomainInspector => LoadedDomain;

        string? IObservableDomainShell.ExportToString( int millisecondsTimeout )
        {
            var d = LoadedDomain;
            return d.ExportToString( millisecondsTimeout );
        }

        Task<TransactionResult> IObservableDomainShell.ModifyAsync( IActivityMonitor monitor,
                                                                    Action<IActivityMonitor, IObservableDomain> actions,
                                                                    bool throwException,
                                                                    int millisecondsTimeout,
                                                                    bool considerRolledbackAsFailure,
                                                                    bool parallelDomainPostActions,
                                                                    bool waitForDomainPostActionsCompletion)
        { 
            var d = LoadedDomain;
            return d.ModifyAsync( monitor,
                                  () => actions.Invoke( monitor, d ),
                                  throwException,
                                  millisecondsTimeout,
                                  considerRolledbackAsFailure,
                                  parallelDomainPostActions,
                                  waitForDomainPostActionsCompletion );
        }

        Task<TransactionResult> IObservableDomainShell.ModifyThrowAsync( IActivityMonitor monitor,
                                                                         Action<IActivityMonitor, IObservableDomain> actions,
                                                                         int millisecondsTimeout,
                                                                         bool considerRolledbackAsFailure,
                                                                         bool parallelDomainPostActions,
                                                                         bool waitForDomainPostActionsCompletion )
        {
            var d = LoadedDomain;
            return d.ModifyThrowAsync( monitor,
                                       () => actions.Invoke( monitor, d ),
                                       millisecondsTimeout,
                                       considerRolledbackAsFailure,
                                       parallelDomainPostActions,
                                       waitForDomainPostActionsCompletion );
        }

        Task<TResult> IObservableDomainShell.ModifyThrowAsync<TResult>( IActivityMonitor monitor,
                                                                        Func<IActivityMonitor, IObservableDomain, TResult> actions,
                                                                        int millisecondsTimeout,
                                                                        bool parallelDomainPostActions,
                                                                        bool waitForDomainPostActionsCompletion )
        {
            var d = LoadedDomain;
            return d.ModifyThrowAsync( monitor,
                                       () => actions.Invoke( monitor, d ),
                                       millisecondsTimeout,
                                       parallelDomainPostActions,
                                       waitForDomainPostActionsCompletion );
        }

        Task<TransactionResult> IObservableDomainShell.TryModifyAsync( IActivityMonitor monitor,
                                                                       Action<IActivityMonitor, IObservableDomain> actions,
                                                                       int millisecondsTimeout,
                                                                       bool considerRolledbackAsFailure,
                                                                       bool parallelDomainPostActions,
                                                                       bool waitForDomainPostActionsCompletion )
        {
            var d = LoadedDomain;
            return d.TryModifyAsync( monitor,
                                         () => actions.Invoke( monitor, d ),
                                         millisecondsTimeout,
                                         considerRolledbackAsFailure,
                                         parallelDomainPostActions,
                                         waitForDomainPostActionsCompletion );
        }

        bool IObservableDomainShell.TryRead(IActivityMonitor monitor, Action<IActivityMonitor, IObservableDomain> reader, int millisecondsTimeout)
        {
            var d = LoadedDomain;
            return d.TryRead( monitor, () => reader.Invoke( monitor, d ), millisecondsTimeout );
        }

        bool IObservableDomainShell.TryRead<T>( IActivityMonitor monitor,
                                                Func<IActivityMonitor, IObservableDomain, T> reader,
                                                [MaybeNullWhen(false)]out T result,
                                                int millisecondsTimeout )
        {
            var d = LoadedDomain;
            return d.TryRead( monitor, () => reader.Invoke( monitor, d ), out result, millisecondsTimeout );
        }

        TInfo IObservableDomainShell.Read<TInfo>( IActivityMonitor monitor, Func<IActivityMonitor, IObservableDomain, TInfo> reader )
        {
            var d = LoadedDomain;
            return d.Read( monitor, () => reader.Invoke( monitor, d ) );
        }

        void IObservableDomainShell.Read( IActivityMonitor monitor, Action<IActivityMonitor, IObservableDomain> reader )
        {
            var d = LoadedDomain;
            d.Read( monitor, () => reader.Invoke( monitor, d ) );
        }
        #endregion

    }

    sealed class Shell<T> : Shell, IObservableDomainShell<T> where T : ObservableRootObject
    {
        public Shell( IActivityMonitor monitor,
                      IObservableDomainAccess<OCoordinatorRoot> coordinator,
                      string domainName,
                      IStreamStore store,
                      IObservableDomainInitializer? initializer,
                      IServiceProvider serviceProvider,
                      IReadOnlyList<string> rootTypeNames,
                      Type[] rootTypes )
            : base( monitor, coordinator, domainName, store, initializer, serviceProvider, rootTypeNames, rootTypes, typeof( ObservableDomain<T> ) )
        {
        }

        private protected override ObservableDomain CreateDomain( IActivityMonitor monitor, bool startTimer )
        {
            return new ObservableDomain<T>( monitor, DomainName, startTimer, Client, ServiceProvider );
        }

        internal protected override ObservableDomain DeserializeDomain( IActivityMonitor monitor, RewindableStream stream, bool? startTimer )
        {
            return new ObservableDomain<T>( monitor, DomainName, Client, stream, ServiceProvider, startTimer );
        }

        sealed class IndependentShellT : IndependentShell, IObservableDomainShell<T>
        {
            public IndependentShellT( Shell<T> s, IActivityMonitor m )
                : base( s, m )
            {
            }

            new IObservableDomainShell<T> Shell => (Shell<T>)base.Shell;

            public Task<TransactionResult> ModifyAsync( IActivityMonitor monitor,
                                                        Action<IActivityMonitor, IObservableDomain<T>> actions,
                                                        bool throwException,
                                                        int millisecondsTimeout,
                                                        bool considerRolledbackAsFailure,
                                                        bool parallelDomainPostActions,
                                                        bool waitForDomainPostActionsCompletion )
            {
                return Shell.ModifyAsync( monitor,
                                          actions,
                                          throwException,
                                          millisecondsTimeout,
                                          considerRolledbackAsFailure,
                                          parallelDomainPostActions,
                                          waitForDomainPostActionsCompletion );
            }

            public Task<TransactionResult> ModifyThrowAsync( IActivityMonitor monitor,
                                                             Action<IActivityMonitor, IObservableDomain<T>> actions,
                                                             int millisecondsTimeout = -1,
                                                             bool considerRolledbackAsFailure = true,
                                                             bool parallelDomainPostActions = true,
                                                             bool waitForDomainPostActionsCompletion = false )
            {
                return Shell.ModifyThrowAsync( monitor,
                                               actions,
                                               millisecondsTimeout,
                                               considerRolledbackAsFailure,
                                               parallelDomainPostActions,
                                               waitForDomainPostActionsCompletion );
            }

            public Task<TResult> ModifyThrowAsync<TResult>( IActivityMonitor monitor,
                                                            Func<IActivityMonitor, IObservableDomain<T>, TResult> actions,
                                                            int millisecondsTimeout = -1,
                                                            bool parallelDomainPostActions = true,
                                                            bool waitForDomainPostActionsCompletion = false )
            {
                return Shell.ModifyThrowAsync( monitor,
                                               actions,
                                               millisecondsTimeout,
                                               parallelDomainPostActions,
                                               waitForDomainPostActionsCompletion );
            }

            public Task<TransactionResult> TryModifyAsync( IActivityMonitor monitor,
                                                           Action<IActivityMonitor, IObservableDomain<T>> actions,
                                                           int millisecondsTimeout = -1,
                                                           bool considerRolledbackAsFailure = true,
                                                           bool parallelDomainPostActions = true,
                                                           bool waitForDomainPostActionsCompletion = false )
            {
                return Shell.TryModifyAsync( monitor,
                                             actions,
                                             millisecondsTimeout,
                                             considerRolledbackAsFailure,
                                             parallelDomainPostActions,
                                             waitForDomainPostActionsCompletion );
            }

            public bool TryRead( IActivityMonitor monitor, Action<IActivityMonitor, IObservableDomain<T>> reader, int millisecondsTimeout = -1 )
            {
                return Shell.TryRead( monitor, reader, millisecondsTimeout );
            }

            public bool TryRead<TInfo>( IActivityMonitor monitor,
                                        Func<IActivityMonitor, IObservableDomain<T>, TInfo> reader,
                                        [MaybeNullWhen( false )] out TInfo result,
                                        int millisecondsTimeout )
            {
                return Shell.TryRead( monitor, reader, out result, millisecondsTimeout );
            }

            public TInfo Read<TInfo>( IActivityMonitor monitor,
                                      Func<IActivityMonitor, IObservableDomain<T>, TInfo> reader )
            {
                return Shell.Read( monitor, reader );
            }

            public void Read( IActivityMonitor monitor,
                              Action<IActivityMonitor, IObservableDomain<T>> reader )
            {
                Shell.Read( monitor, reader );
            }
        }

        private protected override IObservableDomainShell CreateIndependentShell( IActivityMonitor monitor ) => new IndependentShellT( this, monitor );

        new ObservableDomain<T> LoadedDomain => (ObservableDomain<T>)base.LoadedDomain;

        Task<TransactionResult> IObservableDomainAccess<T>.ModifyAsync( IActivityMonitor monitor,
                                                                        Action<IActivityMonitor, IObservableDomain<T>> actions,
                                                                        bool throwException,
                                                                        int millisecondsTimeout,
                                                                        bool considerRolledbackAsFailure,
                                                                        bool parallelDomainPostActions,
                                                                        bool waitForDomainPostActionsCompletion )
        {
            var d = LoadedDomain;
            return d.ModifyAsync( monitor,
                                  () => actions.Invoke( monitor, d ),
                                  throwException,
                                  millisecondsTimeout,
                                  considerRolledbackAsFailure,
                                  parallelDomainPostActions,
                                  waitForDomainPostActionsCompletion );
        }

        Task<TransactionResult> IObservableDomainAccess<T>.ModifyThrowAsync( IActivityMonitor monitor,
                                                                             Action<IActivityMonitor, IObservableDomain<T>> actions,
                                                                             int millisecondsTimeout,
                                                                             bool considerRolledbackAsFailure,
                                                                             bool parallelDomainPostActions,
                                                                             bool waitForDomainPostActionsCompletion )
        {
            var d = LoadedDomain;
            return d.ModifyThrowAsync( monitor,
                                       () => actions.Invoke( monitor, d ),
                                       millisecondsTimeout,
                                       considerRolledbackAsFailure,
                                       parallelDomainPostActions,
                                       waitForDomainPostActionsCompletion );
        }

        Task<TResult> IObservableDomainAccess<T>.ModifyThrowAsync<TResult>( IActivityMonitor monitor,
                                                                                  Func<IActivityMonitor, IObservableDomain<T>, TResult> actions,
                                                                                  int millisecondsTimeout,
                                                                                  bool parallelDomainPostActions,
                                                                                  bool waitForDomainPostActionsCompletion )
        {
            var d = LoadedDomain;
            return d.ModifyThrowAsync( monitor,
                                       () => actions.Invoke( monitor, d ),
                                       millisecondsTimeout,
                                       parallelDomainPostActions,
                                       waitForDomainPostActionsCompletion );
        }

        Task<TransactionResult> IObservableDomainAccess<T>.TryModifyAsync( IActivityMonitor monitor,
                                                                           Action<IActivityMonitor, IObservableDomain<T>> actions,
                                                                           int millisecondsTimeout,
                                                                           bool considerRolledbackAsFailure,
                                                                           bool parallelDomainPostActions,
                                                                           bool waitForDomainPostActionsCompletion )
        {
            var d = LoadedDomain;
            return d.TryModifyAsync( monitor,
                                         () => actions.Invoke( monitor, d ),
                                         millisecondsTimeout,
                                         considerRolledbackAsFailure,
                                         parallelDomainPostActions,
                                         waitForDomainPostActionsCompletion );
        }

        bool IObservableDomainAccess<T>.TryRead( IActivityMonitor monitor, Action<IActivityMonitor, IObservableDomain<T>> reader, int millisecondsTimeout )
        {
            var d = LoadedDomain;
            return d.TryRead( monitor, () => reader.Invoke( monitor, d ), millisecondsTimeout );
        }

        bool IObservableDomainAccess<T>.TryRead<TInfo>( IActivityMonitor monitor,
                                                        Func<IActivityMonitor, IObservableDomain<T>, TInfo> reader,
                                                        [MaybeNullWhen(false)]out TInfo result,
                                                        int millisecondsTimeout )
        {
            var d = LoadedDomain;
            return d.TryRead( monitor, () => reader.Invoke( monitor, d ), out result, millisecondsTimeout );
        }

        TInfo IObservableDomainAccess<T>.Read<TInfo>( IActivityMonitor monitor,
                                                      Func<IActivityMonitor, IObservableDomain<T>, TInfo> reader )
        {
            var d = LoadedDomain;
            return d.Read( monitor, () => reader.Invoke( monitor, d ) );
        }

        void IObservableDomainAccess<T>.Read( IActivityMonitor monitor,
                                              Action<IActivityMonitor, IObservableDomain<T>> reader )
        {
            var d = LoadedDomain;
            d.Read( monitor, () => reader.Invoke( monitor, d ) );
        }

    }

    sealed class Shell<T1, T2> : Shell, IObservableDomainShell<T1, T2>
        where T1 : ObservableRootObject
        where T2 : ObservableRootObject
    {
        public Shell( IActivityMonitor monitor,
                      IObservableDomainAccess<OCoordinatorRoot> coordinator,
                      string domainName,
                      IStreamStore store,
                      IObservableDomainInitializer? initializer,
                      IServiceProvider serviceProvider,
                      IReadOnlyList<string> rootTypeNames,
                      Type[] rootTypes )
            : base( monitor, coordinator, domainName, store, initializer, serviceProvider, rootTypeNames, rootTypes, typeof( ObservableDomain<T1, T2> ) )
        {
        }

        private protected override ObservableDomain CreateDomain( IActivityMonitor monitor, bool startTimer )
        {
            return new ObservableDomain<T1, T2>( monitor, DomainName, startTimer, Client, ServiceProvider );
        }

        internal protected override ObservableDomain DeserializeDomain( IActivityMonitor monitor, RewindableStream stream, bool? startTimer )
        {
            return new ObservableDomain<T1, T2>( monitor, DomainName, Client, stream, ServiceProvider, startTimer );
        }

        sealed class IndependentShellTT : IndependentShell, IObservableDomainShell<T1, T2>
        {
            public IndependentShellTT( Shell<T1, T2> s, IActivityMonitor m )
                : base( s, m )
            {
            }

            new IObservableDomainShell<T1, T2> Shell => (Shell<T1, T2>)base.Shell;

            public Task<TransactionResult> ModifyAsync( IActivityMonitor monitor,
                                                        Action<IActivityMonitor, IObservableDomain<T1, T2>> actions,
                                                        bool throwException,
                                                        int millisecondsTimeout,
                                                        bool considerRolledbackAsFailure,
                                                        bool parallelDomainPostActions,
                                                        bool waitForDomainPostActionsCompletion )
            {
                return Shell.ModifyAsync( monitor,
                                          actions,
                                          throwException,
                                          millisecondsTimeout,
                                          considerRolledbackAsFailure,
                                          parallelDomainPostActions,
                                          waitForDomainPostActionsCompletion );
            }

            public Task<TransactionResult> ModifyThrowAsync( IActivityMonitor monitor,
                                                             Action<IActivityMonitor, IObservableDomain<T1, T2>> actions,
                                                             int millisecondsTimeout = -1,
                                                             bool considerRolledbackAsFailure = true,
                                                             bool parallelDomainPostActions = true,
                                                             bool waitForDomainPostActionsCompletion = false )
            {
                return Shell.ModifyThrowAsync( monitor,
                                               actions,
                                               millisecondsTimeout,
                                               considerRolledbackAsFailure,
                                               parallelDomainPostActions,
                                               waitForDomainPostActionsCompletion );
            }

            public Task<TResult> ModifyThrowAsync<TResult>( IActivityMonitor monitor,
                                                            Func<IActivityMonitor, IObservableDomain<T1, T2>, TResult> actions,
                                                            int millisecondsTimeout = -1,
                                                            bool parallelDomainPostActions = true,
                                                            bool waitForDomainPostActionsCompletion = false )
            {
                return Shell.ModifyThrowAsync( monitor,
                                               actions,
                                               millisecondsTimeout,
                                               parallelDomainPostActions,
                                               waitForDomainPostActionsCompletion );
            }

            public Task<TransactionResult> TryModifyAsync( IActivityMonitor monitor,
                                                           Action<IActivityMonitor, IObservableDomain<T1, T2>> actions,
                                                           int millisecondsTimeout = -1,
                                                           bool considerRolledbackAsFailure = true,
                                                           bool parallelDomainPostActions = true,
                                                           bool waitForDomainPostActionsCompletion = false )
            {
                return Shell.TryModifyAsync( monitor,
                                             actions,
                                             millisecondsTimeout,
                                             considerRolledbackAsFailure,
                                             parallelDomainPostActions,
                                             waitForDomainPostActionsCompletion );
            }

            public bool TryRead( IActivityMonitor monitor, Action<IActivityMonitor, IObservableDomain<T1, T2>> reader, int millisecondsTimeout )
            {
                return Shell.TryRead( monitor, reader, millisecondsTimeout );
            }

            public bool TryRead<TInfo>( IActivityMonitor monitor,
                                        Func<IActivityMonitor, IObservableDomain<T1, T2>, TInfo> reader,
                                        [MaybeNullWhen( false )] out TInfo result,
                                        int millisecondsTimeout )
            {
                return Shell.TryRead( monitor, reader, out result, millisecondsTimeout );
            }

            public TInfo Read<TInfo>( IActivityMonitor monitor,
                                      Func<IActivityMonitor, IObservableDomain<T1, T2>, TInfo> reader )
            {
                return Shell.Read( monitor, reader );
            }
        }

        private protected override IObservableDomainShell CreateIndependentShell( IActivityMonitor monitor ) => new IndependentShellTT( this, monitor );

        new ObservableDomain<T1, T2> LoadedDomain => (ObservableDomain<T1, T2>)base.LoadedDomain;

        Task<TransactionResult> IObservableDomainShell<T1, T2>.ModifyAsync( IActivityMonitor monitor,
                                                                        Action<IActivityMonitor, IObservableDomain<T1, T2>> actions,
                                                                        bool throwException,
                                                                        int millisecondsTimeout,
                                                                        bool considerRolledbackAsFailure,
                                                                        bool parallelDomainPostActions,
                                                                        bool waitForDomainPostActionsCompletion )
        {
            var d = LoadedDomain;
            return d.ModifyAsync( monitor,
                                  () => actions.Invoke( monitor, d ),
                                  throwException,
                                  millisecondsTimeout,
                                  considerRolledbackAsFailure,
                                  parallelDomainPostActions,
                                  waitForDomainPostActionsCompletion );
        }

        Task<TransactionResult> IObservableDomainShell<T1, T2>.ModifyThrowAsync( IActivityMonitor monitor,
                                                                             Action<IActivityMonitor, IObservableDomain<T1, T2>> actions,
                                                                             int millisecondsTimeout,
                                                                             bool considerRolledbackAsFailure,
                                                                             bool parallelDomainPostActions,
                                                                             bool waitForDomainPostActionsCompletion )
        {
            var d = LoadedDomain;
            return d.ModifyThrowAsync( monitor,
                                       () => actions.Invoke( monitor, d ),
                                       millisecondsTimeout,
                                       considerRolledbackAsFailure,
                                       parallelDomainPostActions,
                                       waitForDomainPostActionsCompletion );
        }

        Task<TResult> IObservableDomainShell<T1, T2>.ModifyThrowAsync<TResult>( IActivityMonitor monitor,
                                                                                  Func<IActivityMonitor, IObservableDomain<T1, T2>, TResult> actions,
                                                                                  int millisecondsTimeout,
                                                                                  bool parallelDomainPostActions,
                                                                                  bool waitForDomainPostActionsCompletion )
        {
            var d = LoadedDomain;
            return d.ModifyThrowAsync( monitor,
                                       () => actions.Invoke( monitor, d ),
                                       millisecondsTimeout,
                                       parallelDomainPostActions,
                                       waitForDomainPostActionsCompletion );
        }

        Task<TransactionResult> IObservableDomainShell<T1, T2>.TryModifyAsync( IActivityMonitor monitor,
                                                                               Action<IActivityMonitor, IObservableDomain<T1, T2>> actions,
                                                                               int millisecondsTimeout,
                                                                               bool considerRolledbackAsFailure,
                                                                               bool parallelDomainPostActions,
                                                                               bool waitForDomainPostActionsCompletion )
        {
            var d = LoadedDomain;
            return d.TryModifyAsync( monitor,
                                     () => actions.Invoke( monitor, d ),
                                     millisecondsTimeout,
                                     considerRolledbackAsFailure,
                                     parallelDomainPostActions,
                                     waitForDomainPostActionsCompletion );
        }

        bool IObservableDomainShell<T1, T2>.TryRead( IActivityMonitor monitor, Action<IActivityMonitor, IObservableDomain<T1, T2>> reader, int millisecondsTimeout )
        {
            var d = LoadedDomain;
            return d.TryRead( monitor, () => reader.Invoke( monitor, d ), millisecondsTimeout );
        }

        bool IObservableDomainShell<T1, T2>.TryRead<TInfo>( IActivityMonitor monitor,
                                                        Func<IActivityMonitor, IObservableDomain<T1, T2>, TInfo> reader,
                                                        [MaybeNullWhen( false )] out TInfo result,
                                                        int millisecondsTimeout )
        {
            var d = LoadedDomain;
            return d.TryRead( monitor, () => reader.Invoke( monitor, d ), out result, millisecondsTimeout );
        }

        TInfo IObservableDomainShell<T1, T2>.Read<TInfo>( IActivityMonitor monitor,
                                                          Func<IActivityMonitor, IObservableDomain<T1, T2>, TInfo> reader )
        {
            var d = LoadedDomain;
            return d.Read( monitor, () => reader.Invoke( monitor, d ) );
        }
    }

    sealed class Shell<T1, T2, T3> : Shell, IObservableDomainShell<T1, T2, T3>
        where T1 : ObservableRootObject
        where T2 : ObservableRootObject
        where T3 : ObservableRootObject
    {
        public Shell( IActivityMonitor monitor,
                      IObservableDomainAccess<OCoordinatorRoot> coordinator,
                      string domainName,
                      IStreamStore store,
                      IObservableDomainInitializer? initializer,
                      IServiceProvider serviceProvider,
                      IReadOnlyList<string> rootTypeNames,
                      Type[] rootTypes )
            : base( monitor, coordinator, domainName, store, initializer, serviceProvider, rootTypeNames, rootTypes, typeof( ObservableDomain<T1, T2, T3> ) )
        {
        }

        private protected override ObservableDomain CreateDomain( IActivityMonitor monitor, bool startTimer )
        {
            return new ObservableDomain<T1, T2, T3>( monitor, DomainName, startTimer, Client, ServiceProvider );
        }

        internal protected override ObservableDomain DeserializeDomain( IActivityMonitor monitor, RewindableStream stream, bool? startTimer )
        {
            return new ObservableDomain<T1, T2, T3>( monitor, DomainName, Client, stream, ServiceProvider, startTimer );
        }

        sealed class IndependentShellTTT : IndependentShell, IObservableDomainShell<T1, T2, T3>
        {
            public IndependentShellTTT( Shell<T1, T2, T3> s, IActivityMonitor m )
                : base( s, m )
            {
            }

            new IObservableDomainShell<T1, T2, T3> Shell => (Shell<T1, T2, T3>)base.Shell;

            public Task<TransactionResult> ModifyAsync( IActivityMonitor monitor,
                                                        Action<IActivityMonitor, IObservableDomain<T1, T2, T3>> actions,
                                                        bool throwException,
                                                        int millisecondsTimeout,
                                                        bool considerRolledbackAsFailure,
                                                        bool parallelDomainPostActions,
                                                        bool waitForDomainPostActionsCompletion )
            {
                return Shell.ModifyAsync( monitor,
                                          actions,
                                          throwException,
                                          millisecondsTimeout,
                                          considerRolledbackAsFailure,
                                          parallelDomainPostActions,
                                          waitForDomainPostActionsCompletion );
            }

            public Task<TransactionResult> ModifyThrowAsync( IActivityMonitor monitor,
                                                             Action<IActivityMonitor, IObservableDomain<T1, T2, T3>> actions,
                                                             int millisecondsTimeout = -1,
                                                             bool considerRolledbackAsFailure = true,
                                                             bool parallelDomainPostActions = true,
                                                             bool waitForDomainPostActionsCompletion = false )
            {
                return Shell.ModifyThrowAsync( monitor,
                                               actions,
                                               millisecondsTimeout,
                                               considerRolledbackAsFailure,
                                               parallelDomainPostActions,
                                               waitForDomainPostActionsCompletion );
            }

            public Task<TResult> ModifyThrowAsync<TResult>( IActivityMonitor monitor,
                                                            Func<IActivityMonitor, IObservableDomain<T1, T2, T3>, TResult> actions,
                                                            int millisecondsTimeout = -1,
                                                            bool parallelDomainPostActions = true,
                                                            bool waitForDomainPostActionsCompletion = false )
            {
                return Shell.ModifyThrowAsync( monitor,
                                               actions,
                                               millisecondsTimeout,
                                               parallelDomainPostActions,
                                               waitForDomainPostActionsCompletion );
            }

            public Task<TransactionResult> TryModifyAsync( IActivityMonitor monitor,
                                                           Action<IActivityMonitor, IObservableDomain<T1, T2, T3>> actions,
                                                           int millisecondsTimeout = -1,
                                                           bool considerRolledbackAsFailure = true,
                                                           bool parallelDomainPostActions = true,
                                                           bool waitForDomainPostActionsCompletion = false )
            {
                return Shell.TryModifyAsync( monitor,
                                             actions,
                                             millisecondsTimeout,
                                             considerRolledbackAsFailure,
                                             parallelDomainPostActions,
                                             waitForDomainPostActionsCompletion );

            }

            public bool TryRead( IActivityMonitor monitor, Action<IActivityMonitor, IObservableDomain<T1, T2, T3>> reader, int millisecondsTimeout )
            {
                return Shell.TryRead( monitor, reader, millisecondsTimeout );
            }

            public bool TryRead<TInfo>( IActivityMonitor monitor,
                                        Func<IActivityMonitor, IObservableDomain<T1, T2, T3>, TInfo> reader,
                                        [MaybeNullWhen( false )] out TInfo result,
                                        int millisecondsTimeout )
            {
                return Shell.TryRead( monitor, reader, out result, millisecondsTimeout );
            }

            public TInfo Read<TInfo>( IActivityMonitor monitor,
                                      Func<IActivityMonitor, IObservableDomain<T1, T2, T3>, TInfo> reader )
            {
                return Shell.Read( monitor, reader );
            }

            public void Read( IActivityMonitor monitor,
                              Action<IActivityMonitor, IObservableDomain<T1, T2, T3>> reader )
            {
                Shell.Read( monitor, reader );
            }

        }

        private protected override IObservableDomainShell CreateIndependentShell( IActivityMonitor monitor ) => new IndependentShellTTT( this, monitor );


        new ObservableDomain<T1, T2, T3> LoadedDomain => (ObservableDomain<T1, T2, T3>)base.LoadedDomain;

        Task<TransactionResult> IObservableDomainShell<T1, T2, T3>.ModifyAsync( IActivityMonitor monitor,
                                                                                Action<IActivityMonitor, IObservableDomain<T1, T2, T3>> actions,
                                                                                bool throwException,
                                                                                int millisecondsTimeout,
                                                                                bool considerRolledbackAsFailure,
                                                                                bool parallelDomainPostActions,
                                                                                bool waitForDomainPostActionsCompletion )
        {
            var d = LoadedDomain;
            return d.ModifyAsync( monitor,
                                  () => actions.Invoke( monitor, d ),
                                  throwException,
                                  millisecondsTimeout,
                                  considerRolledbackAsFailure,
                                  parallelDomainPostActions,
                                  waitForDomainPostActionsCompletion );
        }

        Task<TransactionResult> IObservableDomainShell<T1, T2, T3>.ModifyThrowAsync( IActivityMonitor monitor,
                                                                                     Action<IActivityMonitor, IObservableDomain<T1, T2, T3>> actions,
                                                                                     int millisecondsTimeout,
                                                                                     bool considerRolledbackAsFailure,
                                                                                     bool parallelDomainPostActions,
                                                                                     bool waitForDomainPostActionsCompletion )
        {
            var d = LoadedDomain;
            return d.ModifyThrowAsync( monitor,
                                       () => actions.Invoke( monitor, d ),
                                       millisecondsTimeout,
                                       considerRolledbackAsFailure,
                                       parallelDomainPostActions,
                                       waitForDomainPostActionsCompletion );
        }

        Task<TResult> IObservableDomainShell<T1, T2, T3>.ModifyThrowAsync<TResult>( IActivityMonitor monitor,
                                                                                    Func<IActivityMonitor, IObservableDomain<T1, T2, T3>, TResult> actions,
                                                                                    int millisecondsTimeout,
                                                                                    bool parallelDomainPostActions,
                                                                                    bool waitForDomainPostActionsCompletion )
        {
            var d = LoadedDomain;
            return d.ModifyThrowAsync( monitor,
                                       () => actions.Invoke( monitor, d ),
                                       millisecondsTimeout,
                                       parallelDomainPostActions,
                                       waitForDomainPostActionsCompletion );
        }

        Task<TransactionResult> IObservableDomainShell<T1, T2, T3>.TryModifyAsync( IActivityMonitor monitor,
                                                                                   Action<IActivityMonitor, IObservableDomain<T1, T2, T3>> actions,
                                                                                   int millisecondsTimeout,
                                                                                   bool considerRolledbackAsFailure,
                                                                                   bool parallelDomainPostActions,
                                                                                   bool waitForDomainPostActionsCompletion )
        {
            var d = LoadedDomain;
            return d.TryModifyAsync( monitor,
                                     () => actions.Invoke( monitor, d ),
                                     millisecondsTimeout,
                                     considerRolledbackAsFailure,
                                     parallelDomainPostActions,
                                     waitForDomainPostActionsCompletion );
        }

        bool IObservableDomainShell<T1, T2, T3>.TryRead( IActivityMonitor monitor, Action<IActivityMonitor, IObservableDomain<T1, T2, T3>> reader, int millisecondsTimeout )
        {
            var d = LoadedDomain;
            return d.TryRead( monitor, () => reader.Invoke( monitor, d ), millisecondsTimeout );
        }

        bool IObservableDomainShell<T1, T2, T3>.TryRead<TInfo>( IActivityMonitor monitor,
                                                        Func<IActivityMonitor, IObservableDomain<T1, T2, T3>, TInfo> reader,
                                                        [MaybeNullWhen( false )] out TInfo result,
                                                        int millisecondsTimeout )
        {
            var d = LoadedDomain;
            return d.TryRead( monitor, () => reader.Invoke( monitor, d ), out result, millisecondsTimeout );
        }

        TInfo IObservableDomainShell<T1, T2, T3>.Read<TInfo>( IActivityMonitor monitor,
                                                              Func<IActivityMonitor, IObservableDomain<T1, T2, T3>, TInfo> reader )
        {
            var d = LoadedDomain;
            return d.Read( monitor, () => reader.Invoke( monitor, d ) );
        }

        void IObservableDomainShell<T1, T2, T3>.Read( IActivityMonitor monitor,
                                                      Action<IActivityMonitor, IObservableDomain<T1, T2, T3>> reader )
        {
            var d = LoadedDomain;
            d.Read( monitor, () => reader.Invoke( monitor, d ) );
        }
    }

    sealed class Shell<T1, T2, T3, T4> : Shell, IObservableDomainShell<T1, T2, T3, T4>
        where T1 : ObservableRootObject
        where T2 : ObservableRootObject
        where T3 : ObservableRootObject
        where T4 : ObservableRootObject
    {
        public Shell( IActivityMonitor monitor,
                      IObservableDomainAccess<OCoordinatorRoot> coordinator,
                      string domainName,
                      IStreamStore store,
                      IObservableDomainInitializer? initializer,
                      IServiceProvider serviceProvider,
                      IReadOnlyList<string> rootTypeNames,
                      Type[] rootTypes )
            : base( monitor, coordinator, domainName, store, initializer, serviceProvider, rootTypeNames, rootTypes, typeof( ObservableDomain<T1, T2, T3, T4> ) )
        {
        }

        private protected override ObservableDomain CreateDomain( IActivityMonitor monitor, bool startTimer )
        {
            return new ObservableDomain<T1, T2, T3, T4>( monitor, DomainName, startTimer, Client, ServiceProvider );
        }

        internal protected override ObservableDomain DeserializeDomain( IActivityMonitor monitor, RewindableStream stream, bool? startTimer )
        {
            return new ObservableDomain<T1, T2, T3, T4>( monitor, DomainName, Client, stream, ServiceProvider, startTimer );
        }

        sealed class IndependentShellTTTT : IndependentShell, IObservableDomainShell<T1, T2, T3, T4>
        {
            public IndependentShellTTTT( Shell<T1, T2, T3, T4> s, IActivityMonitor m )
                : base( s, m )
            {
            }

            new IObservableDomainShell<T1, T2, T3, T4> Shell => (Shell<T1, T2, T3, T4>)base.Shell;

            public Task<TransactionResult> ModifyAsync( IActivityMonitor monitor,
                                                        Action<IActivityMonitor, IObservableDomain<T1, T2, T3, T4>> actions,
                                                        bool throwException,
                                                        int millisecondsTimeout,
                                                        bool considerRolledbackAsFailure,
                                                        bool parallelDomainPostActions,
                                                        bool waitForDomainPostActionsCompletion )
            {
                return Shell.ModifyAsync( monitor,
                                          actions,
                                          throwException,
                                          millisecondsTimeout,
                                          considerRolledbackAsFailure,
                                          parallelDomainPostActions,
                                          waitForDomainPostActionsCompletion );
            }

            public Task<TransactionResult> ModifyThrowAsync( IActivityMonitor monitor,
                                                             Action<IActivityMonitor, IObservableDomain<T1, T2, T3, T4>> actions,
                                                             int millisecondsTimeout = -1,
                                                             bool considerRolledbackAsFailure = true,
                                                             bool parallelDomainPostActions = true,
                                                             bool waitForDomainPostActionsCompletion = false )
            {
                return Shell.ModifyThrowAsync( monitor,
                                               actions,
                                               millisecondsTimeout,
                                               considerRolledbackAsFailure,
                                               parallelDomainPostActions,
                                               waitForDomainPostActionsCompletion );
            }

            public Task<TResult> ModifyThrowAsync<TResult>( IActivityMonitor monitor,
                                                            Func<IActivityMonitor, IObservableDomain<T1, T2, T3, T4>, TResult> actions,
                                                            int millisecondsTimeout = -1,
                                                            bool parallelDomainPostActions = true,
                                                            bool waitForDomainPostActionsCompletion = false )
            {
                return Shell.ModifyThrowAsync( monitor,
                                               actions,
                                               millisecondsTimeout,
                                               parallelDomainPostActions,
                                               waitForDomainPostActionsCompletion );
            }

            public Task<TransactionResult> TryModifyAsync( IActivityMonitor monitor,
                                                           Action<IActivityMonitor, IObservableDomain<T1, T2, T3, T4>> actions,
                                                           int millisecondsTimeout = -1,
                                                           bool considerRolledbackAsFailure = true,
                                                           bool parallelDomainPostActions = true,
                                                           bool waitForDomainPostActionsCompletion = false )
            {
                return Shell.TryModifyAsync( monitor,
                                             actions,
                                             millisecondsTimeout,
                                             considerRolledbackAsFailure,
                                             parallelDomainPostActions,
                                             waitForDomainPostActionsCompletion );

            }

            public bool TryRead( IActivityMonitor monitor, Action<IActivityMonitor, IObservableDomain<T1, T2, T3, T4>> reader, int millisecondsTimeout )
            {
                return Shell.TryRead( monitor, reader, millisecondsTimeout );
            }

            public bool TryRead<TInfo>( IActivityMonitor monitor,
                                        Func<IActivityMonitor, IObservableDomain<T1, T2, T3, T4>, TInfo> reader,
                                        [MaybeNullWhen( false )] out TInfo result,
                                        int millisecondsTimeout )
            {
                return Shell.TryRead( monitor, reader, out result, millisecondsTimeout );
            }

            public TInfo Read<TInfo>( IActivityMonitor monitor,
                                      Func<IActivityMonitor, IObservableDomain<T1, T2, T3, T4>, TInfo> reader )
            {
                return Shell.Read( monitor, reader );
            }

            public void Read( IActivityMonitor monitor,
                              Action<IActivityMonitor, IObservableDomain<T1, T2, T3, T4>> reader )
            {
                Shell.Read( monitor, reader );
            }

        }

        private protected override IObservableDomainShell CreateIndependentShell( IActivityMonitor monitor ) => new IndependentShellTTTT( this, monitor );

        new ObservableDomain<T1, T2, T3, T4> LoadedDomain => (ObservableDomain<T1, T2, T3, T4>)base.LoadedDomain;

        Task<TransactionResult> IObservableDomainShell<T1, T2, T3, T4>.ModifyAsync( IActivityMonitor monitor,
                                                                                    Action<IActivityMonitor, IObservableDomain<T1, T2, T3, T4>> actions,
                                                                                    bool throwException,
                                                                                    int millisecondsTimeout,
                                                                                    bool considerRolledbackAsFailure,
                                                                                    bool parallelDomainPostActions,
                                                                                    bool waitForDomainPostActionsCompletion )
        {
            var d = LoadedDomain;
            return d.ModifyAsync( monitor,
                                  () => actions.Invoke( monitor, d ),
                                  throwException,
                                  millisecondsTimeout,
                                  considerRolledbackAsFailure,
                                  parallelDomainPostActions,
                                  waitForDomainPostActionsCompletion );
        }

        Task<TransactionResult> IObservableDomainShell<T1, T2, T3, T4>.ModifyThrowAsync( IActivityMonitor monitor,
                                                                                         Action<IActivityMonitor, IObservableDomain<T1, T2, T3, T4>> actions,
                                                                                         int millisecondsTimeout,
                                                                                         bool considerRolledbackAsFailure,
                                                                                         bool parallelDomainPostActions,
                                                                                         bool waitForDomainPostActionsCompletion )
        {
            var d = LoadedDomain;
            return d.ModifyThrowAsync( monitor,
                                       () => actions.Invoke( monitor, d ),
                                       millisecondsTimeout,
                                       considerRolledbackAsFailure,
                                       parallelDomainPostActions,
                                       waitForDomainPostActionsCompletion );
        }

        Task<TResult> IObservableDomainShell<T1, T2, T3, T4>.ModifyThrowAsync<TResult>( IActivityMonitor monitor,
                                                                                        Func<IActivityMonitor, IObservableDomain<T1, T2, T3, T4>, TResult> actions,
                                                                                        int millisecondsTimeout,
                                                                                        bool parallelDomainPostActions,
                                                                                        bool waitForDomainPostActionsCompletion )
        {
            var d = LoadedDomain;
            return d.ModifyThrowAsync( monitor,
                                       () => actions.Invoke( monitor, d ),
                                       millisecondsTimeout,
                                       parallelDomainPostActions,
                                       waitForDomainPostActionsCompletion );
        }

        Task<TransactionResult> IObservableDomainShell<T1, T2, T3, T4>.TryModifyAsync( IActivityMonitor monitor,
                                                                                       Action<IActivityMonitor, IObservableDomain<T1, T2, T3, T4>> actions,
                                                                                       int millisecondsTimeout,
                                                                                       bool considerRolledbackAsFailure,
                                                                                       bool parallelDomainPostActions,
                                                                                       bool waitForDomainPostActionsCompletion )
        {
            var d = LoadedDomain;
            return d.TryModifyAsync( monitor,
                                     () => actions.Invoke( monitor, d ),
                                     millisecondsTimeout,
                                     considerRolledbackAsFailure,
                                     parallelDomainPostActions,
                                     waitForDomainPostActionsCompletion );
        }

        bool IObservableDomainShell<T1, T2, T3, T4>.TryRead( IActivityMonitor monitor,
                                                             Action<IActivityMonitor, IObservableDomain<T1, T2, T3, T4>> reader,
                                                             int millisecondsTimeout )
        {
            var d = LoadedDomain;
            return d.TryRead( monitor, () => reader.Invoke( monitor, d ), millisecondsTimeout );
        }

        bool IObservableDomainShell<T1, T2, T3, T4>.TryRead<TInfo>( IActivityMonitor monitor,
                                                                    Func<IActivityMonitor, IObservableDomain<T1, T2, T3, T4>, TInfo> reader,
                                                                    [MaybeNullWhen( false )] out TInfo result,
                                                                    int millisecondsTimeout )
        {
            var d = LoadedDomain;
            return d.TryRead( monitor, () => reader.Invoke( monitor, d ), out result, millisecondsTimeout );
        }

        TInfo IObservableDomainShell<T1, T2, T3, T4>.Read<TInfo>( IActivityMonitor monitor,
                                                                  Func<IActivityMonitor, IObservableDomain<T1, T2, T3, T4>, TInfo> reader )
        {
            var d = LoadedDomain;
            return d.Read( monitor, () => reader.Invoke( monitor, d ) );
        }

        void IObservableDomainShell<T1, T2, T3, T4>.Read( IActivityMonitor monitor,
                                                          Action<IActivityMonitor, IObservableDomain<T1, T2, T3, T4>> reader )
        {
            var d = LoadedDomain;
            d.Read( monitor, () => reader.Invoke( monitor, d ) );
        }

    }

}
