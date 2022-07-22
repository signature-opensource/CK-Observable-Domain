using CK.Core;
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
        public async Task timed_events_trigger_at_the_end_of_the_Modify_for_immediate_handling_Async( int timeout )
        {
            IReadOnlyList<ActivityMonitorSimpleCollector.Entry> entries = null!;

            using( TestHelper.Monitor.CollectEntries( e => entries = e, LogLevelFilter.Info ) )
            using( var d = new ObservableDomain( TestHelper.Monitor, nameof( timed_events_trigger_at_the_end_of_the_Modify_for_immediate_handling_Async ), startTimer: true ) )
            {
                await d.ModifyThrowAsync( TestHelper.Monitor, () =>
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
        public async Task timed_event_trigger_on_Timer_or_at_the_start_of_the_Modify_Async()
        {
            RawTraces.Clear();
            using( var d = new ObservableDomain( TestHelper.Monitor, nameof( timed_event_trigger_on_Timer_or_at_the_start_of_the_Modify_Async ), startTimer: true ) )
            {
                await d.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    var o = new ObservableReminder( DateTime.UtcNow.AddMilliseconds( 50 ) );
                    o.Elapsed += StaticElapsed;
                    RawTraces.Enqueue( "Before!" );
                } );
                RawTraces.Enqueue( "Not Yet!" );
                d.TimeManager.AllObservableTimedEvents.Single().IsActive.Should().BeTrue();
                Thread.Sleep( 50 );
                await d.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    RawTraces.Enqueue( "After!" );
                } );
                d.TimeManager.AllObservableTimedEvents.Single().IsActive.Should().BeFalse();
            }

            RawTraces.Concatenate().Should().Match( "*Before!*Not Yet!*Elapsed:*After!*" );
        }

        [Test]
        public async Task timed_event_trigger_at_the_start_of_the_Modify_Async()
        {
            IReadOnlyList<ActivityMonitorSimpleCollector.Entry> entries = null!;

            //
            // We start the AutoTimer here otherwise the timed events are not processed.
            // To be able to test at_the_start_of_the_Modify we need to do an awful thing: dispose the actual timer.
            //
            using( TestHelper.Monitor.CollectEntries( e => entries = e, LogLevelFilter.Info ) )
            using( var d = new ObservableDomain( TestHelper.Monitor, nameof( timed_event_trigger_at_the_start_of_the_Modify_Async ), startTimer: true ) )
            {
                // Here is the infamy.
                // A disposed timer is silent...
                d.TimeManager.Timer.Dispose();

                await d.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    var o = new ObservableReminder( DateTime.UtcNow.AddMilliseconds( 20 ) );
                    o.Elapsed += StaticElapsed;
                    TestHelper.Monitor.Info( "Before!" );
                } );

                d.Read( TestHelper.Monitor, () => d.TimeManager.AllObservableTimedEvents.Single().IsActive ).Should().BeTrue( "Not yet." );

                Thread.Sleep( 30 );

                d.Read( TestHelper.Monitor, () =>
                {
                    d.TimeManager.AllObservableTimedEvents.Single().IsActive.Should().BeTrue( "No actual timer: +30 ms (whatever the delta is) will never trigger the event." );
                } );

                await d.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    // "Elapsed: " is already here.
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

        static int RelayedCounter = 0;
        static void Counter_CountChanged( object o ) => ++RelayedCounter;

        [Test]
        public async Task auto_counter_works_as_expected_Async()
        {
            RelayedCounter = 0;
            const int waitTime = 100 * 10 + 200 /*Security*/;
            using( var d = new ObservableDomain( TestHelper.Monitor, nameof( auto_counter_works_as_expected_Async ), startTimer: true ) )
            {
                StupidAutoCounter counter = null!;
                await d.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    counter = new StupidAutoCounter( 100 );
                    counter.CountChanged += Counter_CountChanged;

                } );
                d.Read( TestHelper.Monitor, () =>
                {
                    TestHelper.Monitor.Trace( $"new AutoCounter( 100 ) done. Waiting {waitTime} ms. NextDueTime: '{d.TimeManager.Timer.NextDueTime}'." );

                } );

                Thread.Sleep( waitTime );

                d.Read( TestHelper.Monitor, () =>
                {
                    TestHelper.Monitor.Trace( $"End of Waiting. counter.Count = {counter.Count}. NextDueTime: '{d.TimeManager.Timer.NextDueTime}'." );
                } );

                d.TimeManager.Timer.WaitForNext( 200 ).Should().BeTrue( "AutoTimer must NOT be dead." );

                d.Read( TestHelper.Monitor, () =>
                {
                    TestHelper.Monitor.Trace( $"counter.Count = {counter.Count}." );
                    d.AllObjects.Single().Should().BeSameAs( counter );
                    counter.Count.Should().Match( c => c >= 11 );
                    RelayedCounter.Should().Be( counter.Count );
                } );
            }
        }

        [Test]
        public async Task auto_counter_works_uses_Critical_mode_Async()
        {
            IReadOnlyList<ActivityMonitorSimpleCollector.Entry> entries = null;

            using( TestHelper.Monitor.CollectEntries( e => entries = e ) )
            using( var d = new ObservableDomain( TestHelper.Monitor, nameof( auto_counter_works_uses_Critical_mode_Async ), startTimer: true ) )
            {
                StupidAutoCounter counter = null!;
                await d.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    counter = new StupidAutoCounter( 5 );
                    // We rely on the end of the Modify that execute pending timers
                    // (timed events are handled at the start AND the end of the Modify).
                    Thread.Sleep( 200 );
                    // Here, there is one execution.
                } );

                // This is not really safe: the timer MAY be fired here before we acquire the read lock:
                // this is why we allow the counter to be greater than 1...
                d.Read( TestHelper.Monitor, () =>
                {
                    TestHelper.Monitor.Trace( $"counter.Count = {counter.Count}." );
                    d.AllObjects.Single().Should().BeSameAs( counter );
                    counter.Count.Should().BeGreaterOrEqualTo( 1 );
                } );
            }
            entries.Should().Match( e => e.Any( m => m.Text.Contains( " event(s) lost!" ) ), "We have lost events (around 40)." );
        }

        [Test]
        public async Task callbacks_for_reminders_as_well_as_timers_must_be_regular_object_methods_or_static_Async()
        {
            using( var d = new ObservableDomain( TestHelper.Monitor, nameof( callbacks_for_reminders_as_well_as_timers_must_be_regular_object_methods_or_static_Async ), startTimer: true ) )
            {
                await d.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    var t = new ObservableTimer( DateTime.UtcNow );
                    Assert.Throws<ArgumentException>( () => t.Elapsed += ( o, e ) => { } );
                    var r = new ObservableReminder( DateTime.UtcNow );
                    Assert.Throws<ArgumentException>( () => r.Elapsed += ( o, e ) => { } );
                } );
            }
        }

        [SerializationVersion(0)]
        sealed class SimpleValue : ObservableObject
        {
            public SimpleValue()
            {
            }

            SimpleValue( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
                : base( BinarySerialization.Sliced.Instance )
            {
                Value = r.Reader.ReadInt32();
                ValueFromReminders = r.Reader.ReadInt32();
            }

            public static void Write( BinarySerialization.IBinarySerializer w, in SimpleValue o )
            {
                w.Writer.Write( o.Value );
                w.Writer.Write( o.ValueFromReminders );
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
        public async Task serializing_timers_and_reminders_Async()
        {
            var now = DateTime.UtcNow;
            using( var d = new ObservableDomain( TestHelper.Monitor, nameof( serializing_timers_and_reminders_Async ) + " (Primary)", startTimer: true ) )
            {
                await d.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    // Interval: from 1 to 36 ms.
                    // Only half of them (odd ones) are Active.
                    Enumerable.Range( 0, 8 ).Select( i => new ObservableTimer(i.ToString(), now, 1 + i * 5, (i & 1) != 0)).ToArray();
                    d.TimeManager.AllObservableTimedEvents.Where( o => !o.IsActive ).Should().HaveCount( 8 );

                } );

                using( var d2 = TestHelper.CloneDomain( d, initialDomainDispose: false ) )
                {
                    d2.TimeManager.Timers.Should().HaveCount( 8 );
                    d2.TimeManager.AllObservableTimedEvents.Where( o => !o.IsActive ).Should().HaveCount( 8 );
                }

                TestHelper.Monitor.Info( "Setting callback to timers and creating 5 reminders on Primary Domain." );
                SimpleValue val;
                await d.ModifyThrowAsync( TestHelper.Monitor, () =>
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

                } );

                Thread.Sleep( 50 );
                d.TimeManager.Timer.WaitForNext();

                int secondaryValue = 0;
                using( TestHelper.Monitor.OpenInfo( "Having slept during 50 ms: now creating Secondary by Save/Load the primary domain." ) )
                {
                    using( var d2 = TestHelper.CloneDomain( d, newName: "Secondary", initialDomainDispose: false ) )
                    {
                        d2.Read( TestHelper.Monitor, () =>
                        {
                            d2.TimeManager.Timers.Should().HaveCount( 8 );
                            d2.TimeManager.Reminders.Should().HaveCount( 5 );
                            d2.AllObjects.OfType<SimpleValue>().Single().Value.Should().BeGreaterOrEqualTo( 9, "5 reminders have fired, 4 timers have fired at least once." );
                            d2.TimeManager.Reminders.All( r => !r.IsActive ).Should().BeTrue( "No more Active reminders." );
                            d2.TimeManager.Timers.All( o => o.IsActive == ((int.Parse( o.Name ) & 1) != 0) ).Should().BeTrue();
                            var v = d2.AllObjects.OfType<SimpleValue>().Single();
                            v.ValueFromReminders.Should().Be( 5, "[Secondary] 5 from reminders." );
                            v.Value.Should().BeGreaterOrEqualTo( 9, "[Secondary] 5 reminders have fired, the 4 timers have fired at least once." );
                            secondaryValue = v.Value;
                        } );
                    }
                }
                // Wait for next tick...
                d.TimeManager.Timer.WaitForNext();

                using( TestHelper.Monitor.OpenInfo( "Checking value on Primary domain." ) )
                {
                    d.Read( TestHelper.Monitor, () =>
                    {
                        var v = d.AllObjects.OfType<SimpleValue>().Single();
                        v.ValueFromReminders.Should().Be( 5, "[Primary] 5 from reminders." );
                        v.Value.Should().BeGreaterThan( secondaryValue, "[Primary] Must be greater than the secondary." );
                    } );
                }
            }
        }

        [Test]
        [Explicit]
        public async Task fifty_timers_from_20_to_1000_ms_in_action_Async()
        {
            const int testTime = 5000;
            StupidAutoCounter[] counters = null!;

            using( var d = new ObservableDomain( TestHelper.Monitor, nameof( fifty_timers_from_20_to_1000_ms_in_action_Async ), startTimer: true ) )
            {
                TestHelper.Monitor.Info( $"Creating 50 active counters with interval from 20 to 1000 ms." );
                await d.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    counters = Enumerable.Range( 0, 50 ).Select( i => new StupidAutoCounter( 1000 - i*20 ) ).ToArray();
                } );

                TestHelper.Monitor.Info( $"Waiting for {testTime} ms." );
                Thread.Sleep( testTime );

                await d.ModifyThrowAsync( TestHelper.Monitor, () =>
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

                TestHelper.Monitor.Info( $"Waiting for {testTime} ms again." );
                Thread.Sleep( testTime );

                TestHelper.Monitor.Info( $"Same as before: all counters must have a Count that is {testTime}/IntervalMilliSeconds except the 20 ms one (too small)." );
                d.Read( TestHelper.Monitor, () =>
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
                } );
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
        public async Task AutoTime_is_obviously_not_reentrant_and_has_a_safety_trampoline_Async( int autoTimeFiredSleepTime )
        {
            var monitor = TestHelper.Monitor;
            AutoTimeFiredSleepTime = autoTimeFiredSleepTime;
            AutoTimeFiredCount = 0;

            using( var d = new ObservableDomain( monitor, nameof( AutoTime_is_obviously_not_reentrant_and_has_a_safety_trampoline_Async ), startTimer: true ) )
            {
                int current = 0, previous = 0, delta = 0;
                void UpdateCount()
                {
                    d.Read( monitor, () =>
                    {
                        var c = AutoTimeFiredCount;
                        delta = c - (previous = current);
                        monitor.Info( $"UpdateCount: Δ = " + delta );
                        current = c;
                    } );
                }

                await d.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    var t = new ObservableTimer( DateTime.UtcNow, 10 );
                    t.Elapsed += AutoTime_has_trampoline_OnTimer;
                } );

                d.TimeManager.Timer.WaitForNext();
                UpdateCount();
                delta.Should().BeGreaterThan( 0, "Since we called WaitForNext()." );

                d.TimeManager.Timer.WaitForNext();
                UpdateCount();
                delta.Should().BeGreaterThan( 0, "Since we called WaitForNext() again!" );

                d.Read( TestHelper.Monitor, () =>
                {
                    TestHelper.Monitor.Info( "Locking the Domain for 200 ms." );
                    Thread.Sleep( 200 );
                } );

                d.TimeManager.Timer.WaitForNext();
                UpdateCount();
                delta.Should().BeGreaterThan( 0, "We blocked for 200 ms and called WaitForNext(): at least one Tick should have been raised." );
            }
        }

        [SerializationVersion(0)]
        class TestReminder : InternalObject
        {
            readonly TestCounter? _counter;

            public TestReminder( TestCounter? counter )
            {
                _counter = counter;
            }

            TestReminder( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
                : base( BinarySerialization.Sliced.Instance )
            {
                _counter = r.ReadNullableObject<TestCounter>();
            }

            public static void Write( BinarySerialization.IBinarySerializer w, in TestReminder o )
            {
                w.WriteNullableObject( o._counter );
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
                e.Reminder.Invoking( x => x.Destroy() ).Should().Throw<InvalidOperationException>( "Pooled reminders cannot be disposed." );

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
        public async Task reminder_helper_uses_pooled_ObservableReminders_Async( string mode )
        {
            IReadOnlyList<ActivityMonitorSimpleCollector.Entry> logs = null!;
            using( var d = TestHelper.CreateDomainHandler( nameof( reminder_helper_uses_pooled_ObservableReminders_Async)+mode, startTimer: true, serviceProvider: null ) )
            {
                TimeSpan ReloadIfNeeded()
                {
                    var n = DateTime.UtcNow;
                    if( mode == "WithIntermediateSaves" ) d.ReloadNewDomain( TestHelper.Monitor );
                    return DateTime.UtcNow - n;
                }

                using( TestHelper.Monitor.CollectEntries( entries => logs = entries, LogLevelFilter.Info ) )
                {
                    await d.Domain.ModifyThrowAsync( TestHelper.Monitor, () =>
                    {
                        var counter = new TestCounter();
                        var r1 = new TestReminder( counter );
                        r1.StartWork( "Hello!", 3 );

                    } );
                    TimeSpan reloadDelta = ReloadIfNeeded();
                    Thread.Sleep( 3 * 100 + (int)reloadDelta.TotalMilliseconds + 100/*Security*/ );
                    ReloadIfNeeded();
                }
                logs.Select( l => l.Text ).Should().Contain( "TestReminder: Working: Hello! (count:3)", "The 2 other logs are on the domain monitor!" );
                d.Domain.Read( TestHelper.Monitor, () =>
                {
                    d.Domain.TimeManager.Reminders.Should().HaveCount( 2, "2 pooled reminders have been created." );
                    d.Domain.AllInternalObjects.OfType<TestCounter>().Single().Count.Should().BeGreaterOrEqualTo( 4, "Counter has been incremented at least four times." );
                    d.Domain.TimeManager.Reminders.All( r => r.IsPooled && !r.IsActive && !r.IsDestroyed ).Should().BeTrue( "Reminders are free to be reused." );
                } );
                ReloadIfNeeded();
                using( TestHelper.Monitor.CollectEntries( entries => logs = entries, LogLevelFilter.Info ) )
                {
                    await d.Domain.ModifyThrowAsync( TestHelper.Monitor, () =>
                    {
                        var r2 = new TestReminder( null );
                        r2.StartWork( "Another Job!", 0 );

                    } );
                    ReloadIfNeeded();
                }
                logs.Select( l => l.Text ).Should().Contain( "TestReminder: Working: Another Job! (count:0)" );
                d.Domain.Read( TestHelper.Monitor, () =>
                {
                    d.Domain.TimeManager.Reminders.Should().HaveCount( 2, "Still 2 pooled reminders." );
                    d.Domain.TimeManager.Reminders.All( r => r.IsPooled && !r.IsActive && !r.IsDestroyed ).Should().BeTrue( "Reminders are free to be reused." );
                } );
                ReloadIfNeeded();
                await d.Domain.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    var r = d.Domain.AllInternalObjects.OfType<TestReminder>().First();
                    r.StartTooooooLooooongWork();

                } );
                ReloadIfNeeded();
                d.Domain.Read( TestHelper.Monitor, () =>
                {
                    d.Domain.TimeManager.Reminders.Should().HaveCount( 2, "Still 2 pooled reminders." );
                    d.Domain.TimeManager.Reminders.Where( r => !r.IsActive ).Should().HaveCount( 1, "One is in used." );
                } );
                ReloadIfNeeded();
                await d.Domain.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    var r3 = new TestReminder( null );
                    r3.StartTooooooLooooongWork();

                } );
                ReloadIfNeeded();
                d.Domain.Read( TestHelper.Monitor, () =>
                {
                    d.Domain.TimeManager.Reminders.Should().HaveCount( 2, "Still 2 pooled reminders." );
                    d.Domain.TimeManager.Reminders.Where( r => !r.IsActive ).Should().BeEmpty( "No more free reminders!" );
                } );
                ReloadIfNeeded();
                await d.Domain.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    var r4 = new TestReminder( null );
                    r4.StartTooooooLooooongWork();

                } );
                ReloadIfNeeded();
                d.Domain.Read( TestHelper.Monitor, () =>
                {
                    d.Domain.TimeManager.Reminders.Should().HaveCount( 3, "A third one has been required!" );
                    d.Domain.TimeManager.Reminders.Where( r => !r.IsActive ).Should().BeEmpty( "All 3 are in use." );
                } );
                ReloadIfNeeded();
                await d.Domain.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    foreach( var r in d.Domain.TimeManager.Reminders )
                    {
                        r.DueTimeUtc = Util.UtcMinValue;
                    }

                } );
                ReloadIfNeeded();
                d.Domain.Read( TestHelper.Monitor, () =>
                {
                    d.Domain.TimeManager.Reminders.Should().HaveCount( 3, "3 created..." );
                    d.Domain.TimeManager.Reminders.Where( r => !r.IsActive ).Should().HaveCount( 3, "... and free to be reused." );
                } );
            }
        }

        [Test]
        public async Task testing_reminders_Async()
        {
            using var d = new ObservableDomain( TestHelper.Monitor, nameof( testing_reminders_Async ), startTimer: true );

            var dates = Enumerable.Range( 0, 100 ).Select( i => DateTime.UtcNow.AddDays( 1 + i ) ).ToArray();
            var revert = dates.Reverse().ToArray();
            var random = new Random();

            static void RequiredForActivation( object sender, ObservableReminderEventArgs e ) { }

            await CreateDatesAsync( d, dates );
            await ApplyDatesAsync( d, revert );
            await DisposeAllRemindersAsync( d, false );

            await CreateDatesAsync( d, revert );
            await ApplyDatesAsync( d, dates );
            await DisposeAllRemindersAsync( d, true );

            for( int i = 0; i < 200; ++i )
            {
                await CreateDatesAsync( d, dates );
                await ApplyDatesAsync( d, Shuffled() );
                await ApplyDatesAsync( d, Shuffled() );
                await DisposeAllRemindersAsync( d, true );
            }
            for( int i = 0; i < 200; ++i )
            {
                await CreateDatesAsync( d, Shuffled() );
                await DisposeAllRemindersAsync( d, true );
            }

            static Task CreateDatesAsync( ObservableDomain d, DateTime[] dates )
            {
                return d.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    for( int i = 0; i < dates.Length; ++i )
                    {
                        var o = new ObservableReminder( dates[i] );
                        o.Elapsed += RequiredForActivation;
                    }
                } );
            }

            static Task ApplyDatesAsync( ObservableDomain d, DateTime[] newDates )
            {
                return d.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    for( int i = 0; i < newDates.Length; ++i )
                    {
                        d.TimeManager.Reminders.ElementAt( i ).DueTimeUtc = newDates[i];
                    }
                } );
            }

            Task DisposeAllRemindersAsync( ObservableDomain d, bool rand )
            {
                return d.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    while( d.TimeManager.Reminders.Count > 0 )
                    {
                        d.TimeManager.Reminders.ElementAt( random.Next( d.TimeManager.Reminders.Count ) ).Destroy();
                    }
                } );
            }

            DateTime[] Shuffled()
            {
                return dates.OrderBy( x => random.Next() ).ToArray();
            }
        }


        [Test]
        public async Task auto_destroying_reminders_Async()
        {
            using var od = new ObservableDomain<Root>( TestHelper.Monitor, nameof( auto_destroying_reminders_Async ), true );

            await od.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                var r = new Random();
                var m = new Machine();
                od.Root.Objects.Add( m );
                for( int i = 0; i < 500; ++i )
                {
                    var o = new ObservableProductSample( m );
                    od.Root.Objects.Add( o );
                    o.SetAutoDestroyTimeout( TimeSpan.FromMilliseconds( r.Next( 100 ) ) );
                }
                for( int i = 0; i < 100; ++i )
                {
                    var o = new ObservableProductSample( m );
                    od.Root.Objects.Add( o );
                    o.SetAutoDestroyTimeout( TimeSpan.FromDays( 1 ) );
                }
            } );

            await Task.Delay( 200 );

            ObservableDomain.IdempotenceSerializationCheck( TestHelper.Monitor, od );

            od.Read( TestHelper.Monitor, () =>
            {
                int i = 1;
                while( i < 501 )
                {
                    od.Root.Objects[i++].IsDestroyed.Should().BeTrue();
                }
                while( i < 601 )
                {
                    od.Root.Objects[i++].IsDestroyed.Should().BeFalse();
                }
            } );

            ObservableDomain.IdempotenceSerializationCheck( TestHelper.Monitor, od );
        }

    }
}
