using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable
{
    /// <summary>
    /// Offers a protected view on its <see cref="ObservableDomain"/> from the point of view
    /// of domain objects: This is exposed by the protected <see cref="ObservableObject.Domain"/>
    /// and <see cref="InternalObject.Domain"/>.
    /// </summary>
    /// <remarks>
    /// This object is marked with <see cref="NotExportableAttribute"/> just to be safe if this is, by mistake, exposed
    /// in the public API of an <see cref="ObservableObject"/> or an <see cref="InternalObject"/>.
    /// </remarks>
    [NotExportable( Error = "DomainView must not be exposed. Only the protected Domain should be used." )]
    public readonly struct DomainView
    {
        readonly IDestroyable _o;
        readonly ObservableDomain _d;

        internal DomainView( IDestroyable o, ObservableDomain d )
        {
            _o = o;
            _d = d;
        }

        /// <summary>
        /// Gives access to the monitor to use.
        /// </summary>
        public IActivityMonitor Monitor => _d.CurrentMonitor;

        /// <summary>
        /// Gets the current transaction number: the very first one is 1.
        /// Note that if this transaction fails, the <see cref="ObservableDomain.TransactionSerialNumber"/> will not
        /// be set to this number.
        /// </summary>
        public int CurrentTransactionNumber => _d.TransactionSerialNumber + 1;

        /// <summary>
        /// Sends a <see cref="ObservableDomainCommand"/> to the external world. Commands are enlisted
        /// into <see cref="TransactionResult.Commands"/> (when the transaction succeeds)
        /// and will be processed by one (or more) <see cref="ObservableDomainSidekick"/>.
        /// </summary>
        /// <param name="command">The command to send.</param>
        public void SendCommand( in ObservableDomainCommand command )
        {
            _d.SendCommand( _o, command );
        }

        /// <summary>
        /// Sends a command to a sidekick via a locator (a sidekick is its own <see cref="ISidekickLocator"/>).
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
            _d.SendCommand( _o, new ObservableDomainCommand( command, targetLocator, isOptionalExecution ) );
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
        /// By default, the sidekick instance must exist AND handle the command (<see cref="ObservableDomainSidekick.ExecuteCommand(IActivityMonitor, in SidekickCommand)"/>
        /// must return true).
        /// When set to true, a simple warning is emitted if the sidekick is not instantiated or failed to handle the command.
        /// </param>
        public void SendCommand( object command, Type sidekickTargetType, bool isOptionalExecution = false )
        {
            _d.SendCommand( _o, new ObservableDomainCommand( command, sidekickTargetType, isOptionalExecution ) );
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
            _d.SendCommand( _o, new ObservableDomainCommand( command, null, isOptionalExecution ) );
        }

        /// <summary>
        /// Gets the domain name. 
        /// </summary>
        public string DomainName => _d.DomainName;

        /// <summary>
        /// Gets a preallocated reusable event argument. 
        /// </summary>
        public ObservableDomainEventArgs DefaultEventArgs => _d.DefaultEventArgs;

        /// <summary>
        /// Uses a pooled <see cref="ObservableReminder"/> to call the specified callback at the given time with the
        /// associated <see cref="ObservableTimedEventBase.Tag"/> object.
        /// </summary>
        /// <param name="dueTimeUtc">The due time. Must be in Utc and not <see cref="Util.UtcMinValue"/> or <see cref="Util.UtcMaxValue"/>.</param>
        /// <param name="callback">The callback method. Must not be null.</param>
        /// <param name="tag">Optional tag that will be available on event argument's: <see cref="ObservableTimedEventBase.Tag"/>.</param>
        public void Remind( DateTime dueTimeUtc, SafeEventHandler<ObservableReminderEventArgs> callback, object? tag = null )
        {
            _d.TimeManager.Remind( dueTimeUtc, callback, null, tag );
        }

        /// <summary>
        /// Uses a pooled <see cref="ObservableReminder"/> to call the specified callback at the given time with the
        /// associated <see cref="ObservableTimedEventBase.Tag"/> object, binding this reminder to a <see cref="SuspendableClock"/>.
        /// </summary>
        /// <param name="dueTimeUtc">The due time. Must be in Utc and not <see cref="Util.UtcMinValue"/> or <see cref="Util.UtcMaxValue"/>.</param>
        /// <param name="callback">The callback method. Must not be null.</param>
        /// <param name="clock">The <see cref="SuspendableClock"/> to which the reminder must be bound.</param>
        /// <param name="tag">Optional tag that will be available on event argument's: <see cref="ObservableTimedEventBase.Tag"/>.</param>
        public void Remind( DateTime dueTimeUtc, SafeEventHandler<ObservableReminderEventArgs> callback, SuspendableClock clock, object? tag = null )
        {
            _d.TimeManager.Remind( dueTimeUtc, callback, clock, tag );
        }

        /// <summary>
        /// Gets whether the domain is being deserialized.
        /// </summary>
        public bool IsDeserializing => _d.IsDeserializing;

        /// <summary>
        /// Ensures that required sidekicks are instantiated and that any required <see cref="ObservableDomainSidekick.RegisterClientObject(IActivityMonitor, IDestroyable)"/>
        /// have been called.
        /// When this method returns false, it means that an error occurred and that the current transaction cannot be committed.
        /// <para>
        /// This should typically called at the end of a final constructor code of a <see cref="ISidekickClientObject{TSidekick}"/> object.
        /// </para>
        /// </summary>
        /// <returns>True on success, false if one required sidekick failed to be instantiated.</returns>
        public bool EnsureSidekicks() => _d.EnsureSidekicks( _o );

        /// <summary>
        /// Gets a central domain simple random number generator.
        /// </summary>
        public Random Random => _d._random;
    }
}
