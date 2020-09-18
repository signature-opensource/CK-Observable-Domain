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

        ObservableLeague( IStreamStore streamStore, CoordinatorClient coordinator, ConcurrentDictionary<string, Shell> domains )
        {
            _domains = domains;
            _streamStore = streamStore;
            _coordinator = coordinator;
            // Associates the league to the coordinator. This finalizes the initialization of the league. 
            _coordinator.FinalizeConstruct( this );
        }

        /// <summary>
        /// Factory method for <see cref="ObservableLeague"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="store">The store to use.</param>
        /// <returns>A new league or null on error.</returns>
        public static async Task<ObservableLeague?> LoadAsync( IActivityMonitor monitor, IStreamStore store )
        {
            try
            {
                // The CoordinatorClient creates its ObservableDomain<Coordinator> domain.
                var client = new CoordinatorClient( monitor, store );
                // Async initialization here, just like other managed domains.
                await client.InitializeAsync( monitor, client.Domain );
                // No need to acquire a read lock here.
                var domains = new ConcurrentDictionary<string, Shell>( StringComparer.OrdinalIgnoreCase );
                IEnumerable<Domain> observableDomains = client.Domain.Root.Domains.Values;
                foreach( var d in observableDomains )
                {
                    var shell = Shell.Create( monitor, client, d.DomainName, store, d.RootTypes );
                    d.Initialize( shell );
                    domains.TryAdd( d.DomainName, shell );
                }
                // Shells have been created: we can create the whole structure.
                var o = new ObservableLeague( store, client, domains );
                // And immediately loads the domains that need to be.
                foreach( var d in observableDomains )
                {
                    await d.Shell.SynchronizeOptionsAsync( monitor, d.Options, d.HasActiveTimedEvents );
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
                                         Shell.Create( monitor, _coordinator, name, _streamStore, rootTypes ),
                                         ( n, s ) => throw new Exception( $"Internal error: domain name '{n}' already exists." ) );
        }

        IManagedDomain IManagedLeague.RebindDomain( IActivityMonitor monitor, string name, IReadOnlyList<string> rootTypes )
        {
            return _domains.AddOrUpdate( name,
                                         n => Shell.Create( monitor, _coordinator, name, _streamStore, rootTypes ),
                                         ( n, s ) =>
                                         {
                                             if( !s.RootTypes.SequenceEqual( rootTypes ) )
                                             {
                                                 throw new Exception( $"Unable to rebind domain named '{n}', root types differ: existing are '{s.RootTypes.Concatenate()}', reloaded ones want to be '{rootTypes.Concatenate()}'." );
                                             }
                                             return s;
                                         } );
        }

        void IManagedLeague.OnDestroy( IActivityMonitor monitor, IManagedDomain d )
        {
            _domains.TryRemove( d.DomainName, out var _ );
        }
    }
}
