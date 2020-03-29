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
                foreach( var d in client.Domain.Root.Domains.Values )
                {
                    var shell = new Shell( monitor, d.DomainName, store, d.RootTypes ) { Options = d.Options };
                    d.Initialize( shell );
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
        /// Reads the coordinator domain by protecting the <paramref name="reader"/> function in a <see cref="ObservableDomain.AcquireReadLock(int)"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="reader">The reader function.</param>
        /// <param name="millisecondsTimeout">
        /// The maximum number of milliseconds to wait for a read access before giving up. Wait indefinitely by default.
        /// </param>
        public void ReadCoordinator( IActivityMonitor monitor, Action<IActivityMonitor, IObservableDomain<Coordinator>> reader, int millisecondsTimeout = -1 )
        {
            using( _coordinator.Domain.AcquireReadLock( millisecondsTimeout ) )
            {
                reader.Invoke( monitor, _coordinator.Domain );
            }
        }

        /// <summary>
        /// Reads the domain by protecting the <paramref name="reader"/> function in a <see cref="ObservableDomain.AcquireReadLock(int)"/>.
        /// </summary>
        /// <typeparam name="T">The type of the information to read.</typeparam>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="reader">The reader function that projects read information into a <typeparamref name="T"/>.</param>
        /// <param name="millisecondsTimeout">
        /// The maximum number of milliseconds to wait for a read access before giving up. Wait indefinitely by default.
        /// </param>
        /// <returns>The information.</returns>
        public T Read<T>( IActivityMonitor monitor, Func<IActivityMonitor, IObservableDomain<Coordinator>, T> reader, int millisecondsTimeout = -1 )
        {
            using( _coordinator.Domain.AcquireReadLock( millisecondsTimeout ) )
            {
                return reader( monitor, _coordinator.Domain );
            }
        }

        /// <summary>
        /// Enables <see cref="Coordinator"/> domain edition.
        /// Any exceptions raised by <see cref="IObservableDomainClient.OnTransactionStart(IActivityMonitor,ObservableDomain, DateTime)"/> (at the start
        /// of the process) and by <see cref="TransactionResult.PostActions"/> (after the successful commit or the failure) are thrown by this method.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="actions">The actions to execute inside the Coordinator's domain transaction.</param>
        /// <param name="millisecondsTimeout">The maximum number of milliseconds to wait for a write access before giving up. Wait indefinitely by default.</param>
        /// <returns>
        /// The transaction result from <see cref="ObservableDomain.Modify"/>.
        /// <see cref="TransactionResult.Empty"/> when the lock has not been taken before <paramref name="millisecondsTimeout"/>.
        /// </returns>
        public Task<TransactionResult> ModifyCoordinatorAsync( IActivityMonitor monitor, Action<IActivityMonitor, IObservableDomain<Coordinator>> actions, int millisecondsTimeout = -1 )
        {
            return _coordinator.Domain.ModifyAsync( monitor, () => actions.Invoke( monitor, _coordinator.Domain ), millisecondsTimeout );
        }

        /// <summary>
        /// Enables <see cref="Coordinator"/> domain edition.
        /// This never throws: any exception outside the action's transaction is caught, logged and returned.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="actions">The actions to execute inside the ObservableDomain's transaction.</param>
        /// <param name="millisecondsTimeout">The maximum number of milliseconds to wait for a write access before giving up. Wait indefinitely by default.</param>
        /// <returns>
        /// Returns the transaction result (that may be <see cref="TransactionResult.Empty"/>) and any exception outside of the observable transaction itself.
        /// </returns>
        public Task<(TransactionResult, Exception)> SafeModifyCoordinatorAsync( IActivityMonitor monitor, Action<IActivityMonitor, IObservableDomain<Coordinator>> actions, int millisecondsTimeout )
        {
            return _coordinator.Domain.SafeModifyAsync( monitor, () => actions.Invoke( monitor, _coordinator.Domain ), millisecondsTimeout );
        }

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
                foreach( var shell in _domains.Values ) shell.ClosingLeague = true;
                _domains.Clear();
            }
        }

        IManagedDomain IManagedLeague.CreateDomain( IActivityMonitor monitor, string name, IReadOnlyList<string> rootTypes )
        {
            Debug.Assert( !_coordinator.Domain.IsDisposed, "Domain.Dispose requires the Write lock." );
            return _domains.AddOrUpdate( name,
                                         new Shell( monitor, name, _streamStore, rootTypes ),
                                         ( n, s ) => throw new Exception( $"Internal error: domain name '{n}' already exists." ) );
        }

        IManagedDomain IManagedLeague.RebindDomain( IActivityMonitor monitor, string name, IReadOnlyList<string> rootTypes, ManagedDomainOptions options )
        {
            return _domains.AddOrUpdate( name,
                                         n => new Shell( monitor, name, _streamStore, rootTypes ) { Options = options },
                                         ( n, s ) =>
                                         {
                                             if( !s.RootTypes.SequenceEqual( rootTypes ) )
                                             {
                                                 throw new Exception( $"Unable to rebind domain named '{n}', root types differ: existing are '{s.RootTypes.Concatenate()}', reloaded ones want to be '{rootTypes.Concatenate()}'." );
                                             }
                                             s.Options = options;
                                             return s;
                                         } );
        }

        void IManagedLeague.OnDestroy( IActivityMonitor monitor, IManagedDomain d )
        {
            _domains.TryRemove( d.DomainName, out var _ );
        }
    }
}
