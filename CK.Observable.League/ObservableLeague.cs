using CK.Core;
using CK.Text;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CK.Observable.League
{
    public partial class ObservableLeague : IManagedLeague
    {
        readonly ConcurrentDictionary<string, Shell> _domains;
        readonly IStreamStore _streamStore;
        readonly CoordinatorClient _coordinator;
        SequentialEventHandlerAsyncSender<(string DomainName, string JsonEvent)> _jsonEvents;

        ObservableLeague( IStreamStore streamStore, CoordinatorClient coordinator, ConcurrentDictionary<string, Shell> domains )
        {
            _domains = domains;
            _streamStore = streamStore;
            _coordinator = coordinator;
            // Associates the league to the coordinator. This finalizes the initialization of the league. 
            _coordinator.FinalizeConstruct( this );
        }

        public event SequentialEventHandlerAsync<(string DomainName, string JsonEvent)> JsonDomainEvents
        {
            add => _jsonEvents.Add( value );
            remove => _jsonEvents.Remove( value );
        }

        public static async Task<ObservableLeague?> LoadAsync( IActivityMonitor monitor, IStreamStore store )
        {
            try
            {
                // The CoordinatorClient creates its ObservableDomain<Coordinator> domain.
                var client = new CoordinatorClient( monitor, store );
                // Async initialization here, just like other managed domains.
                await client.InitializeAsync( monitor, client.Domain );
                // No need to acquire a read lock here.
                var domains = new ConcurrentDictionary<string, Shell>();
                foreach( var d in client.Domain.Root.Domains.Values )
                {
                    var shell = new Shell( monitor, d.DomainName, store, d.RootTypes ) { Options = d.Options };
                    d.Shell = shell;
                    domains.TryAdd( d.DomainName, shell );
                }
                return new ObservableLeague( store, client, domains );
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
        /// Enables <see cref="Coordinator"/> domain edition.
        /// </summary>
        /// <param name="monitor"></param>
        /// <param name="actions"></param>
        /// <param name="millisecondsTimeout"></param>
        /// <returns></returns>
        public async Task<(TransactionResult, Exception)> SafeModifyCoordinatorAsync( IActivityMonitor monitor, Action<IActivityMonitor, IObservableDomain<Coordinator>> actions, int millisecondsTimeout )
        {
            return await _coordinator.Domain.SafeModifyAsync( monitor, () => actions.Invoke( monitor, _coordinator.Domain ), millisecondsTimeout );
        }


        IManagedDomain IManagedLeague.CreateDomain( IActivityMonitor monitor, string name, IReadOnlyList<string> rootTypes )
        {
            return _domains.AddOrUpdate( name,
                                         new Shell( monitor, name, _streamStore, rootTypes ),
                                         ( n, s ) => throw new Exception( $"Internal error: domain name '{n}' already exists." ) );
        }

        IManagedDomain IManagedLeague.RebindDomain( IActivityMonitor monitor, string name, IReadOnlyList<string> rootTypes )
        {
            return _domains.AddOrUpdate( name,
                                         n => new Shell( monitor, name, _streamStore, rootTypes ),
                                         ( n, s ) =>
                                         {
                                             if( !s.RootTypes.SequenceEqual( rootTypes ) )
                                             {
                                                 throw new Exception( $"Unable to rebind domain named '{n}', root types differ: existing are '{s.RootTypes.Concatenate()}', reloaded ones want to be '{rootTypes.Concatenate()}'." );
                                             }
                                             return s;
                                         } );
        }
    }
}
