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
        [SetUp]
        public void BeforeEach()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        [TestCase( -10 )]
        [TestCase( 0 )]
        public void timed_events_trigger_at_the_end_of_the_Modify_for_immediate_handling( int timeout )
        {
            IReadOnlyList<ActivityMonitorSimpleCollector.Entry> entries = null;

            using( TestHelper.Monitor.CollectEntries( e => entries = e, LogLevelFilter.Info ) )
            using( var d = new ObservableDomain( TestHelper.Monitor, nameof( timed_events_trigger_at_the_end_of_the_Modify_for_immediate_handling ) ) )
            {
                d.Modify( TestHelper.Monitor, () =>
                {
                    var o = new ObservableReminder( DateTime.UtcNow.AddMilliseconds( timeout ) );
                    o.Elapsed += StaticElapsed;
                    TestHelper.Monitor.Info( "Before!" );
                } );
                TestHelper.Monitor.Info( "After!" );
                d.TimeManager.AllObservableTimedEvents.Single().IsActive.Should().BeFalse();
            }
            entries.Select( e => e.Text ).Concatenate().Should().Match( "*Before!*Elapsed:*After!*" );
        }

        static ConcurrentQueue<string> RawTraces = new ConcurrentQueue<string>();

        [Test]
        public void timed_event_trigger_on_Timer_or_at_the_start_of_the_Modify()
        {
            RawTraces.Clear();
            using( var d = new ObservableDomain( TestHelper.Monitor, nameof( timed_event_trigger_on_Timer_or_at_the_start_of_the_Modify ) ) )
            {
                d.Modify( TestHelper.Monitor, () =>
                {
                    var o = new ObservableReminder( DateTime.UtcNow.AddMilliseconds( 50 ) );
                    o.Elapsed += StaticElapsed;
                    RawTraces.Enqueue( "Before!" );
                } );
                RawTraces.Enqueue( "Not Yet!" );
                d.TimeManager.AllObservableTimedEvents.Single().IsActive.Should().BeTrue();
                Thread.Sleep( 50 );
                d.Modify( TestHelper.Monitor, () =>
                {
                    RawTraces.Enqueue( "After!" );
                } );
                d.TimeManager.AllObservableTimedEvents.Single().IsActive.Should().BeFalse();
            }

            RawTraces.Concatenate().Should().Match( "*Before!*Not Yet!*Elapsed:*After!*" );
        }

        class FakeAutoTimer : TimeManager.AutoTimer
        {
            public FakeAutoTimer( ObservableDomain d )
                :base( d )
            {
            }

            protected override Task<(TransactionResult, Exception)> OnDueTimeAsync( IActivityMonitor m ) => Task.FromResult<(TransactionResult, Exception)>((TransactionResult.Empty,null));
        }  


        [Test]
        public void timed_event_trigger_at_the_start_of_the_Modify()
        {
            // Since we use a Fake AutoTimer, we can use the TestHelper.Monitor: everything occur on it.
            IReadOnlyList<ActivityMonitorSimpleCollector.Entry> entries = null;

            using( TestHelper.Monitor.CollectEntries( e => entries = e, LogLevelFilter.Info ) )
            using( var d = new ObservableDomain( TestHelper.Monitor, nameof( timed_event_trigger_at_the_start_of_the_Modify ) ) )
            {
                var autoTimer = d.TimeManager.Timer;
                d.TimeManager.Timer = new FakeAutoTimer( d );
                autoTimer.Dispose();

                d.Modify( TestHelper.Monitor, () =>
                {
                    var o = new ObservableReminder( DateTime.UtcNow.AddMilliseconds( 50 ) );
                    o.Elapsed += StaticElapsed;
                    TestHelper.Monitor.Info( "Before!" );
                } );
                TestHelper.Monitor.Info( "Not Yet!" );
                d.TimeManager.AllObservableTimedEvents.Single().IsActive.Should().BeTrue();
                Thread.Sleep( 50 + 30 );
                d.TimeManager.AllObservableTimedEvents.Single().IsActive.Should().BeTrue( "Auto timer is Fake: +30 ms (whatever the delta is) will never trigger the event." );
                d.Modify( TestHelper.Monitor, () =>
                {
                    TestHelper.Monitor.Info( "After!" );
                } );
                d.TimeManager.AllObservableTimedEvents.Single().IsActive.Should().BeFalse();
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
            const int waitTime = 100 * 10;
            using( var d = new ObservableDomain( TestHelper.Monitor, nameof( auto_counter_works_as_expected ) ) )
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
                d.TimeManager.Timer.WaitForNext();
                using( d.AcquireReadLock() )
                {
                    TestHelper.Monitor.Trace( $"counter.Count = {counter.Count}." );
                    d.AllObjects.Single().Should().BeSameAs( counter );
                    counter.Count.Should().Match( c => c == 10 || c == 11 );
                    relayedCounter.Should().Be( counter.Count );
                }
            }
        }

        [Test]
        public void auto_counter_works_uses_Critical_mode()
        {
            IReadOnlyList<ActivityMonitorSimpleCollector.Entry> entries = null;

            using( TestHelper.Monitor.CollectEntries( e => entries = e ) )
            using( var d = new ObservableDomain( TestHelper.Monitor, nameof( auto_counter_works_uses_Critical_mode ) ) )
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
            using( var d = new ObservableDomain( TestHelper.Monitor, nameof( callbacks_for_reminders_as_well_as_timers_must_be_regular_object_methods_or_static ) ) )
            {
                var tranResult = d.Modify( TestHelper.Monitor, () =>
                {
                    var t = new ObservableTimer(DateTime.UtcNow);
                    Assert.Throws<ArgumentException>( () => t.Elapsed += ( o, e ) => { } );
                    var r = new ObservableReminder( DateTime.UtcNow );
                    Assert.Throws<ArgumentException>( () => r.Elapsed += ( o, e ) => { } );
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
                ValueFromReminders = r.ReadInt32();
            }

            void Write( BinarySerializer w )
            {
                w.Write( Value );
                w.Write( ValueFromReminders );
            }

            public int Value { get; set; }

            public int ValueFromReminders { get; set; }

            public void SilentIncrementValue( object source, EventArgs args )
            {
                Value += 1;
                if( source is ObservableReminder ) ValueFromReminders += 1;
            }

            public void IncrementValue( object source, ObservableTimedEventArgs args )
            {
                Value += 1;
                args.Monitor.Trace( $"[{args.Domain.DomainName}] Value => {Value}" );
                if( source is ObservableReminder )
                {
                    ValueFromReminders += 1;
                    args.Monitor.Trace( $"ValueFromReminders => {ValueFromReminders}" );
                }
            }
        }

        [Test]
        public void serializing_timers_and_reminders()
        {
            var now = DateTime.UtcNow;
            using( var d = new ObservableDomain( TestHelper.Monitor, nameof( serializing_timers_and_reminders ) + " (Primary)" ) )
            {
                var tranResult = d.Modify( TestHelper.Monitor, () =>
                {
                    // Interval: from 1 to 36 ms.
                    // Only half of them (odd ones) are Active.
                    Enumerable.Range( 0, 8 ).Select( i => new ObservableTimer(i.ToString(), now, 1 + i * 5, (i & 1) != 0)).ToArray();
                    d.TimeManager.AllObservableTimedEvents.Where( o => !o.IsActive ).Should().HaveCount( 8 );
                } );
                tranResult.Success.Should().BeTrue();
                using( var d2 = TestHelper.SaveAndLoad( d ) )
                {
                    d2.TimeManager.Timers.Should().HaveCount( 8 );
                    d2.TimeManager.AllObservableTimedEvents.Where( o => !o.IsActive ).Should().HaveCount( 8 );
                }
                TestHelper.Monitor.Info( "Setting callback to timers and creating 5 reminders on Primary Domain." );
                SimpleValue val;
                d.Modify( TestHelper.Monitor, () =>
                {
                    val = new SimpleValue();
                    foreach( var t in d.TimeManager.Timers )
                    {
                        t.Elapsed += val.IncrementValue;
                    }
                    // From 0 ms to 5*9 = 45 ms.
                    foreach( var r in Enumerable.Range( 0, 5 ).Select( i => new ObservableReminder( now.AddMilliseconds( i * 9 ) ) ) )
                    {
                        r.Elapsed += val.SilentIncrementValue;
                    }
                    d.TimeManager.AllObservableTimedEvents.Where( o => o.IsActive ).Should().HaveCount( 4 + 5, "4 timers and 5 reminders are Active." );
                    TestHelper.Monitor.Info( "Leaving Primary Domain configuration." );
                } ).Success.Should().BeTrue();

                Thread.Sleep( 50 );
                d.TimeManager.Timer.WaitForNext();

                int secondaryValue = 0;
                using( TestHelper.Monitor.OpenInfo( "Having slept during 50 ms: now creating Secondary by Save/Load the primary domain." ) )
                {
                    using( var d2 = TestHelper.SaveAndLoad( d, "Secondary" ) )
                    {
                        d2.TimeManager.Timers.Should().HaveCount( 8 );
                        d2.TimeManager.Reminders.Should().HaveCount( 5 );
                        using( d2.AcquireReadLock() )
                        {
                            d2.AllObjects.OfType<SimpleValue>().Single().Value.Should().BeGreaterOrEqualTo( 9, "5 reminders have fired, 4 timers have fired at least once." );
                            d2.TimeManager.Reminders.All( r => !r.IsActive ).Should().BeTrue( "No more Active reminders." );
                            d2.TimeManager.Timers.All( o => o.IsActive == ((int.Parse( o.Name ) & 1) != 0) ).Should().BeTrue();
                            var v = d2.AllObjects.OfType<SimpleValue>().Single();
                            v.ValueFromReminders.Should().Be( 5, "[Secondary] 5 from reminders." );
                            v.Value.Should().BeGreaterOrEqualTo( 9, "[Secondary] 5 reminders have fired, the 4 timers have fired at least once." );
                            secondaryValue = v.Value;
                        }
                    }
                }
                // Wait for next tick...
                d.TimeManager.Timer.WaitForNext();

                using( TestHelper.Monitor.OpenInfo( "Checking value on Primary domain." ) )
                {
                    using( d.AcquireReadLock() )
                    {
                        var v = d.AllObjects.OfType<SimpleValue>().Single();
                        v.ValueFromReminders.Should().Be( 5, "[Primary] 5 from reminders." );
                        v.Value.Should().BeGreaterThan( secondaryValue, "[Primary] Must be greater than the secondary." );
                    }
                }
            }
        }

        [Test]
        public void fifty_timers_from_20_to_1000_ms_in_action()
        {
            const int testTime = 5000;
            AutoCounter[] counters = null;

            using( var d = new ObservableDomain( TestHelper.Monitor, nameof( fifty_timers_from_20_to_1000_ms_in_action ) ) )
            {
                TestHelper.Monitor.Info( $"Creating 50 active counters with interval from 20 to 1000 ms." );
                var tranResult = d.Modify( TestHelper.Monitor, () =>
                {
                    counters = Enumerable.Range( 0, 50 ).Select( i => new AutoCounter( 1000 - i*20 ) ).ToArray();
                } );
                tranResult.Success.Should().BeTrue();
                tranResult.NextDueTimeUtc.Should().BeCloseTo( DateTime.UtcNow.AddMilliseconds( 20 ), precision: 20 );

                TestHelper.Monitor.Info( $"Waiting for {testTime} ms." );
                Thread.Sleep( testTime );
                d.TimeManager.Timer.WaitForNext();

                tranResult = d.Modify( TestHelper.Monitor, () =>
                {
                    foreach( var c in counters ) c.Stop();
                    var deviants = counters.Select( c => (Interval: c.IntervalMilliSeconds, Expected: testTime / c.IntervalMilliSeconds, Delta: c.Count - (testTime / c.IntervalMilliSeconds)) )
                                           .Where( c => Math.Abs( c.Delta ) > 2 );
                    if( deviants.Skip( 1 ).Any() )
                    {
                        using( TestHelper.Monitor.OpenError( $"Deviants detected: all counters must have a Count that is {testTime}/IntervalMilliSeconds except the 20 ms one (too small)." ) )
                        {
                            TestHelper.Monitor.Info( deviants.Select( x => $"[{x.Interval} ms] Expected={x.Expected}, Δ = {x.Delta}" ).Concatenate( Environment.NewLine ) );
                        }
                        return;
                    }
                    else
                    {
                        TestHelper.Monitor.Info( $"For all counters: Count=({testTime}/IntervalMilliSeconds)+/-2 except the 20 ms one (too small)." );
                    }
                    //
                    using( TestHelper.Monitor.OpenInfo( $"Reconfiguring the 100 active counters with interval from 1000 to 10 ms and restart them." ) )
                    {
                        for( int i = 0; i < counters.Length; ++i )
                        {
                            var c = counters[i];
                            c.Reconfigure( DateTime.UtcNow, (i + 1) * 10 );
                        }
                    }
                    foreach( var c in counters ) c.Restart();
                    counters.Should().Match( c => c.All( x => x.Count == 0 ) );
                } );
                tranResult.Success.Should().BeTrue();

                TestHelper.Monitor.Info( $"Waiting for {testTime} ms again." );
                Thread.Sleep( testTime );

                TestHelper.Monitor.Info( $"Same as before: all counters must have a Count that is {testTime}/IntervalMilliSeconds except the 20 ms one (too small)." );
                using( d.AcquireReadLock() )
                {
                    var deviants = counters.Select( c => (Interval: c.IntervalMilliSeconds, Expected: testTime / c.IntervalMilliSeconds, Delta: c.Count - (testTime / c.IntervalMilliSeconds)) )
                                           .Where( c => Math.Abs( c.Delta ) > 2 );
                    if( deviants.Skip( 1 ).Any() )
                    {
                        using( TestHelper.Monitor.OpenError( $"Deviants detected: all counters must have a Count that is {testTime}/IntervalMilliSeconds except the 20 ms one (too small)." ) )
                        {
                            TestHelper.Monitor.Info( deviants.Select( x => $"[{x.Interval} ms] Expected={x.Expected}, Δ = {x.Delta}" ).Concatenate( Environment.NewLine ) );
                        }
                    }
                    else
                    {
                        TestHelper.Monitor.Info( $"For all counters: Count=({testTime}/IntervalMilliSeconds)+/-2 except the 20 ms one (too small)." );
                    }
                }
            }
        }


        static bool ReentrantGuard = false;
        static int AutoTimeFiredSleepTime = 0;
        static int AutoTimeFiredCount = 0;

        static void AutoTime_has_trampoline_OnTimer( object source, ObservableTimedEventArgs args )
        {
            if( ReentrantGuard ) throw new Exception( "AutoTimer guaranties no-reentrancy." );
            ReentrantGuard = true;
            args.Monitor.Debug( $"Fired: {++AutoTimeFiredCount}." );
            if( AutoTimeFiredSleepTime != 0 ) Thread.Sleep( AutoTimeFiredSleepTime );
            ReentrantGuard = false;
        }

        [TestCase( 0 )]
        public void AutoTime_is_obviously_not_reentrant_and_has_a_safety_trampoline( int autoTimeFiredSleepTime )
        {
            var monitor = TestHelper.Monitor;
            AutoTimeFiredSleepTime = autoTimeFiredSleepTime;
            AutoTimeFiredCount = 0;

            using( var d = new ObservableDomain( monitor, nameof( AutoTime_is_obviously_not_reentrant_and_has_a_safety_trampoline ) ) )
            {
                int current = 0, previous = 0, delta = 0;
                void UpdateCount()
                {
                    using( d.AcquireReadLock() )
                    {
                        var c = AutoTimeFiredCount;
                        delta = c - (previous = current);
                        monitor.Info( $"UpdateCount: Δ = " + delta );
                        current = c;
                    }
                }

                d.Modify( TestHelper.Monitor, () =>
                {
                    var t = new ObservableTimer( DateTime.UtcNow, 10 );
                    t.Elapsed += AutoTime_has_trampoline_OnTimer;
                } ).Success.Should().BeTrue();

                d.TimeManager.Timer.WaitForNext();
                UpdateCount();
                delta.Should().BeGreaterThan( 0, "Since we called WaitForNext()." );

                d.TimeManager.Timer.WaitForNext();
                UpdateCount();
                delta.Should().BeGreaterThan( 0, "Since we called WaitForNext() again!" );

                using( d.AcquireReadLock() )
                {
                    TestHelper.Monitor.Info( "Locking the Domain for 200 ms." );
                    Thread.Sleep( 200 );
                }

                d.TimeManager.Timer.WaitForNext();
                UpdateCount();
                delta.Should().BeGreaterThan( 0, "We blocked for 200 ms and called WaitForNext(): at least one Tick should have been raised." );
            }
        }

        [SerializationVersion(0)]
        class TestReminder : InternalObject
        {
            readonly TestCounter _counter;

            public TestReminder( TestCounter counter )
            {
                _counter = counter;
            }

            TestReminder( IBinaryDeserializerContext c )
                : base( c )
            {
                var r = c.StartReading();
                _counter = (TestCounter)r.ReadObject();
            }

            void Write( BinarySerializer w )
            {
                w.WriteObject( _counter );
            }

            public void StartWork( string message, int repeatCount )
            {
                Domain.Remind( DateTime.UtcNow, DisplayMessageAndStartCounting, (message, repeatCount) );
            }

            public void StartTooooooLooooongWork()
            {
                Domain.Remind( DateTime.UtcNow.AddDays( 1 ), DisplayMessageAndStartCounting, ("Will never happen.", 0) );
            }

            void DisplayMessageAndStartCounting( object sender, ObservableReminderEventArgs e )
            {
                e.Reminder.IsPooled.Should().BeTrue();
                e.Reminder.Invoking( x => x.Dispose() ).Should().Throw<InvalidOperationException>( "Pooled reminders cannot be disposed." );

                var (msg, count) = ((string,int))e.Reminder.Tag;

                if( msg == "Will never happen." ) throw new Exception( "TestReminder: " + msg );
                Domain.Monitor.Info( $"TestReminder: Working: {msg} (count:{count})" );
                if( count > 0 )
                {
                    e.Reminder.DueTimeUtc = DateTime.UtcNow.AddMilliseconds( 80 );
                    e.Reminder.Tag = (msg, --count);
                }
                // Increment the counter from another reminder :).
                if( _counter != null ) Domain.Remind( DateTime.UtcNow.AddMilliseconds( 20 ), _counter.Increment );
            }
        }

        [TestCase( "WithIntermediateSaves" )]
        [TestCase( "" )]
        public void reminder_helper_uses_pooled_ObservableReminders( string mode )
        {
            IReadOnlyList<ActivityMonitorSimpleCollector.Entry> logs = null;
            using( var d = TestHelper.CreateDomainHandler( nameof( reminder_helper_uses_pooled_ObservableReminders)+mode ) )
            {
                TimeSpan ReloadIfNeeded()
                {
                    var n = DateTime.UtcNow;
                    if( mode == "WithIntermediateSaves" ) d.Reload( TestHelper.Monitor );
                    return DateTime.UtcNow - n;
                }

                using( TestHelper.Monitor.CollectEntries( entries => logs = entries, LogLevelFilter.Info ) )
                {
                    d.Domain.Modify( TestHelper.Monitor, () =>
                    {
                        var counter = new TestCounter();
                        var r1 = new TestReminder( counter );
                        r1.StartWork( "Hello!", 3 );

                    } ).Success.Should().BeTrue();
                    TimeSpan reloadDelta = ReloadIfNeeded();
                    Thread.Sleep( 5 * 100 + (int)reloadDelta.TotalMilliseconds );
                    ReloadIfNeeded();
                }
                logs.Select( l => l.Text ).Should().Contain( "TestReminder: Working: Hello! (count:3)", "The 2 other logs are on the domain monitor!" );
                using( d.Domain.AcquireReadLock() )
                {
                    d.Domain.TimeManager.Reminders.Should().HaveCount( 2, "2 pooled reminders have been created." );
                    d.Domain.AllInternalObjects.OfType<TestCounter>().Single().Count.Should().BeGreaterOrEqualTo( 4, "Counter has been incremented at least four times." );
                    d.Domain.TimeManager.Reminders.All( r => r.IsPooled && !r.IsActive && !r.IsDisposed ).Should().BeTrue( "Reminders are free to be reused." );
                }
                ReloadIfNeeded();
                using( TestHelper.Monitor.CollectEntries( entries => logs = entries, LogLevelFilter.Info ) )
                {
                    d.Domain.Modify( TestHelper.Monitor, () =>
                    {
                        var r2 = new TestReminder( null );
                        r2.StartWork( "Another Job!", 0 );

                    } ).Success.Should().BeTrue();
                    ReloadIfNeeded();
                }
                logs.Select( l => l.Text ).Should().Contain( "TestReminder: Working: Another Job! (count:0)" );
                using( d.Domain.AcquireReadLock() )
                {
                    d.Domain.TimeManager.Reminders.Should().HaveCount( 2, "Still 2 pooled reminders." );
                    d.Domain.TimeManager.Reminders.All( r => r.IsPooled && !r.IsActive && !r.IsDisposed ).Should().BeTrue( "Reminders are free to be reused." );
                }
                ReloadIfNeeded();
                d.Domain.Modify( TestHelper.Monitor, () =>
                {
                    var r = d.Domain.AllInternalObjects.OfType<TestReminder>().First();
                    r.StartTooooooLooooongWork();

                } ).Success.Should().BeTrue();
                ReloadIfNeeded();
                using( d.Domain.AcquireReadLock() )
                {
                    d.Domain.TimeManager.Reminders.Should().HaveCount( 2, "Still 2 pooled reminders." );
                    d.Domain.TimeManager.Reminders.Where( r => !r.IsActive ).Should().HaveCount( 1, "One is in used." );
                }
                ReloadIfNeeded();
                d.Domain.Modify( TestHelper.Monitor, () =>
                {
                    var r3 = new TestReminder( null );
                    r3.StartTooooooLooooongWork();

                } ).Success.Should().BeTrue();
                ReloadIfNeeded();
                using( d.Domain.AcquireReadLock() )
                {
                    d.Domain.TimeManager.Reminders.Should().HaveCount( 2, "Still 2 pooled reminders." );
                    d.Domain.TimeManager.Reminders.Where( r => !r.IsActive ).Should().BeEmpty( "No more free reminders!" );
                }
                ReloadIfNeeded();
                d.Domain.Modify( TestHelper.Monitor, () =>
                {
                    var r4 = new TestReminder( null );
                    r4.StartTooooooLooooongWork();

                } ).Success.Should().BeTrue();
                ReloadIfNeeded();
                using( d.Domain.AcquireReadLock() )
                {
                    d.Domain.TimeManager.Reminders.Should().HaveCount( 3, "A third one has been required!" );
                    d.Domain.TimeManager.Reminders.Where( r => !r.IsActive ).Should().BeEmpty( "All 3 are in use." );
                }
                ReloadIfNeeded();
                d.Domain.Modify( TestHelper.Monitor, () =>
                {
                    foreach( var r in d.Domain.TimeManager.Reminders )
                    {
                        r.DueTimeUtc = Util.UtcMinValue;
                    }

                } ).Success.Should().BeTrue();
                ReloadIfNeeded();
                using( d.Domain.AcquireReadLock() )
                {
                    d.Domain.TimeManager.Reminders.Should().HaveCount( 3, "3 created..." );
                    d.Domain.TimeManager.Reminders.Where( r => !r.IsActive ).Should().HaveCount( 3, "... and free to be reused." );
                }
            }
        }
    }
}
