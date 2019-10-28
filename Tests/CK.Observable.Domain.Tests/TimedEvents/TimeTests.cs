using CK.Core;
using CK.Text;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Domain.Tests.TimedEvents
{
    [TestFixture]
    public class TimeTests
    {

        [TestCase( -10 )]
        [TestCase( 0 )]
        public void timed_events_trigger_at_the_end_of_the_Modify_for_immediate_handling( int timeout )
        {
            IReadOnlyList<ActivityMonitorSimpleCollector.Entry> entries = null;

            using( TestHelper.Monitor.CollectEntries( e => entries = e, LogLevelFilter.Info ) )
            using( var d = new ObservableDomain( TestHelper.Monitor, "Test" ) )
            {
                d.Modify( TestHelper.Monitor, () =>
                {
                    var o = new ObservableReminder( DateTime.UtcNow.AddMilliseconds( timeout ) );
                    o.Elapsed += StaticElapsed;
                    TestHelper.Monitor.Info( "Before!" );
                } );
                TestHelper.Monitor.Info( "After!" );
                d.TimeManager.AllObservableTimedEventBase().Single().IsActive.Should().BeFalse();
            }
            entries.Select( e => e.Text ).Concatenate().Should().Match( "*Before!*Elapsed:*After!*" );
        }

        static ConcurrentQueue<string> RawTraces = new ConcurrentQueue<string>();

        [Test]
        public void timed_event_trigger_on_Timer_or_at_the_start_of_the_Modify()
        {
            RawTraces.Clear();
            using( var d = new ObservableDomain( TestHelper.Monitor, "Test" ) )
            {
                d.Modify( TestHelper.Monitor, () =>
                {
                    var o = new ObservableReminder( DateTime.UtcNow.AddMilliseconds( 50 ) );
                    o.Elapsed += StaticElapsed;
                    RawTraces.Enqueue( "Before!" );
                } );
                RawTraces.Enqueue( "Not Yet!" );
                d.TimeManager.AllObservableTimedEventBase().Single().IsActive.Should().BeTrue();
                Thread.Sleep( 50 );
                d.Modify( TestHelper.Monitor, () =>
                {
                    RawTraces.Enqueue( "After!" );
                } );
                d.TimeManager.AllObservableTimedEventBase().Single().IsActive.Should().BeFalse();
            }

            RawTraces.Concatenate().Should().Match( "*Before!*Not Yet!*Elapsed:*After!*" );
        }

        class FakeAutoTimer : TimeManager.AutoTimer
        {
            public FakeAutoTimer( ObservableDomain d )
                :base( d )
            {
            }

            protected override Task OnDueTimeAsync( IActivityMonitor m ) => Task.CompletedTask;
        }  


        [Test]
        public void timed_event_trigger_at_the_start_of_the_Modify()
        {
            // Since we use a Fake AutoTimer, we can use the TestHelper.Monitor: everything occur on it.

            IReadOnlyList<ActivityMonitorSimpleCollector.Entry> entries = null;

            using( TestHelper.Monitor.CollectEntries( e => entries = e, LogLevelFilter.Info ) )
            using( var d = new ObservableDomain( TestHelper.Monitor, "Test" ) )
            {
                var autoTimer = d.TimeManager.CurrentTimer;
                d.TimeManager.CurrentTimer = new FakeAutoTimer( d );
                autoTimer.Dispose();

                d.Modify( TestHelper.Monitor, () =>
                {
                    var o = new ObservableReminder( DateTime.UtcNow.AddMilliseconds( 50 ) );
                    o.Elapsed += StaticElapsed;
                    TestHelper.Monitor.Info( "Before!" );
                } );
                TestHelper.Monitor.Info( "Not Yet!" );
                d.TimeManager.AllObservableTimedEventBase().Single().IsActive.Should().BeTrue();
                Thread.Sleep( 50 + 30 );
                d.TimeManager.AllObservableTimedEventBase().Single().IsActive.Should().BeTrue( "Auto timer is Fake: +30 ms (whatever the delta is) will never trigger the event." );
                d.Modify( TestHelper.Monitor, () =>
                {
                    TestHelper.Monitor.Info( "After!" );
                } );
                d.TimeManager.AllObservableTimedEventBase().Single().IsActive.Should().BeFalse();
            }
            entries.Select( e => e.Text ).Concatenate().Should().Match( "*Before!*Elapsed:*After!*" );
        }

        static void StaticElapsed( object source, ObservableTimedEventArgs args )
        {
            source.Should().BeAssignableTo<ObservableTimedEventBase>();
            var msg = "Elapsed: " + args.ToString();
            args.Monitor.Info( msg );
            RawTraces.Enqueue( msg );
        }

        [SerializationVersion( 0 )]
        class AutoCounter : ObservableObject
        {
            readonly ObservableTimer _timer;

            public AutoCounter( int intervalMilliSeconds )
            {
                _timer = new ObservableTimer( DateTime.UtcNow, true, intervalMilliSeconds ) { Mode = ObservableTimerMode.Critical };
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

            public void Start() => _timer.IsActive = true;

            public void Reconfigure( DateTime firstDueTimeUtc, int intervalMilliSeconds ) => _timer.Reconfigure( firstDueTimeUtc, intervalMilliSeconds );

            public int IntervalMilliSeconds
            {
                get => _timer.IntervalMilliSeconds;
                set => _timer.IntervalMilliSeconds = value;
            }

            public void Stop() => _timer.IsActive = false;

            void Write( BinarySerializer w )
            {
                w.Write( Count );
                w.WriteObject( _timer );
            }

        }

        [Test]
        public void auto_counter_works_as_expected()
        {
            int relayedCounter = 0;
            const int waitTime = 100 * 11;
            using( var d = new ObservableDomain( TestHelper.Monitor, "Test" ) )
            {
                AutoCounter counter = null;
                d.Modify( TestHelper.Monitor, () =>
                {
                    counter = new AutoCounter( 100 );
                    counter.CountChanged += ( o, e ) => relayedCounter++;
                } );
                TestHelper.Monitor.Trace( $"new AutoCounter( 100 ) done. Waiting {waitTime} ms." );
                Thread.Sleep( waitTime );
                TestHelper.Monitor.Trace( $"End of Waiting." );
                using( d.AcquireReadLock() )
                {
                    TestHelper.Monitor.Trace( $"counter.Count = {counter.Count}, TimeManager.AutoTimer.OnTimeSkipped = {TimeManager.AutoTimer.OnTimeSkipped}." );
                    d.AllObjects.Single().Should().BeSameAs( counter );
                    relayedCounter.Should().Be( counter.Count );
                    counter.Count.Should().Match( c => c == 10 || c == 11 );
                }
            }
        }

        [Test]
        public void auto_counter_works_uses_Critical_mode()
        {
            using( var d = new ObservableDomain( TestHelper.Monitor, "Test" ) )
            {
                AutoCounter counter = null;
                d.Modify( TestHelper.Monitor, () =>
                {
                    counter = new AutoCounter( 20 );
                    Thread.Sleep( 100 );
                } );
                using( d.AcquireReadLock() )
                {
                    TestHelper.Monitor.Trace( $"counter.Count = {counter.Count}." );
                    d.AllObjects.Single().Should().BeSameAs( counter );
                    counter.Count.Should().Match( c => c == 1 );
                }
            }
        }

        [Test]
        public void testing_system_time()
        {
            const int waitTime = 50 * 11;

            TestHelper.Monitor.Trace( "Starting!" );
            using( var timer = new OTimer( 50 ) )
            {
                TestHelper.Monitor.Trace( $"new AutoCounter( OTimer ) done. Waiting {waitTime} ms." );
                Thread.Sleep( waitTime );
                TestHelper.Monitor.Trace( $"End of Waiting. Counter = {timer.Counter}." );
                timer.Counter.Should().Match( c => c == 10 || c == 11 );
            }
        }

        class OTimer : IDisposable
        {
            DateTime _baseTime;
            int _intervalMilliSeconds;
            Timer _timer;
            ActivityMonitor _monitor;
            int _counter;

            public OTimer( int intervalMilliSeconds )
            {
                _baseTime = DateTime.UtcNow;
                _baseTime = _baseTime.AddMilliseconds( -_baseTime.Millisecond );
                _intervalMilliSeconds = intervalMilliSeconds;
                _timer = new System.Threading.Timer( OnTime, this, Timeout.Infinite, Timeout.Infinite );
                _monitor = new ActivityMonitor();
                var next = ObservableTimer.AdjustNextDueTimeUtc( DateTime.UtcNow, _baseTime, intervalMilliSeconds );
                SetNextDueTimeUtc( next );
            }

            public int Counter => _counter;

            public void SetNextDueTimeUtc( DateTime nextDueTimeUtc )
            {
                var delta = nextDueTimeUtc - DateTime.UtcNow;
                var ms = delta <= TimeSpan.Zero ? 0 : (long)Math.Ceiling( delta.TotalMilliseconds );
                _timer.Change( ms, Timeout.Infinite );
                _monitor.Debug( $"Timer set in {ms} ms." );
            }

            static void OnTime( object o )
            {
                var oTimer = (OTimer)o;
                ++oTimer._counter;
                oTimer._monitor.Debug( $"Raised! ({oTimer._counter})." );
                oTimer._baseTime.AddMilliseconds( oTimer._intervalMilliSeconds );
                var next = ObservableTimer.AdjustNextDueTimeUtc( DateTime.UtcNow, oTimer._baseTime, oTimer._intervalMilliSeconds );
                oTimer.SetNextDueTimeUtc( next );
            }

            public void Dispose()
            {
                _timer.Dispose();
            }

        }

    }
}
