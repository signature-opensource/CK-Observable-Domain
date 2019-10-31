using CK.Core;
using CK.Text;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
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
                    TestHelper.Monitor.Trace( $"counter.Count = {counter.Count}." );
                    d.AllObjects.Single().Should().BeSameAs( counter );
                    relayedCounter.Should().Be( counter.Count );
                    counter.Count.Should().Match( c => c == 10 || c == 11 );
                }
            }
        }

        [Test]
        public void auto_counter_works_uses_Critical_mode()
        {
            IReadOnlyList<ActivityMonitorSimpleCollector.Entry> entries = null;

            using( TestHelper.Monitor.CollectEntries( e => entries = e ) )
            using( var d = new ObservableDomain( TestHelper.Monitor, "Test" ) )
            {
                AutoCounter counter = null;
                d.Modify( TestHelper.Monitor, () =>
                {
                    counter = new AutoCounter( 5 );
                    // We rely on the end of the Modify that execute pending timers
                    // (timed events are handled at the start AND the end of the Modify).
                    Thread.Sleep( 200 );
                    // Here, there is one execution.
                } );
                // This is not really safe: the timer MAY be fired here before we do the AcquireReadLock:
                // this is why we allow the counter to be greater than 1...
                using( d.AcquireReadLock() )
                {
                    TestHelper.Monitor.Trace( $"counter.Count = {counter.Count}." );
                    d.AllObjects.Single().Should().BeSameAs( counter );
                    counter.Count.Should().BeGreaterOrEqualTo( 1 );
                }
            }
            entries.Should().Match( e => e.Any( m => m.Text.Contains( " event(s) lost!" ) ), "We have lost events (around 40)." );
        }

        [Test]
        public void callbacks_for_reminders_as_well_as_timers_must_be_regular_object_methods_or_static()
        {
            using( var d = new ObservableDomain( TestHelper.Monitor, "Test" ) )
            {
                var tranResult = d.Modify( TestHelper.Monitor, () =>
                {
                    var t = new ObservableTimer( DateTime.UtcNow );
                    Assert.Throws<ArgumentException>( () => t.Elapsed += ( o, e ) => { } );
                    Assert.Throws<ArgumentException>( () => t.Elapsed += new EventHandler<ObservableTimedEventArgs>( ( o, e ) => { } ) );
                    var r = new ObservableReminder( DateTime.UtcNow );
                    Assert.Throws<ArgumentException>( () => r.Elapsed += ( o, e ) => { } );
                    Assert.Throws<ArgumentException>( () => r.Elapsed += new EventHandler<ObservableTimedEventArgs>( ( o, e ) => { } ) );
                } );
                tranResult.Success.Should().BeTrue();
            }
        }

        [SerializationVersion(0)]
        sealed class SimpleValue : ObservableObject
        {
            public SimpleValue()
            {
            }

            SimpleValue( IBinaryDeserializerContext c )
                : base( c )
            {
                var r = c.StartReading();
                Value = r.ReadInt32();
            }

            void Write( BinarySerializer w )
            {
                w.Write( Value );
            }

            public int Value { get; set; }

            public void SilentIncrementValue( object source, EventArgs args )
            {
                Value += 1;
            }

            public void IncrementValue( object source, EventMonitoredArgs args )
            {
                Value += 1;
                args.Monitor.Trace( $"Value => {Value}" );
            }
        }


        [Test]
        public void serializing_timers_and_reminders()
        {
            var now = DateTime.UtcNow;
            using( var d = new ObservableDomain( TestHelper.Monitor, "Test" ) )
            {
                var tranResult = d.Modify( TestHelper.Monitor, () =>
                {
                    // First due time: from 50 to 450 ms.
                    // Interval: from 20 to 180 ms.
                    // Latest in 450+180 ms = 630 ms.
                    Enumerable.Range( 0, 8 ).Select( i => new ObservableTimer( now.AddMilliseconds( (i + 1) * 50 ), (i & 1) != 0, (i + 1) * 20 ) ).ToArray();
                    d.TimeManager.AllObservableTimedEvents.Where( o => !o.IsActive ).Should().HaveCount( 8 );
                } );
                tranResult.Success.Should().BeTrue();
                using( var d2 = SaveAndLoad( d ) )
                {
                    d2.TimeManager.Timers.Should().HaveCount( 8 );
                    d2.TimeManager.AllObservableTimedEvents.Where( o => !o.IsActive ).Should().HaveCount( 8 );
                }
                SimpleValue val;
                d.Modify( TestHelper.Monitor, () =>
                {
                    val = new SimpleValue();
                    foreach( var t in d.TimeManager.Timers )
                    {
                        t.Elapsed += val.SilentIncrementValue;
                    }
                    // Max: (5+1)*50 = 300 ms.
                    foreach( var r in Enumerable.Range( 0, 5 ).Select( i => new ObservableReminder( now.AddMilliseconds( (i + 1) * 50 ) ) ) )
                    {
                        r.Elapsed += val.IncrementValue;
                    }
                    d.TimeManager.AllObservableTimedEvents.Where( o => o.IsActive ).Should().HaveCount( 4 + 5 );
                } ).Success.Should().BeTrue();

                using( var d2 = SaveAndLoad( d ) )
                {
                    d2.TimeManager.Timers.Should().HaveCount( 8 );
                    d2.TimeManager.Reminders.Should().HaveCount( 8 );
                    d2.TimeManager.AllObservableTimedEvents.Where( o => !o.IsActive ).Should().HaveCount( 16 );
                }
            }
        }

        [Test]
        public void hundred_timers_from_10_to_1000_ms_in_action()
        {
            const int testTime = 5000;
            AutoCounter[] counters = null;

            using( var d = new ObservableDomain( TestHelper.Monitor, "Test" ) )
            {
                TestHelper.Monitor.Info( $"Creating 100 active counters with interval from 10 to 1000 ms." );
                var tranResult = d.Modify( TestHelper.Monitor, () =>
                {
                    counters = Enumerable.Range( 0, 100 ).Select( i => new AutoCounter( 1000 - i*10 ) ).ToArray();
                } );
                tranResult.Success.Should().BeTrue();
                tranResult.NextDueTimeUtc.Should().BeCloseTo( DateTime.UtcNow, precision: 10 );
                TestHelper.Monitor.Info( $"Waiting for {testTime} ms." );
                Thread.Sleep( testTime );
                tranResult = d.Modify( TestHelper.Monitor, () =>
                {
                    foreach( var c in counters ) c.Stop();
                    TestHelper.Monitor.Info( $"All counters must have a Count that is {testTime}/IntervalMilliSeconds except the 10 ms one: 10 ms is too small (20 ms is okay here)." );
                    var deviants = counters.Select( ( c, idx ) => (Idx: idx, C : c, Delta: c.Count - (testTime / c.IntervalMilliSeconds)) )
                                           .Where( c => Math.Abs( c.Delta ) > 2 );
                    TestHelper.Monitor.Info( deviants.Select( x => $"{x.Idx }: {x.C.Count}, {x.C.IntervalMilliSeconds} ms => {x.Delta}" ).Concatenate( Environment.NewLine ) );
                    deviants.Skip( 1 ).Should().BeEmpty();
                    //
                    TestHelper.Monitor.Info( $"Reconfiguring the 100 active counters with interval from 1000 to 10 ms and restart them." );
                    for( int i = 0; i < counters.Length; ++i )
                    {
                        var c = counters[i];
                        int before = c.IntervalMilliSeconds;
                        c.Reconfigure( DateTime.UtcNow, (i+1) * 10 );
                        TestHelper.Monitor.Info( $"{before} => {c.IntervalMilliSeconds} (Count:{c.Count}." );
                    }
                    foreach( var c in counters ) c.Restart();
                    counters.Should().Match( c => c.All( x => x.Count == 0 ) );
                } );
                tranResult.Success.Should().BeTrue();
                TestHelper.Monitor.Info( $"Waiting for {testTime} ms again." );
                Thread.Sleep( testTime );
                TestHelper.Monitor.Info( $"Same as before: All counters must have a Count that is {testTime}/IntervalMilliSeconds except the 10 ms one: 10 ms is too small (20 ms is okay here)." );
                using( d.AcquireReadLock() )
                {
                    var deviants = counters.Select( ( c, idx ) => (Idx: idx, C: c, Delta: c.Count - (testTime / c.IntervalMilliSeconds)) )
                                           .Where( c => Math.Abs( c.Delta ) > 2 );
                    TestHelper.Monitor.Info( deviants.Select( x => $"{x.Idx }: {x.C.Count}, {x.C.IntervalMilliSeconds} ms => {x.Delta}" ).Concatenate( Environment.NewLine ) );
                    deviants.Skip( 1 ).Should().BeEmpty();
                }
            }
        }

        #region Simplified Timer use (code sandbox).
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
                timer.Counter.Should().Match( c => c == 10 || c == 11 || c == 12 );
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

        #endregion


        internal static ObservableDomain SaveAndLoad( ObservableDomain domain )
        {
            using( var s = new MemoryStream() )
            {
                domain.Save( TestHelper.Monitor, s, leaveOpen: true );
                var d = new ObservableDomain( TestHelper.Monitor, domain.DomainName );
                s.Position = 0;
                d.Load( TestHelper.Monitor, s, leaveOpen: true );
                return d;
            }
        }
    }
}
