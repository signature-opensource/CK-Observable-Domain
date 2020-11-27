using CK.Core;
using CK.Text;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace CK.Observable.League
{
    /// <summary>
    /// Primary object that manages a bunch of <see cref="ObservableDomain"/>.
    /// To interact with existing domains, a <see cref="IObservableDomainLoader"/> must be obtained
    /// thanks to the <see cref="this[string]"/> accessor (or the more explicit <see cref="Find(string)"/> method).
    /// Creation and destruction of domains are under control of the <see cref="Coordinator"/> domain.
    /// </summary>
    public partial class ObservableLeague : IManagedLeague
    {
        readonly ConcurrentDictionary<string, Shell> _domains;
        readonly IStreamStore _streamStore;
        readonly CoordinatorClient _coordinator;
        readonly IServiceProvider _serviceProvider;

        ObservableLeague( IStreamStore streamStore,
                          IServiceProvider serviceProvider,
                          CoordinatorClient coordinator,
                          ConcurrentDictionary<string, Shell> domains )
        {
            _domains = domains;
            _streamStore = streamStore;
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
        /// <param name="serviceProvider">
        /// The service provider used to instantiate <see cref="ObservableDomainSidekick"/> objects.
        /// When null, a dummy service provider (<see cref="EmptyServiceProvider.Default"/>) is provided to the domains.
        /// </param>
        /// <returns>A new league or null on error.</returns>
        public static async Task<ObservableLeague?> LoadAsync( IActivityMonitor monitor,
                                                               IStreamStore store,
                                                               IServiceProvider? serviceProvider = null )
        {
            if( serviceProvider == null ) serviceProvider = EmptyServiceProvider.Default;
            try
            {
                // The CoordinatorClient creates its ObservableDomain<Coordinator> domain.
                var client = new CoordinatorClient( monitor, store, serviceProvider );
                // Async initialization here, just like other managed domains.
                // Contrary to other domains, it is created with an active timer (that may be stopped later if needed).
                client.Domain = (ObservableDomain<Coordinator>)await client.InitializeAsync( monitor, startTimer: true, (m,startTimer) => new ObservableDomain<Coordinator>(m, String.Empty, startTimer, client, serviceProvider));
                // No need to acquire a read lock here.
                var domains = new ConcurrentDictionary<string, Shell>( StringComparer.OrdinalIgnoreCase );
                IEnumerable<Domain> observableDomains = client.Domain.Root.Domains.Values;
                foreach( var d in observableDomains )
                {
                    var shell = Shell.Create( monitor, client, d.DomainName, store, serviceProvider, d.RootTypes );
                    d.Initialize( shell );
                    domains.TryAdd( d.DomainName, shell );
                }
                // Shells have been created: we can create the whole structure.
                var o = new ObservableLeague( store, serviceProvider, client, domains );
                // And immediately loads the domains that need to be.
                foreach( Domain d in observableDomains )
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

        /// <summary>
        /// Finds an existing domain.
        /// </summary>
        /// <param name="domainName">The domain name to find.</param>
        /// <returns>The managed domain or null if not found.</returns>
        public IObservableDomainLoader? Find( string domainName ) => _domains.TryGetValue( domainName, out Shell shell ) ? shell : null;

        /// <summary>
        /// Shortcut of <see cref="Find(string)"/>.
        /// </summary>
        /// <param name="domainName">The domain name to find.</param>
        /// <returns>The managed domain or null if not found.</returns>
        public IObservableDomainLoader? this[string domainName] => Find( domainName );

        /// <summary>
        /// Gets the access to the Coordinator domain.
        /// </summary>
        public IObservableDomainAccess<Coordinator> Coordinator => _coordinator;

        /// <summary>
        /// Closes this league. The coordinator's domain is saved and disposed and 
        /// the map of the domain is cleared: existing <see cref="IObservableDomainLoader"/>
        /// can no more obtain new <see cref="IObservableDomainShell"/>.
        /// </summary>
        public async Task CloseAsync( IActivityMonitor monitor )
        {
            if( !_coordinator.Domain.IsDisposed )
            {
                // No risk here: Dispose can be called multiple times.
                _coordinator.Domain.Dispose();
                // Since this saves the snapshot, there is no risk here.
                await _coordinator.SaveAsync( monitor );
                // Setting the flag is safe as well as clearing the concurrent dictionary.
                foreach( var shell in _domains.Values )
                {
                    await shell.OnClosingLeagueAsync( monitor );
                }
                _domains.Clear();
            }
        }

        IManagedDomain IManagedLeague.CreateDomain( IActivityMonitor monitor, string name, IReadOnlyList<string> rootTypes )
        {
            Debug.Assert( !_coordinator.Domain.IsDisposed );
            return _domains.AddOrUpdate( name,
                                         Shell.Create( monitor, _coordinator, name, _streamStore, _serviceProvider, rootTypes ),
                                         ( n, s ) => throw new Exception( $"Internal error: domain name '{n}' already exists." ) );
        }

        IManagedDomain IManagedLeague.RebindDomain( IActivityMonitor monitor, string name, IReadOnlyList<string> rootTypes )
        {
            return _domains.AddOrUpdate( name,
                                         n => Shell.Create( monitor, _coordinator, name, _streamStore, _serviceProvider, rootTypes ),
                                         ( n, s ) =>
                                         {
                                             if( !s.RootTypes.SequenceEqual( rootTypes ) )
                                             {
                                                 throw new Exception( $"Unable to rebind domain named '{n}', root types differ: existing are '{s.RootTypes.Concatenate()}', reloaded ones want to be '{rootTypes.Concatenate()}'." );
                                             }
                                             return s;
                                         } );
        }

        /// <summary>
        /// Called from the coordinator: the domain's name is no more associated to the Shell.
        /// The <see cref="Shell.IsDestroyed"/> has been set to true: when the domain will no more be used,
        /// the <see cref="StreamStoreClient.ArchiveAsync(IActivityMonitor)"/> will be called instead of <see cref="StreamStoreClient.SaveAsync(IActivityMonitor)"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="d">The managed domain (ie. the Shell: we only need here its DomainName).</param>
        void IManagedLeague.OnDestroy( IActivityMonitor monitor, IManagedDomain d )
        {
            _domains.TryRemove( d.DomainName, out var _ );
        }
    }
}
