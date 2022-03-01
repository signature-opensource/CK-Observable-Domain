using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable.Domain.Tests.TimedEvents
{
    [BinarySerialization.SerializationVersion( 0 )]
    class AutoCounter : ObservableObject
    {
        readonly ObservableTimer _timer;
        ObservableEventHandler _countChanged;

        /// <summary>
        /// Initializes a new <see cref="AutoCounter"/> that starts immediately.
        /// </summary>
        /// <param name="intervalMilliSeconds">Interval between <see cref="Count"/> increment.</param>
        public AutoCounter( int intervalMilliSeconds )
        {
            _timer = new ObservableTimer( DateTime.UtcNow, intervalMilliSeconds, true ) { Mode = ObservableTimerMode.Critical };
            _timer.Elapsed += IncrementCount;
        }

        /// <summary>
        /// Event method called by the private <see cref="ObservableTimer"/>.
        /// </summary>
        /// <param name="sender">The sender is our private ObservableTimer.</param>
        /// <param name="e">The event argument.</param>
        void IncrementCount( object sender, ObservableTimedEventArgs e )
        {
            Count++;
            e.Monitor.Info( $"AutoCounter call n°{Count}." );
        }

        AutoCounter( BinarySerialization.IBinaryDeserializer d, BinarySerialization.ITypeReadInfo info )
                : base( BinarySerialization.Sliced.Instance )
        {
            Count = d.Reader.ReadInt32();
            _timer = d.ReadObject<ObservableTimer>();
            _countChanged = new ObservableEventHandler( d );
        }

        public static void Write( BinarySerialization.IBinarySerializer s, in AutoCounter o )
        {
            s.Writer.Write( o.Count );
            s.WriteObject( o._timer );
            o._countChanged.Write( s );
        }

        /// <summary>
        /// This event is automatically raised each time Count changed.
        /// This is an unsafe event (it is not serialized and no cleanup of disposed <see cref="IDestroyable"/> is done).
        /// Even if it is supported, safe event should always be preferred.
        /// </summary>
        public event SafeEventHandler CountChanged
        {
            add => _countChanged.Add( value, nameof( CountChanged ) );
            remove => _countChanged.Remove( value );
        }

        /// <summary>
        /// Gets the count property.
        /// </summary>
        public int Count { get; private set; }

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
        /// Gets the private <see cref="ObservableTimedEventBase.IsActive"/>.
        /// Because of the <see cref="SuspendableClock"/>, then Start is not guaranteed to put this to true
        /// (A contrario, Stop is guaranteed to transition this to false.)
        /// </summary>
        public bool IsRunning => _timer.IsActive;

        /// <summary>
        /// Gets or sets the private <see cref="ObservableTimedEventBase.SuspendableClock"/>.
        /// </summary>
        public SuspendableClock? SuspendableClock
        {
            get => _timer.SuspendableClock;
            set => _timer.SuspendableClock = value;
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
