using CK.Core;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Linq;

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

        public CoordinatorClient( IActivityMonitor monitor, IStreamStore store, IServiceProvider serviceProvider )
            : base( String.Empty, store, null )
        {
            Domain = new ObservableDomain<Coordinator>( monitor, String.Empty, this, serviceProvider );
        }

        public ObservableDomain<Coordinator> Domain { get; }

        public override void OnTransactionCommit( in SuccessfulTransactionEventArgs c )
        {
            base.OnTransactionCommit( c );
            Domain d = null;
            foreach( var e in c.Events )
            {
                if( e is NewObjectEvent n && n.Object is Domain dN )
                {
                    d = dN;
                    break;
                }
                if( e is PropertyChangedEvent p && p.PropertyName == nameof( CK.Observable.League.Domain.Options ) && p.Object is Domain dP )
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

        protected override void DoLoadFromSnapshot( IActivityMonitor monitor, ObservableDomain d )
        {
            Debug.Assert( Domain == d );
            base.DoLoadFromSnapshot( monitor, d );
            if( _league != null ) Domain.Root.Initialize( monitor, _league );
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
