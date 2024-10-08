using System;
using System.Collections.Generic;
using CK.Core;

namespace CK.Observable;

/// <summary>
/// Public facade for <see cref="TimeManager"/> exposed by <see cref="IObservableDomain"/>.
/// </summary>
public interface ITimeManager
{
    /// <summary>
    /// Gets whether this <see cref="TimeManager"/> is running.
    /// Use <see cref="Start"/> and <see cref="Stop"/> inside a transaction to change it.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Ensure that this manager is running.
    /// This must be called inside a transaction (inside one of the <see cref="ObservableDomain.Modify"/> methods) otherwise
    /// an <see cref="InvalidOperationException"/> is thrown.
    /// </summary>
    void Start();

    /// <summary>
    /// Ensure that this manager is not running.
    /// When stopped, <see cref="NextActiveTime"/> is <see cref="Util.UtcMinValue"/> and <see cref="ObservableTimer"/>
    /// and <see cref="ObservableReminder"/> are only checked (and eventually fire) when one of the <see cref="ObservableDomain.Modify"/>
    /// method is executed. The domain is no more active.
    /// <para>
    /// This must be called inside a transaction (inside one of the <see cref="ObservableDomain.Modify"/> methods) otherwise
    /// an <see cref="InvalidOperationException"/> is thrown.
    /// </para>
    /// </summary>
    void Stop();

    /// <summary>
    /// Gets the next time this domain will be active: this <see cref="IsRunning"/> is true
    /// and there is at least one <see cref="ObservableTimer"/> or <see cref="ObservableReminder"/> that
    /// must fire at this time.
    /// When <see cref="IsRunning"/> is false or no events are planned, this is <see cref="Util.UtcMinValue"/>.
    /// <para>
    /// This is an important property that can be used by domain managers to handle load and unload of a domain.
    /// </para>
    /// </summary>
    DateTime NextActiveTime { get; }

    /// <summary>
    /// Gets the collection of <see cref="ObservableTimer"/> (even when <see cref="ObservableTimedEventBase.IsActive"/> is false).
    /// </summary>
    IReadOnlyCollection<ObservableTimer> Timers { get; }

    /// <summary>
    /// Gets the collection of <see cref="ObservableReminder"/> (note that <see cref="ObservableTimedEventBase.IsActive"/>
    /// or <see cref="ObservableReminder.IsPooled"/> can be true or false).
    /// </summary>
    IReadOnlyCollection<ObservableReminder> Reminders { get; }

    /// <summary>
    /// Gets the collection of all the <see cref="ObservableTimedEventBase"/>.
    /// </summary>
    IReadOnlyCollection<ObservableTimedEventBase> AllObservableTimedEvents { get; }

    /// <summary>
    /// Uses a pooled <see cref="ObservableReminder"/> to call the specified callback at the given time with the
    /// associated <see cref="ObservableTimedEventBase.Tag"/> object.
    /// <para>
    /// This must be called inside a transaction (inside one of the <see cref="ObservableDomain.Modify"/> methods) otherwise
    /// an <see cref="InvalidOperationException"/> is thrown.
    /// </para>
    /// </summary>
    /// <param name="dueTimeUtc">The due time. Must be in Utc and not <see cref="Util.UtcMinValue"/> or <see cref="Util.UtcMaxValue"/>.</param>
    /// <param name="callback">The callback method. Must not be null.</param>
    /// <param name="clock">The optional <see cref="SuspendableClock"/> to which the reminder must be bound.</param>
    /// <param name="tag">
    /// Optional tag that will be available on event argument's <see cref="ObservableReminderEventArgs.Reminder"/>.
    /// The reminder object exposes the <see cref="ObservableTimedEventBase.Tag"/>.
    /// </param>
    void Remind( DateTime dueTimeUtc, SafeEventHandler<ObservableReminderEventArgs> callback, SuspendableClock? clock, object? tag );

}
