using CK.Core;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Linq;
using System.IO;

namespace CK.Observable.League
{
    /// <summary>
    /// The coordinator client is nearly the same as its base <see cref="StreamStoreClient"/> except
    /// that it is definitely bound to the always loaded Coordinator domain, that it must rebind the <see cref="Domain.Shell"/>
    /// to the managed domains on reload and that it handles some changes like the disposal of a Domain.
    /// </summary>
    internal class CoordinatorClient : StreamStoreClient, IObservableDomainAccess<Coordinator>
    {
        IManagedLeague? _league;
        IServiceProvider _serviceProvider;
        int? _optionsPropertyId;

        public CoordinatorClient( IActivityMonitor monitor, IStreamStore store, IServiceProvider serviceProvider )
            : base( String.Empty, store, initializer: null, next: null )
        {
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Gets the coordinator domain: this set by the the <see cref="ObservableLeague.LoadAsync"/> right after
        /// having new'ed this client: this can't be done from the constructor since the domain restoration is
        /// an async operation.
        /// </summary>
        public ObservableDomain<Coordinator> Domain { get; internal set; }

        public override void OnTransactionCommit( in SuccessfulTransactionEventArgs c )
        {
            base.OnTransactionCommit( c );
            if( !_optionsPropertyId.HasValue ) _optionsPropertyId = c.FindPropertyId( nameof( CK.Observable.League.Domain.Options ) );
            if( _optionsPropertyId.HasValue )
            {
                Domain d = null;
                foreach( var e in c.Events )
                {
                    if( e is NewObjectEvent n && n.Object is Domain dN )
                    {
                        d = dN;
                        break;
                    }
                    if( e is PropertyChangedEvent p && p.PropertyId == _optionsPropertyId.Value && p.Object is Domain dP )
                    {
                        d = dP;
                        break;
                    }
                }
                if( d != null )
                {
                    c.PostActions.Add( ctx => d.Shell.SynchronizeOptionsAsync( ctx.Monitor, d.Options, hasActiveTimedEvents: null ) );
                }
            }
        }

        /// <summary>
        /// Gets the league. This is available (not null) once the initialization step
        /// is done: a first (async) load from the store has been done, the <see cref="Coordinator.Domains"/>
        /// have been associated to their shells and, eventually, the ObservableLeague itself is created.
        /// </summary>
        internal IManagedLeague League => _league!;

        internal void FinalizeConstruct( IManagedLeague league )
        {
            _league = league;
            Domain.Root.FinalizeConstruct( league );
        }

        protected override void DoLoadOrCreateFromSnapshot( IActivityMonitor monitor, ref ObservableDomain? d, bool restoring )
        {
            Debug.Assert( Domain == d );
            base.DoLoadOrCreateFromSnapshot( monitor, ref d, restoring );
            if( _league != null ) Domain.Root.Initialize( monitor, _league );
        }

        protected override ObservableDomain DoDeserializeDomain( IActivityMonitor monitor, Stream stream, Func<ObservableDomain, bool> loadHook )
        {
            return new ObservableDomain<Coordinator>( monitor, String.Empty, this, stream, leaveOpen: true, encoding: null, _serviceProvider, loadHook );
        }

        #region Coordinator: IObservableDomainAccess<Coordinator>.
        void IObservableDomainAccess<Coordinator>.Read( IActivityMonitor monitor, Action<IActivityMonitor, IObservableDomain<Coordinator>> reader, int millisecondsTimeout )
        {
            using( Domain.AcquireReadLock( millisecondsTimeout ) )
            {
                reader.Invoke( monitor, Domain );
            }
        }

        T IObservableDomainAccess<Coordinator>.Read<T>( IActivityMonitor monitor, Func<IActivityMonitor, IObservableDomain<Coordinator>, T> reader, int millisecondsTimeout )
        {
            using( Domain.AcquireReadLock( millisecondsTimeout ) )
            {
                return reader( monitor, Domain );
            }
        }

        Task<TransactionResult> IObservableDomainAccess<Coordinator>.ModifyAsync( IActivityMonitor monitor, Action<IActivityMonitor, IObservableDomain<Coordinator>> actions, int millisecondsTimeout )
        {
            return Domain.ModifyAsync( monitor, () => actions.Invoke( monitor, Domain ), millisecondsTimeout );
        }
        
        Task IObservableDomainAccess<Coordinator>.ModifyThrowAsync( IActivityMonitor monitor, Action<IActivityMonitor, IObservableDomain<Coordinator>> actions, int millisecondsTimeout )
        {
            return Domain.ModifyThrowAsync( monitor, () => actions.Invoke( monitor, Domain ), millisecondsTimeout );
        }

        Task<(TransactionResult, Exception)> IObservableDomainAccess<Coordinator>.ModifyNoThrowAsync( IActivityMonitor monitor, Action<IActivityMonitor, IObservableDomain<Coordinator>> actions, int millisecondsTimeout )
        {
            return Domain.ModifyNoThrowAsync( monitor, () => actions.Invoke( monitor, Domain ), millisecondsTimeout );
        }
        #endregion

    }
}
