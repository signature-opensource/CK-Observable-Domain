using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable.Domain.Tests.TimedEvents
{

    [SerializationVersion( 0 )]
    class AutoCounter : ObservableObject
    {
        readonly ObservableTimer _timer;

        public AutoCounter( int intervalMilliSeconds )
        {
            _timer = new ObservableTimer(DateTime.UtcNow, intervalMilliSeconds, true) { Mode = ObservableTimerMode.Critical };
            _timer.Elapsed += IncrementCount;
        }

        void IncrementCount( object sender, ObservableTimedEventArgs e ) => Count++;

        public AutoCounter( IBinaryDeserializerContext d )
            : base( d )
        {
            var r = d.StartReading();
            Count = r.ReadInt32();
            _timer = (ObservableTimer)r.ReadObject();
        }

        /// <summary>
        /// This event is automatically raised each time Count changed.
        /// </summary>
        public EventHandler CountChanged;

        public int Count { get; set; }

        public void Restart()
        {
            Count = 0;
            _timer.IsActive = true;
        }

        public void Reconfigure( DateTime firstDueTimeUtc, int intervalMilliSeconds ) => _timer.Reconfigure( firstDueTimeUtc, intervalMilliSeconds );

        public int IntervalMilliSeconds
        {
            get => _timer.IntervalMilliSeconds;
            set => _timer.IntervalMilliSeconds = value;
        }

        public void Stop()
        {
            _timer.IsActive = false;
        }

        void Write( BinarySerializer w )
        {
            w.Write( Count );
            w.WriteObject( _timer );
        }

    }
}
