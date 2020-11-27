using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace CK.Observable
{
    /// <summary>
    /// Encapsulates the result of a successful <see cref="ObservableDomain.Transaction.Commit"/>.
    /// This is available from <see cref="IObservableDomainClient.OnTransactionCommit(in SuccessfulTransactionEventArgs)"/>
    /// and <see cref="ObservableDomain.OnSuccessfulTransaction"/>.
    /// </summary>
    public class SuccessfulTransactionEventArgs : EventMonitoredArgs 
    {
        readonly ObservableDomain _domain;
        readonly Func<string, int?> _propertyId;
        internal readonly ActionRegistrar<PostActionContext> _domainPostActions;
        internal readonly ActionRegistrar<PostActionContext> _localPostActions;
        internal readonly List<ObservableDomainCommand> _commands;

        /// <summary>
        /// Gets the observable domain.
        /// </summary>
        public IObservableDomain Domain => _domain;

        /// <summary>
        /// Gets the start time (UTC) of the transaction.
        /// </summary>
        public DateTime StartTimeUtc { get; }

        /// <summary>
        /// Gets the time (UTC) of the transaction commit.
        /// </summary>
        public DateTime CommitTimeUtc { get; }

        /// <summary>
        /// Gets the events that the transaction generated (all <see cref="ObservableObject"/> changes).
        /// Can be empty.
        /// </summary>
        public IReadOnlyList<ObservableEvent> Events { get; }

        /// <summary>
        /// Tries to return the property identifier that is associated to the property name if this
        /// property name has already been used in the domain.
        /// </summary>
        /// <param name="propertyName">THe property name to look for.</param>
        /// <returns>The property identifier or null.</returns>
        public int? FindPropertyId( string propertyName ) => _propertyId( propertyName );

        /// <summary>
        /// Adds a command to the ones already enqueued by <see cref="DomainView.SendCommand(object)"/>.
        /// </summary>
        public void SendCommand( in ObservableDomainCommand command ) => _commands.Add( command );

        /// <summary>
        /// Registrar for actions (that can be synchronous as well as asynchronous) that must be executed after
        /// the transaction itself.
        /// </summary>
        public IActionRegistrar<PostActionContext> LocalPostActions => _localPostActions;

        /// <summary>
        /// Registrar for actions (that can be synchronous as well as asynchronous) that must be executed after
        /// the transaction itself, respecting the order of other set of <see cref="DomainPostActions"/> submitted by
        /// other (concurrent) transactions on this domain.
        /// </summary>
        public IActionRegistrar<PostActionContext> DomainPostActions => _domainPostActions;

        internal SuccessfulTransactionEventArgs( ObservableDomain d, Func<string,int?> propertyId, IReadOnlyList<ObservableEvent> e, List<ObservableDomainCommand> c, DateTime startTime )
            : base( d.CurrentMonitor )
        {
            _domain = d;
            _propertyId = propertyId;
            _localPostActions = new ActionRegistrar<PostActionContext>();
            _domainPostActions = new ActionRegistrar<PostActionContext>();
            _commands = c;
            StartTimeUtc = startTime;
            CommitTimeUtc = DateTime.UtcNow;
            Events = e;
        }

    }
}
