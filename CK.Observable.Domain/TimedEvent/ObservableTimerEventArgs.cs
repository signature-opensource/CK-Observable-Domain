using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    /// <summary>
    /// Exposes a standardized current UTC time for the event and the
    /// expected exact time at which the event should have been raised.
    /// This allows to adapt the behavior to "real" time aspects if necessary.
    /// </summary>
    public class ObservableTimedEventArgs : EventMonitoredArgs
    {
        /// <summary>
        /// Initializes a new <see cref="ObservableTimedEventArgs"/>.
        /// </summary>
        /// <param name="monitor">The monitor to expose to event clients.</param>
        /// <param name="source">
        /// The typed source, so that <see cref="ObservableTimedEventBase.Domain"/> and <see cref="ObservableTimedEventBase.Tag"/> object
        /// are easily accessible.
        /// </param>
        /// <param name="current">The current time.</param>
        /// <param name="expected">The expected event time.</param>
        public ObservableTimedEventArgs( IActivityMonitor monitor, ObservableTimedEventBase source, DateTime current, DateTime expected )
            : base( monitor )
        {
            Source = source;
            Current = current;
            Expected = expected;
            DeltaMilliSeconds = (int)Math.Ceiling( (current - expected).TotalMilliseconds );
        }

        /// <summary>
        /// Gets the <see cref="ObservableTimedEventBase"/> that is raising this event.
        /// </summary>
        public ObservableTimedEventBase Source { get; }

        /// <summary>
        /// Gets the current, standardized, time of this event.
        /// </summary>
        public DateTime Expected { get; }

        /// <summary>
        /// Gets the expected time of this event.
        /// </summary>
        public DateTime Current { get; }

        /// <summary>
        /// Gets the difference between <see cref="Current"/> and <see cref="Expected"/>
        /// rounded to the upper millisecond.
        /// </summary>
        public int DeltaMilliSeconds { get; }

        /// <summary>
        /// Overridden to return the Current/Expected/Delta values.
        /// </summary>
        /// <returns>A readable string.</returns>
        public override string ToString() => $"Current: {Current}, Expected: {Expected}, Delta: {DeltaMilliSeconds} ms.";

    }
}
