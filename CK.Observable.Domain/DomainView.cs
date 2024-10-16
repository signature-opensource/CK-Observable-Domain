using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable;

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
    /// Gets the current transaction status.
    /// </summary>
    public CurrentTransactionStatus CurrentTransactionStatus => _d.CurrentTransactionStatus;

    /// <summary>
    /// Gets the current transaction number (the very first one is 0).
    /// This will be incremented at the end of the current transaction if it succeeds.
    /// </summary>
    public int TransactionNumber => _d.TransactionSerialNumber;

    /// <summary>
    /// Gets the current commit time. Defaults to <see cref="DateTime.UtcNow"/> at the very beginning,
    /// when no transaction has been committed yet (and <see cref="TransactionSerialNumber"/> is 0).
    /// </summary>
    public DateTime TransactionCommitTimeUtc => _d.TransactionCommitTimeUtc;

    /// <summary>
    /// Gets the PocoDirectory. <see cref="HasPocoDirectory"/> must be true otherwise
    /// an <see cref="InvalidOperationException"/> is raised.
    /// This requires a ServiceProvider to be provided to the ObservableDomain constructor (of course,
    /// a PocoDirectory must be available).
    /// </summary>
    public PocoDirectory PocoDirectory => _d.PocoDirectory;

    /// <summary>
    /// Gets whether the <see cref="PocoDirectory"/> is available.
    /// </summary>
    public bool HasPocoDirectory => _d.HasPocoDirectory;

    /// <summary>
    /// Sends a <see cref="ObservableDomainCommand"/> to the external world only if <see cref="CurrentTransactionStatus"/>
    /// is <see cref="CurrentTransactionStatus.Regular"/> (otherwise the command is ignored and a warning is emitted).
    /// Commands are enlisted into <see cref="TransactionResult.Commands"/>  and will be processed by one (or more)
    /// <see cref="ObservableDomainSidekick"/> when and if the transaction succeeded.
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
    /// This is a helper that calls <see cref="SendCommand(in ObservableDomainCommand)"/>: the <see cref="CurrentTransactionStatus"/>
    /// must be <see cref="CurrentTransactionStatus.Regular"/> otherwise the command is ignored (and a warning is emitted).
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
    /// This is a helper that calls <see cref="SendCommand(in ObservableDomainCommand)"/>: the <see cref="CurrentTransactionStatus"/>
    /// must be <see cref="CurrentTransactionStatus.Regular"/> otherwise the command is ignored (and a warning is emitted).
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
    /// In this "broadcast mode", if all <see cref="ObservableDomainSidekick.ExecuteCommand(IActivityMonitor, in SidekickCommand)"/>
    /// return false, the command is considered unhandled and by default this is an error: see <paramref name="isOptionalExecution"/>.
    /// <para>
    /// This is a helper that calls <see cref="SendCommand(in ObservableDomainCommand)"/>: the <see cref="CurrentTransactionStatus"/>
    /// must be <see cref="CurrentTransactionStatus.Regular"/> otherwise the command is ignored (and a warning is emitted).
    /// </para>
    /// </summary>
    /// <param name="command">The command payload.</param>
    /// <param name="isOptionalExecution">
    /// By default, at least one sidekick must handle the command
    /// (at least one <see cref="ObservableDomainSidekick.ExecuteCommand(IActivityMonitor, in SidekickCommand)"/> must return true).
    /// When set to true, a simple warning is emitted if the command failed to be handled.
    /// </param>
    public void SendBroadcastCommand( object command, bool isOptionalExecution = false )
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
    /// Ensures that required sidekicks are instantiated and that any required <see cref="ObservableDomainSidekick.RegisterClientObject(IActivityMonitor, IDestroyable)"/>
    /// have been called.
    /// <para>
    /// This should typically called at the end of a final constructor code of a <see cref="ISidekickClientObject{TSidekick}"/> (or decorated
    /// with a <see cref="UseSidekickAttribute"/>) object.
    /// </para>
    /// <para>
    /// When <see cref="DomainView.CurrentTransactionStatus"/> is not <see cref="CurrentTransactionStatus.Regular"/> (we are deserializing or initializing),
    /// nothing is done: <see cref="ObservableDomain.HasWaitingSidekicks"/> is true and the sidekicks will kick in at the start of the next transaction
    /// (or during the roll back if a <see cref="IObservableDomainClient"/> can do it).
    /// </para>
    /// </summary>
    public void EnsureSidekicks() => _d.EnsureSidekicks( _o );

    /// <summary>
    /// Gets a central domain simple random number generator.
    /// </summary>
    public Random Random => _d._random;
}
