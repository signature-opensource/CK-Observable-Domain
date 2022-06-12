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

            IEnumerable<Domain>? touched = null;
            if( c.RollbackedInfo != null )
            {
                // We don't have any sidekick that may have interfered with our domains.
                // We have nothing to do.
                if( c.RollbackedInfo.IsSafeRollback ) return;
                Debug.Assert( c.RollbackedInfo.IsDangerousRollback );
                // Resynchronize all.
                touched = Domain.AllObjects.OfType<Domain>();
            }
            else
            {
                HashSet<Domain>? hashTouched = null;
                if( !_optionsPropertyId.HasValue ) _optionsPropertyId = c.FindPropertyId( nameof( CK.Observable.League.Domain.Options ) );
                foreach( var e in c.Events )
                {
                    if( e is NewObjectEvent n && n.Object is Domain dN )
                    {
                        if( hashTouched == null ) hashTouched = new HashSet<Domain>();
                        hashTouched.Add( dN );
                        break;
                    }
                    if( _optionsPropertyId.HasValue && e is PropertyChangedEvent p && p.PropertyId == _optionsPropertyId.Value && p.Object is Domain dP )
                    {
                        if( hashTouched == null ) hashTouched = new HashSet<Domain>();
                        hashTouched.Add( dP );
                        break;
                    }
                }
                touched = hashTouched;
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
        bool IObservableDomainAccess<Coordinator>.TryRead( IActivityMonitor monitor, Action<IActivityMonitor, IObservableDomain<Coordinator>> reader, int millisecondsTimeout )
        {
            var d = Domain;
            return d.TryRead( monitor, () => reader( monitor, d ), millisecondsTimeout );
        }

        bool IObservableDomainAccess<Coordinator>.TryRead<T>( IActivityMonitor monitor,
                                                              Func<IActivityMonitor, IObservableDomain<Coordinator>, T> reader,
                                                              [MaybeNullWhen(false)]out T result,
                                                              int millisecondsTimeout )
        {
            var d = Domain;
            return d.TryRead( monitor, () => reader( monitor, d ), out result, millisecondsTimeout );
        }

        Task<TransactionResult> IObservableDomainAccess<Coordinator>.ModifyAsync( IActivityMonitor monitor,
                                                                                  Action<IActivityMonitor,
                                                                                  IObservableDomain<Coordinator>> actions,
                                                                                  bool throwException,
                                                                                  int millisecondsTimeout,
                                                                                  bool considerRolledbackAsFailure,
                                                                                  bool parallelDomainPostActions,
                                                                                  bool waitForDomainPostActionsCompletion )
        {
            var d = Domain;
            return d.ModifyAsync( monitor,
                                  () => actions.Invoke( monitor, d ),
                                  throwException,
                                  millisecondsTimeout,
                                  considerRolledbackAsFailure,
                                  parallelDomainPostActions,
                                  waitForDomainPostActionsCompletion );
        }
        
        Task<TransactionResult> IObservableDomainAccess<Coordinator>.ModifyThrowAsync( IActivityMonitor monitor,
                                                                                       Action<IActivityMonitor, IObservableDomain<Coordinator>> actions,
                                                                                       int millisecondsTimeout,
                                                                                       bool considerRolledbackAsFailure,
                                                                                       bool parallelDomainPostActions,
                                                                                       bool waitForDomainPostActionsCompletion )
        {
            var d = Domain;
            return d.ModifyThrowAsync( monitor,
                                       () => actions.Invoke( monitor, d ),
                                       millisecondsTimeout,
                                       considerRolledbackAsFailure,
                                       parallelDomainPostActions,
                                       waitForDomainPostActionsCompletion );
        }

        Task<TResult> IObservableDomainAccess<Coordinator>.ModifyThrowAsync<TResult>( IActivityMonitor monitor,
                                                                                      Func<IActivityMonitor, IObservableDomain<Coordinator>, TResult> actions,
                                                                                      int millisecondsTimeout,
                                                                                      bool considerRolledbackAsFailure,
                                                                                      bool parallelDomainPostActions,
                                                                                      bool waitForDomainPostActionsCompletion )
        {
            var d = Domain;
            return d.ModifyThrowAsync( monitor,
                                       () => actions.Invoke( monitor, d ),
                                       millisecondsTimeout,
                                       considerRolledbackAsFailure,
                                       parallelDomainPostActions,
                                       waitForDomainPostActionsCompletion );
        }

        Task<TransactionResult> IObservableDomainAccess<Coordinator>.ModifyNoThrowAsync( IActivityMonitor monitor,
                                                                                         Action<IActivityMonitor, IObservableDomain<Coordinator>> actions,
                                                                                         int millisecondsTimeout,
                                                                                         bool considerRolledbackAsFailure,
                                                                                         bool parallelDomainPostActions,
                                                                                         bool waitForDomainPostActionsCompletion )
        {
            var d = Domain;
            return d.ModifyNoThrowAsync( monitor,
                                         () => actions.Invoke( monitor, d ),
                                         millisecondsTimeout,
                                         considerRolledbackAsFailure,
                                         parallelDomainPostActions,
                                         waitForDomainPostActionsCompletion );
        }
        #endregion

    }
}
