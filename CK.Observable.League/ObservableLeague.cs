using CK.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CK.Observable.League
{
    public partial class ObservableLeague
    {
        readonly ConcurrentDictionary<string, Shell> _domains;
        readonly IStreamStore _streamStore;
        readonly ObservableDomain<Coordinator> _coordinator;
        SequentialEventHandlerAsyncSender<(string DomainName, string JsonEvent)> _jsonEvents;

        ObservableLeague( IStreamStore streamStore, ObservableDomain<Coordinator> coordinator, ConcurrentDictionary<string, Shell> domains )
        {
            _domains = domains;
            _streamStore = streamStore;
            _coordinator = coordinator;
        }

        public event SequentialEventHandlerAsync<(string DomainName, string JsonEvent)> JsonDomainEvents
        {
            add => _jsonEvents.Add( value );
            remove => _jsonEvents.Remove( value );
        }

        public static async Task<ObservableLeague?> LoadAsync( IActivityMonitor monitor, IStreamStore store )
        {
            var client = new StreamStoreClient( String.Empty, store );
            var coordinator = new ObservableDomain<Coordinator>( monitor, client.DomainName, client );
            try
            {
                await client.InitializeAsync( monitor, coordinator );
                // No need to acquire a read lock here.
                var domains = new ConcurrentDictionary<string, Shell>();
                foreach( var d in coordinator.Root.Domains.Values )
                {
                    var shell = new Shell( monitor, d.DomainName, store, d.RootTypes );
                    d.Shell = shell;
                    domains.TryAdd( d.DomainName, shell );
                }
                return new ObservableLeague( store, coordinator, domains );
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
        /// <returns>The managed domain or null.</returns>
        public IObservableDomainLoader? Find( string domainName ) => _domains.TryGetValue( domainName, out Shell shell ) ? shell : null;

        /// <summary>
        /// Creates and adds a domain shell.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="name">The new domain name.</param>
        /// <param name="rootTypes">The root types.</param>
        /// <returns>The shell.</returns>
        internal IManagedDomain CreateDomain( IActivityMonitor monitor, string name, IReadOnlyList<string> rootTypes )
        {
            var shell = new Shell( monitor, name, _streamStore, rootTypes );
            _domains.AddOrUpdate( shell.DomainName, shell, ( n, s ) => throw new Exception( "Internal error: domain collections lost synchronization." ) );
            return shell;
        }

        /// <summary>
        /// Enables <see cref="Coordinator"/> domain edition.
        /// </summary>
        /// <param name="monitor"></param>
        /// <param name="actions"></param>
        /// <param name="millisecondsTimeout"></param>
        /// <returns></returns>
        public async Task<(TransactionResult, Exception)> SafeModifyCoordinatorAsync( IActivityMonitor monitor, Action<IActivityMonitor, IObservableDomain<Coordinator>> actions, int millisecondsTimeout )
        {
            return await _coordinator.SafeModifyAsync( monitor, () => actions.Invoke( monitor, _coordinator ), millisecondsTimeout );
        }


    }
}
