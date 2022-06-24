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
    /// that it is definitely bound to the always loaded Coordinator domain, that it must rebind the <see cref="ODomain.Shell"/>
    /// to the managed domains on reload and that it handles some changes like the disposal of a Domain.
    /// <para>
    /// The other StreamStoreClient implementation is the <see cref="ObservableLeague.DomainClient"/> that drives the behavior
    /// of the managed domains.
    /// </para>
    /// </summary>
    internal class CoordinatorClient : StreamStoreClient, IObservableDomainAccess<OCoordinatorRoot>
    {
        IManagedLeague? _league;
        IServiceProvider _serviceProvider;
        int? _optionsPropertyId;

        static CoordinatorClient()
        {
            BinaryDeserializer.DefaultSharedContext.AddDeserializationHook( t =>
            {
                if( t.ReadInfo.TypeNamespace == "CK.Observable.League" )
                {
                    if( t.ReadInfo.TypeName == "Domain" )
                    {
                        t.SetTargetType( typeof( ODomain ) );
                    }
                    else if( t.ReadInfo.TypeName == "Coordinator" )
                    {
                        t.SetTargetType( typeof( OCoordinatorRoot ) );
                    }
                }
            } );
        }

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
        public ObservableDomain<OCoordinatorRoot> Domain { get; internal set; }

        public override void OnTransactionCommit( in TransactionDoneEventArgs c )
        {
            base.OnTransactionCommit( c );

            IEnumerable<ODomain>? touched = null;
            if( c.RollbackedInfo != null )
            {
                // We don't have any sidekick that may have interfered with our domains.
                // We have nothing to do.
                if( c.RollbackedInfo.IsSafeRollback ) return;
                Debug.Assert( c.RollbackedInfo.IsDangerousRollback );
                // Resynchronize all.
                touched = Domain.AllObjects.OfType<ODomain>();
            }
            else
            {
                HashSet<ODomain>? hashTouched = null;
                if( !_optionsPropertyId.HasValue ) _optionsPropertyId = c.FindPropertyId( nameof( CK.Observable.League.ODomain.Options ) );
                foreach( var e in c.Events )
                {
                    if( e is NewObjectEvent n && n.Object is ODomain dN )
                    {
                        if( hashTouched == null ) hashTouched = new HashSet<ODomain>();
                        hashTouched.Add( dN );
                        break;
                    }
                    if( _optionsPropertyId.HasValue && e is PropertyChangedEvent p && p.PropertyId == _optionsPropertyId.Value && p.Object is ODomain dP )
                    {
                        if( hashTouched == null ) hashTouched = new HashSet<ODomain>();
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
        /// is done: a first (asynchronous) load from the store has been done, the <see cref="OCoordinatorRoot.Domains"/>
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
            return new ObservableDomain<OCoordinatorRoot>( monitor, String.Empty, this, stream, _serviceProvider, startTimer );
        }

        #region Coordinator: IObservableDomainAccess<Coordinator>.
        bool IObservableDomainAccess<OCoordinatorRoot>.TryRead( IActivityMonitor monitor, Action<IActivityMonitor, IObservableDomain<OCoordinatorRoot>> reader, int millisecondsTimeout )
        {
            var d = Domain;
            return d.TryRead( monitor, () => reader( monitor, d ), millisecondsTimeout );
        }

        bool IObservableDomainAccess<OCoordinatorRoot>.TryRead<T>( IActivityMonitor monitor,
                                                              Func<IActivityMonitor, IObservableDomain<OCoordinatorRoot>, T> reader,
                                                              [MaybeNullWhen(false)]out T result,
                                                              int millisecondsTimeout )
        {
            var d = Domain;
            return d.TryRead( monitor, () => reader( monitor, d ), out result, millisecondsTimeout );
        }

        Task<TransactionResult> IObservableDomainAccess<OCoordinatorRoot>.ModifyAsync( IActivityMonitor monitor,
                                                                                  Action<IActivityMonitor,
                                                                                  IObservableDomain<OCoordinatorRoot>> actions,
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
        
        Task<TransactionResult> IObservableDomainAccess<OCoordinatorRoot>.ModifyThrowAsync( IActivityMonitor monitor,
                                                                                       Action<IActivityMonitor, IObservableDomain<OCoordinatorRoot>> actions,
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

        Task<TResult> IObservableDomainAccess<OCoordinatorRoot>.ModifyThrowAsync<TResult>( IActivityMonitor monitor,
                                                                                      Func<IActivityMonitor, IObservableDomain<OCoordinatorRoot>, TResult> actions,
                                                                                      int millisecondsTimeout,
                                                                                      bool parallelDomainPostActions,
                                                                                      bool waitForDomainPostActionsCompletion )
        {
            var d = Domain;
            return d.ModifyThrowAsync( monitor,
                                       () => actions.Invoke( monitor, d ),
                                       millisecondsTimeout,
                                       parallelDomainPostActions,
                                       waitForDomainPostActionsCompletion );
        }

        Task<TransactionResult> IObservableDomainAccess<OCoordinatorRoot>.TryModifyAsync( IActivityMonitor monitor,
                                                                                          Action<IActivityMonitor, IObservableDomain<OCoordinatorRoot>> actions,
                                                                                          int millisecondsTimeout,
                                                                                          bool considerRolledbackAsFailure,
                                                                                          bool parallelDomainPostActions,
                                                                                          bool waitForDomainPostActionsCompletion )
        {
            var d = Domain;
            return d.TryModifyAsync( monitor,
                                         () => actions.Invoke( monitor, d ),
                                         millisecondsTimeout,
                                         considerRolledbackAsFailure,
                                         parallelDomainPostActions,
                                         waitForDomainPostActionsCompletion );
        }
        #endregion

    }
}
