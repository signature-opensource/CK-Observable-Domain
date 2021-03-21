using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    /// <summary>
    /// Exposes a standardized current UTC time for the event and the expected exact time at which the event should have been raised.
    /// This allows to adapt the behavior to "real" time aspects if necessary.
    /// </summary>
    public class ObservableTimerEventArgs : ObservableTimedEventArgs
    {
        internal ObservableTimerEventArgs( ObservableTimer source )
            : base( source.TimeManager!.Domain )
        {
            Timer = source;
        }

        /// <summary>
        /// Gets the <see cref="ObservableTimer" /> that is raising this event.
        /// </summary>
        public ObservableTimer Timer { get; }
    }
}
