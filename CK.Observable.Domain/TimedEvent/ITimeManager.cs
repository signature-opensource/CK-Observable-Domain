using System;
using System.Collections.Generic;
using CK.Core;

namespace CK.Observable
{
    /// <summary>
    /// Public facade for <see cref="TimeManager"/> exposed by <see cref="IObservableDomain"/>.
    /// </summary>
    public interface ITimeManager
    {
        /// <summary>
        /// Gets the number of active timed events.
        /// </summary>
        int ActiveTimedEventsCount { get; }

        /// <summary>
        /// Gets the next due time of all the active timed events.
        /// <see cref="CK.Core.Util.UtcMinValue"/> when no active events exist.
        /// </summary>
        DateTime NextDueTimeUtc { get; }

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
}
