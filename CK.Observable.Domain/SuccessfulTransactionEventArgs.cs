using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        /// Gets the transaction number.
        /// </summary>
        public int TransactionNumber { get; }

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
        /// Adds a command to the ones already enqueued by <see cref="DomainView.SendCommand(in ObservableDomainCommand)"/>.
        /// </summary>
        /// <param name="command">The command to send.</param>
        public void SendCommand( in ObservableDomainCommand command ) => _commands.Add( command );

        /// <summary>
        /// Sends a command to a known sidekick. By default, the target sidekick must handle the command: see <paramref name="isOptionalExecution"/>.
        /// <para>
        /// This is just a helper that calls <see cref="SendCommand(in ObservableDomainCommand)"/>.
        /// </para>
        /// </summary>
        /// <param name="command">The command payload.</param>
        /// <param name="target">The target sidekick that must handle the command.</param>
        /// <param name="isOptionalExecution">
        /// By default, the sidekick must handle the command (<see cref="ObservableDomainSidekick.ExecuteCommand(Core.IActivityMonitor, in SidekickCommand)"/>
        /// must return true).
        /// When set to true, a simple warning is emitted if the sidekick failed to handle the command.
        /// </param>
        public void SendCommand( object command, ObservableDomainSidekick target, bool isOptionalExecution = false )
        {
            _commands.Add( new ObservableDomainCommand( command, target, isOptionalExecution ) );
        }

        /// <summary>
        /// Sends a command to a sidekick associated to a locator.
        /// By default, the target sidekick must handle the command: see <paramref name="isOptionalExecution"/>.
        /// <para>
        /// This is just a helper that calls <see cref="SendCommand(in ObservableDomainCommand)"/>.
        /// </para>
        /// </summary>
        /// <param name="command">The command payload.</param>
        /// <param name="targetLocator">A locator object of the target sidekick that must handle the command.</param>
        /// <param name="isOptionalExecution">
        /// By default, the sidekick must handle the command (<see cref="ObservableDomainSidekick.ExecuteCommand(Core.IActivityMonitor, in SidekickCommand)"/>
        /// must return true).
        /// When set to true, a simple warning is emitted if the sidekick failed to handle the command.
        /// </param>
        public void SendCommand( object command, ISidekickLocator targetLocator, bool isOptionalExecution = false )
        {
            _commands.Add( new ObservableDomainCommand( command, targetLocator, isOptionalExecution ) );
        }

        /// <summary>
        /// Sends a command to a known sidekick type. By default, a sidekick instance must exist AND handle the command:
        /// see <paramref name="isOptionalExecution"/>.
        /// <para>
        /// This is just a helper that calls <see cref="SendCommand(in ObservableDomainCommand)"/>.
        /// </para>
        /// </summary>
        /// <param name="command">The command payload.</param>
        /// <param name="sidekickTargetType">The type of the sidekick that must handle the command.</param>
        /// <param name="isOptionalExecution">
        /// By default, the sidekick instance must exist AND handle the command (<see cref="ObservableDomainSidekick.ExecuteCommand(Core.IActivityMonitor, in SidekickCommand)"/>
        /// must return true).
        /// When set to true, a simple warning is emitted if the sidekick is not instantiated or failed to handle the command.
        /// </param>
        public void SendCommand( object command, Type sidekickTargetType, bool isOptionalExecution = false )
        {
            _commands.Add( new ObservableDomainCommand( command, sidekickTargetType, isOptionalExecution ) );
        }

        /// <summary>
        /// Sends a command in "broadcast mode": all existing sidekicks will have the opportunity to handle it.
        /// In this "broadcast mode", if all <see cref="ObservableDomainSidekick.ExecuteCommand(Core.IActivityMonitor, in SidekickCommand)"/>
        /// return false, the command is considered unhandled and by default this is an error: see <paramref name="isOptionalExecution"/>.
        /// <para>
        /// This is just a helper that calls <see cref="SendCommand(in ObservableDomainCommand)"/>.
        /// </para>
        /// </summary>
        /// <param name="command">The command payload.</param>
        /// <param name="isOptionalExecution">
        /// By default, at least one sidekick must handle the command
        /// (at least one <see cref="ObservableDomainSidekick.ExecuteCommand(Core.IActivityMonitor, in SidekickCommand)"/> must return true).
        /// When set to true, a simple warning is emitted if the command failed to be handled.
        /// </param>
        public void SendCommand( object command, bool isOptionalExecution = false )
        {
            _commands.Add( new ObservableDomainCommand( command, null, isOptionalExecution ) );
        }

        /// <summary>
        /// Gets whether a <see cref="ObservableDomain.SaveCommand"/> has been sent and
        /// should be honored if possible.
        /// </summary>
        public bool HasSaveCommand => _commands.Any( x => x.Command == ObservableDomain.SaveCommand );

        /// <summary>
        /// Registrar for actions (that can be synchronous as well as asynchronous) that must be executed after
        /// the transaction itself.
        /// </summary>
        public IActionRegistrar<PostActionContext> PostActions => _localPostActions;

        /// <summary>
        /// Registrar for actions (that can be synchronous as well as asynchronous) that must be executed after
        /// the transaction itself, respecting the order of other set of <see cref="DomainPostActions"/> submitted by
        /// other (concurrent) transactions on this domain.
        /// </summary>
        public IActionRegistrar<PostActionContext> DomainPostActions => _domainPostActions;

        internal SuccessfulTransactionEventArgs( ObservableDomain d,
                                                 Func<string,int?> propertyId,
                                                 IReadOnlyList<ObservableEvent> e,
                                                 List<ObservableDomainCommand> c,
                                                 DateTime startTime,
                                                 int tranNum )
            : base( d.CurrentMonitor )
        {
            _domain = d;
            _propertyId = propertyId;
            _localPostActions = new ActionRegistrar<PostActionContext>();
            _domainPostActions = new ActionRegistrar<PostActionContext>();
            _commands = c;
            StartTimeUtc = startTime;
            CommitTimeUtc = DateTime.UtcNow;
            TransactionNumber = tranNum;
            Events = e;
        }

    }
}
