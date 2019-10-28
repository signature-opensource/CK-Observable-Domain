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
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="current">The current time.</param>
        /// <param name="expected">The expected event time.</param>
        public ObservableTimedEventArgs( IActivityMonitor monitor, DateTime current, DateTime expected )
            : base( monitor )
        {
            Current = current;
            Expected = expected;
            Delta = current - expected;
        }

        /// <summary>
        /// Gets the current, standardized, time of this event.
        /// </summary>
        public DateTime Expected { get; }

        /// <summary>
        /// Gets the expected time of this event.
        /// </summary>
        public DateTime Current { get; }

        /// <summary>
        /// Gets the difference between <see cref="Current"/> and <see cref="Expected"/>.
        /// </summary>
        public TimeSpan Delta { get; }

        /// <summary>
        /// Overridden to return the Current/Expected/Delta values.
        /// </summary>
        /// <returns>A readable string.</returns>
        public override string ToString() => $"Current: {Current}, Expected: {Expected}, Delta: {Delta}.";

    }
}
