namespace CK.Observable;

/// <summary>
/// Exposes a standardized current UTC time for the event and the expected exact time at which the event should have been raised.
/// This allows to adapt the behavior to "real" time aspects if necessary.
/// </summary>
public class ObservableReminderEventArgs : ObservableTimedEventArgs
{
    internal ObservableReminderEventArgs( ObservableReminder source )
        : base( source.TimeManager!.Domain )
    {
        Reminder = source;
    }

    /// <summary>
    /// Gets the <see cref="ObservableReminder" /> that is raising this event.
    /// </summary>
    public ObservableReminder Reminder { get; }
}
