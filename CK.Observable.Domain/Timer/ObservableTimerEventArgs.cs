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
    public class ObservableTimedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new <see cref="ObservableTimedEventArgs"/>.
        /// </summary>
        /// <param name="current">The current time.</param>
        /// <param name="expected">The expected event time.</param>
        public ObservableTimedEventArgs( DateTime current, DateTime expected )
        {
            Current = current;
            Expected = expected;
            Delta = expected - current;
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
        /// Gets the difference between <see cref="Expected"/> and <see cref="Current"/>.
        /// </summary>
        public TimeSpan Delta { get; }

    }
}
