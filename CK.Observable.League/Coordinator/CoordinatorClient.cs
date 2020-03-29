using CK.Core;
using System;
using System.Diagnostics;

namespace CK.Observable.League
{
    /// <summary>
    /// The coordinator client is nearly the same as the other <see cref="StreamStoreClient"/> except
    /// that it is definitely bound to the always loaded Coordinator domain, that it must rebind the <see cref="Domain.Shell"/>
    /// to the managed domains on reload and that it handles some changes like the disposal of a Domain.
    /// </summary>
    internal class CoordinatorClient : StreamStoreClient
    {
        IManagedLeague? _league;

        public CoordinatorClient( IActivityMonitor monitor, IStreamStore store )
            : base( String.Empty, store, null )
        {
            Domain = new ObservableDomain<Coordinator>( monitor, String.Empty, this );
        }

        public ObservableDomain<Coordinator> Domain { get; }

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

    }
}
