using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable;

/// <summary>
/// Exposes a standardized current UTC time for the event and the expected exact time at which the event should have been raised.
/// This allows to adapt the behavior to "real" time aspects if necessary.
/// </summary>
public abstract class ObservableTimedEventArgs : ObservableDomainEventArgs
{
    internal ObservableTimedEventArgs( ObservableDomain d )
        : base( d )
    {
    }

    /// <summary>
    /// Gets the expected time of this event.
    /// </summary>
    public DateTime Expected { get; internal set; }

    /// <summary>
    /// Gets the current, unified, time of this event: all timer handlers see the same time that is either
    /// the <see cref="IInternalTransaction.StartTime"/> when activated at the start of <see cref="ObservableDomain.Modify"/>
    /// or the time right after the Modify.
    /// </summary>
    public DateTime Current { get; internal set; }

    /// <summary>
    /// Gets the difference between <see cref="Current"/> and <see cref="Expected"/>
    /// rounded to the upper millisecond.
    /// </summary>
    public int DeltaMilliSeconds { get; internal set; }

    /// <summary>
    /// Overridden to return the Current/Expected/Delta values.
    /// </summary>
    /// <returns>A readable string.</returns>
    public override string ToString() => $"Current: {Current}, Expected: {Expected}, Delta: {DeltaMilliSeconds} ms.";

}
