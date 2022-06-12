using CK.Core;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using CK.BinarySerialization;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;

namespace CK.Observable.League
{
    /// <summary>
    /// The coordinator client is nearly the same as its base <see cref="StreamStoreClient"/> except
    /// that it is definitely bound to the always loaded Coordinator domain, that it must rebind the <see cref="Domain.Shell"/>
    /// to the managed domains on reload and that it handles some changes like the disposal of a Domain.
    /// <para>
    /// The other StreamStoreClient implementation is the <see cref="ObservableLeague.DomainClient"/> that drives the behavior
    /// of the managed domains.
    /// </para>
    /// </summary>
    internal class CoordinatorClient : StreamStoreClient, IObservableDomainAccess<Coordinator>
    {
        IManagedLeague? _league;
        IServiceProvider _serviceProvider;
        int? _optionsPropertyId;

        public CoordinatorClient( IActivityMonitor monitor, IStreamStore store, IServiceProvider serviceProvider )
            : base( String.Empty, store, next: null )
        {
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Gets the coordinator domain: this set by the <see cref="ObservableLeague.LoadAsync"/> right after
        /// having newed this client: this can't be done from the constructor since the domain restoration is
        /// an asynchronous operation.
        /// </summary>
        public ObservableDomain<Coordinator> Domain { get; internal set; }

        public override void OnTransactionCommit( in SuccessfulTransactionEventArgs c )
        {
            base.OnTransactionCommit( c );
            if( !_optionsPropertyId.HasValue ) _optionsPropertyId = c.FindPropertyId( nameof( CK.Observable.League.Domain.Options ) );
            HashSet<Domain>? touched = null;
            foreach( var e in c.Events )
            {
                if( e is NewObjectEvent n && n.Object is Domain dN )
                {
                    if( touched == null ) touched = new HashSet<Domain>();
                    touched.Add( dN );
                    break;
                }
                if( _optionsPropertyId.HasValue && e is PropertyChangedEvent p && p.PropertyId == _optionsPropertyId.Value && p.Object is Domain dP )
                {
                    if( touched == null ) touched = new HashSet<Domain>();
                    touched.Add( dP );
                    break;
                }
            }
            if( touched != null )
            {
                foreach( var d in touched )
                {
                    c.DomainPostActions.Add( ctx => d.Shell.SynchronizeOptionsAsync( ctx.Monitor, d.Options, nextActiveTime: null ) );
                }
            }
        }

        /// <summary>
        /// Gets the league. This is available (not null) once the initialization step
        /// is done: a first (asynchronous) load from the store has been done, the <see cref="Coordinator.Domains"/>
        /// have been associated to their shells and, eventually, the ObservableLeague itself is created.
        /// </summary>
        internal IManagedLeague League => _league!;

        internal void FinalizeConstruct( IManagedLeague league )
        {
            _league = league;
            Domain.Root.FinalizeConstruct( league );
        }

        protected override void DoLoadOrCreateFromSnapshot( IActivityMonitor monitor, [AllowNull]ref ObservableDomain d, bool restoring, bool? startTimer )
        {
            Debug.Assert( Domain == d );
            base.DoLoadOrCreateFromSnapshot( monitor, ref d, restoring, startTimer );
            if( _league != null ) Domain.Root.Initialize( monitor, _league );
        }

        protected override ObservableDomain DoDeserializeDomain( IActivityMonitor monitor, RewindableStream stream, bool? startTimer )
        {
            return new ObservableDomain<Coordinator>( monitor, String.Empty, this, stream, _serviceProvider, startTimer );
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

        Task<TransactionResult> IObservableDomainAccess<Coordinator>.ModifyAsync( IActivityMonitor monitor, Action<IActivityMonitor, IObservableDomain<Coordinator>> actions, int millisecondsTimeout, bool parallelDomainPostActions )
        {
            return Domain.ModifyAsync( monitor, () => actions.Invoke( monitor, Domain ), millisecondsTimeout, considerRolledbackAsFailure: parallelDomainPostActions );
        }
        
        Task<TransactionResult> IObservableDomainAccess<Coordinator>.ModifyThrowAsync( IActivityMonitor monitor, Action<IActivityMonitor, IObservableDomain<Coordinator>> actions, int millisecondsTimeout, bool parallelDomainPostActions )
        {
            return Domain.ModifyThrowAsync( monitor, () => actions.Invoke( monitor, Domain ), parallelDomainPostActions, millisecondsTimeout );
        }

        async Task<(TResult, TransactionResult)> IObservableDomainAccess<Coordinator>.ModifyThrowAsync<TResult>( IActivityMonitor monitor, Func<IActivityMonitor, IObservableDomain<Coordinator>, TResult> actions, int millisecondsTimeout, bool parallelDomainPostActions )
        {
            TResult r = default;
            var tr = await Domain.ModifyThrowAsync( monitor, () => r = actions.Invoke( monitor, Domain ), parallelDomainPostActions, millisecondsTimeout );
            return (r,tr);
        }

        Task<(Exception? OnStartTransactionError, TransactionResult Transaction)> IObservableDomainAccess<Coordinator>.ModifyNoThrowAsync( IActivityMonitor monitor, Action<IActivityMonitor, IObservableDomain<Coordinator>> actions, int millisecondsTimeout, bool parallelDomainPostActions )
        {
            return Domain.ModifyNoThrowAsync( monitor, () => actions.Invoke( monitor, Domain ), parallelDomainPostActions, millisecondsTimeout );
        }
        #endregion

    }
}
