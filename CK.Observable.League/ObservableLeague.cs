using CK.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Observable.League
{
    /// <summary>
    /// Primary object that manages a bunch of <see cref="ObservableDomain"/>.
    /// To interact with existing domains, a <see cref="IObservableDomainLoader"/> must be obtained
    /// thanks to the <see cref="this[string]"/> accessor (or the more explicit <see cref="Find(string)"/> method).
    /// Creation and destruction of domains are under control of the <see cref="Coordinator"/> domain.
    /// </summary>
    public partial class ObservableLeague : IObservableLeague, IManagedLeague
    {
        readonly ConcurrentDictionary<string, Shell> _domains;
        readonly IStreamStore _streamStore;
        readonly IObservableDomainInitializer? _initializer;
        readonly CoordinatorClient _coordinator;
        readonly IServiceProvider _serviceProvider;

        static readonly SemaphoreSlim _loadAsyncLock = new SemaphoreSlim( 1 );

        ObservableLeague( IStreamStore streamStore,
                          IObservableDomainInitializer? initializer,
                          IServiceProvider serviceProvider,
                          CoordinatorClient coordinator,
                          ConcurrentDictionary<string, Shell> domains )
        {
            _domains = domains;
            _streamStore = streamStore;
            _initializer = initializer;
            _coordinator = coordinator;
            _serviceProvider = serviceProvider;
            // Associates the league to the coordinator. This finalizes the initialization of the league.
            _coordinator.FinalizeConstruct( this );
        }

        /// <summary>
        /// Factory method for <see cref="ObservableLeague"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="store">The store to use.</param>
        /// <param name="initializer">Optional initializer that will be called on new domains.</param>
        /// <param name="serviceProvider">
        /// The service provider used to instantiate <see cref="ObservableDomainSidekick"/> objects.
        /// When null, a dummy service provider (<see cref="EmptyServiceProvider.Default"/>) is provided to the domains.
        /// </param>
        /// <returns>A new league or null on error.</returns>
        public static async Task<ObservableLeague?> LoadAsync( IActivityMonitor monitor,
                                                               IStreamStore store,
                                                               IObservableDomainInitializer? initializer = null,
                                                               IServiceProvider? serviceProvider = null )
        {
            await _loadAsyncLock.WaitAsync();
            try
            {
                if( serviceProvider == null ) serviceProvider = EmptyServiceProvider.Default;
                try
                {
                    // The CoordinatorClient creates its ObservableDomain<Coordinator> domain.
                    var client = new CoordinatorClient( monitor, store, serviceProvider );
                    // Async initialization here, just like other managed domains.
                    // Contrary to other domains, it is created with an active timer (that may be stopped later if needed)
                    // and it is not submitted to the domain initializer (if any).
                    client.Domain = (ObservableDomain<OCoordinatorRoot>)await client.InitializeAsync( monitor,
                                                                                                      startTimer: true,
                                                                                                      createOnLoadError: false,
                                                                                                      ( m, startTimer ) => new ObservableDomain<OCoordinatorRoot>( m, String.Empty, startTimer, client, serviceProvider ),
                                                                                                      initializer: null );
                    // No need to acquire a read lock here.
                    var domains = new ConcurrentDictionary<string, Shell>( StringComparer.OrdinalIgnoreCase );
                    IEnumerable<ODomain> observableDomains = client.Domain.Root.Domains.Values;
                    foreach( var d in observableDomains )
                    {
                        var shell = Shell.Create( monitor, client, d.DomainName, store, initializer, serviceProvider, d.RootTypes );
                        d.Initialize( shell );
                        domains.TryAdd( d.DomainName, shell );
                    }
                    // Shells have been created: we can create the whole structure.
                    var o = new ObservableLeague( store, initializer, serviceProvider, client, domains );
                    monitor.Info( $"Created ObservableLeague #{o.GetHashCode()}." );
                    // And immediately loads the domains that need to be.
                    foreach( ODomain d in observableDomains )
                    {
                        await d.Shell.SynchronizeOptionsAsync( monitor, d.Options, d.NextActiveTime );
                    }
                    return o;
                }
                catch( Exception ex )
                {
                    monitor.Error( "Unable to initialize an ObservableLeague: loading Coordinator failed.", ex );
                }
                return null;
            }
            finally
            {
                _loadAsyncLock.Release();
            }
        }

        /// <inheritdoc />
        public IObservableDomainLoader? Find( string domainName ) => _domains.TryGetValue( domainName, out Shell shell ) ? shell : null;

        /// <inheritdoc />
        public IObservableDomainLoader? this[string domainName] => Find( domainName );

        /// <inheritdoc />
        public IObservableDomainAccess<OCoordinatorRoot> Coordinator => _coordinator;

        /// <summary>
        /// Closes this league. The coordinator's domain is saved and disposed and
        /// the map of the domain is cleared: existing <see cref="IObservableDomainLoader"/>
        /// can no more obtain new <see cref="IObservableDomainShell"/>.
        /// </summary>
        public async Task CloseAsync( IActivityMonitor monitor )
        {
            if( !_coordinator.Domain.IsDisposed )
            {
                using( monitor.OpenTrace( $"Closing ObservableLeague #{GetHashCode()}." ) )
                {
                    // No risk here: Dispose can be called multiple times.
                    _coordinator.Domain.Dispose();
                    // Since this saves the snapshot, there is no risk here.
                    int retryCount = 0;
                    for( ; ; )
                    {
                        if( !await _coordinator.SaveSnapshotAsync( monitor, true ) )
                        {
                            if( retryCount++ <= 3 )
                            {
                                monitor.Warn( $"Unable to save Coordinator snapshot. Retrying in {retryCount * 100} ms." );
                                await Task.Delay( retryCount * 100 );
                            }
                            else
                            {
                                monitor.Fatal( $"Unable to save Coordinator snapshot after 3 tries." );
                                break;
                            }
                        }
                        else
                        {
                            monitor.Info( "Coordinator domain snapshot saved." );
                            break;
                        }
                    }
                    // Setting the flag is safe as well as clearing the concurrent dictionary.
                    foreach( var shell in _domains.Values )
                    {
                        await shell.OnClosingLeagueAsync( monitor );
                    }
                    _domains.Clear();
                }
            }
        }

        IInternalManagedDomain IManagedLeague.CreateDomain( IActivityMonitor monitor, string name, IReadOnlyList<string> rootTypes )
        {
            Debug.Assert( !_coordinator.Domain.IsDisposed );
            return _domains.AddOrUpdate( name,
                                         Shell.Create( monitor, _coordinator, name, _streamStore, _initializer, _serviceProvider, rootTypes ),
                                         ( n, s ) => throw new InvalidOperationException( $"Internal error: domain name '{n}' already exists." ) );
        }

        IInternalManagedDomain IManagedLeague.RebindDomain( IActivityMonitor monitor, string name, IReadOnlyList<string> rootTypes )
        {
            return _domains.AddOrUpdate( name,
                                         n => Shell.Create( monitor, _coordinator, name, _streamStore, _initializer, _serviceProvider, rootTypes ),
                                         ( n, s ) =>
                                         {
                                             if( !s.RootTypeNames.SequenceEqual( rootTypes ) )
                                             {
                                                 throw new Exception( $"Unable to rebind domain named '{n}', root types differ: existing are '{s.RootTypeNames.Concatenate()}', reloaded ones want to be '{rootTypes.Concatenate()}'." );
                                             }
                                             return s;
                                         } );
        }

        /// <summary>
        /// Called from the coordinator: the domain's name is no more associated to the Shell.
        /// The <see cref="Shell.IsDestroyed"/> has been set to true: when the domain will no more be used,
        /// the <see cref="StreamStoreClient.ArchiveSnapshotAsync(IActivityMonitor)"/> will be called instead of <see cref="StreamStoreClient.SaveSnapshotAsync(IActivityMonitor)"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="d">The managed domain (ie. the Shell: we only need here its DomainName).</param>
        void IManagedLeague.OnDestroy( IActivityMonitor monitor, IInternalManagedDomain d )
        {
            _domains.TryRemove( d.DomainName, out var _ );
        }
    }
}
