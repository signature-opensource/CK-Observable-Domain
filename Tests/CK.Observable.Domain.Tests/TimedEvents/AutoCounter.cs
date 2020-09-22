using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable.Domain.Tests.TimedEvents
{

    [SerializationVersion( 0 )]
    class AutoCounter : ObservableObject
    {
        readonly ObservableTimer _timer;

        /// <summary>
        /// Initializes a new <see cref="AutoCounter"/> that start immediately.
        /// </summary>
        /// <param name="intervalMilliSeconds">Interval between <see cref="Count"/> increment.</param>
        public AutoCounter( int intervalMilliSeconds )
        {
            _timer = new ObservableTimer(DateTime.UtcNow, intervalMilliSeconds, true) { Mode = ObservableTimerMode.Critical };
            _timer.Elapsed += IncrementCount;
        }

        /// <summary>
        /// Event method called by the private <see cref="ObservableTimer"/>.
        /// </summary>
        /// <param name="sender">The sender is our private ObservableTimer.</param>
        /// <param name="e">The event argument.</param>
        void IncrementCount( object sender, ObservableTimedEventArgs e ) => Count++;

        AutoCounter( IBinaryDeserializerContext d )
            : base( d )
        {
            var r = d.StartReading().Reader;
            Count = r.ReadInt32();
            _timer = (ObservableTimer)r.ReadObject();
        }

        void Write( BinarySerializer w )
        {
            w.Write( Count );
            w.WriteObject( _timer );
        }

        /// <summary>
        /// This event is automatically raised each time Count changed.
        /// This is an unsafe event (it is not serialized and no cleanup of disposed <see cref="IDisposableObject"/> is done).
        /// Even if it is supported, safe event should always be preferred.
        /// </summary>
        public EventHandler CountChanged;

        /// <summary>
        /// Gets or sets the count property.
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// Restarts this counter by setting the <see cref="Count"/> to 0 and <see cref="ObservableTimer.IsActive"/> to true.
        /// </summary>
        public void Restart()
        {
            Count = 0;
            _timer.IsActive = true;
        }

        /// <summary>
        /// Stops this counter by setting the <see cref="ObservableTimer.IsActive"/> to false.
        /// </summary>
        public void Stop()
        {
            _timer.IsActive = false;
        }

        /// <summary>
        /// Reconfigures the private timer: see <see cref="ObservableTimer.Reconfigure(DateTime, int)"/>.
        /// </summary>
        /// <param name="firstDueTimeUtc"></param>
        /// <param name="intervalMilliSeconds"></param>
        public void Reconfigure( DateTime firstDueTimeUtc, int intervalMilliSeconds ) => _timer.Reconfigure( firstDueTimeUtc, intervalMilliSeconds );

        /// <summary>
        /// Gets or sets the private <see cref="ObservableTimer.IntervalMilliSeconds"/>.
        /// </summary>
        public int IntervalMilliSeconds
        {
            get => _timer.IntervalMilliSeconds;
            set => _timer.IntervalMilliSeconds = value;
        }

    }
}
