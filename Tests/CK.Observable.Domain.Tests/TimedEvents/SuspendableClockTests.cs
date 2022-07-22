using CK.Core;
using CK.Observable.Domain.Tests.Sample;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Domain.Tests.TimedEvents
{
    [TestFixture]
    public class SuspendableClockTests
    {
        [SetUp]
        public void BeforeEach()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        static ConcurrentQueue<string> RawTraces = new ConcurrentQueue<string>();

        [Test]
        public async Task Ô_temps_suspend_ton_vol_Async()
        {
            const int deltaTime = 200;

            using( var d = new ObservableDomain( TestHelper.Monitor, nameof( Ô_temps_suspend_ton_vol_Async ), startTimer: true ) )
            {
                StupidAutoCounter counter = null;
                SuspendableClock clock = null;
                await d.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    counter = new StupidAutoCounter( deltaTime );
                    counter.IsRunning.Should().BeTrue();

                    clock = new SuspendableClock();
                    counter.SuspendableClock = clock;

                    clock.IsActive.Should().BeTrue();
                    counter.IsRunning.Should().BeTrue();

                } );

                int intermediateCount = 0;
                Thread.Sleep( deltaTime * 6 );
                await d.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    TestHelper.Monitor.Trace( $"counter.Count = {counter.Count}." );
                    (intermediateCount = counter.Count).Should().Match( c => c == 6 || c == 7 );
                    clock.IsActive = false;
                    counter.IsRunning.Should().BeFalse( "The bound clock is suspended." );

                } );

                Thread.Sleep( deltaTime * 5 );
                d.Read( TestHelper.Monitor, () =>
                {
                    TestHelper.Monitor.Trace( $"counter.Count = {counter.Count}." );
                    counter.Count.Should().Be( intermediateCount );
                } );
                await d.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    clock.IsActive = true;
                    counter.IsRunning.Should().BeTrue( "Back to business." );

                } );

                Thread.Sleep( deltaTime * 5 );
                d.Read( TestHelper.Monitor, () =>
                {
                    TestHelper.Monitor.Trace( $"counter.Count = {counter.Count}." );
                    counter.Count.Should().BeCloseTo( intermediateCount + 5, 2 );
                } );
            }
        }

        const int enoughMilliseconds = 500;

        [TestCase( "" )]
        [TestCase( "CumulateUnloadedTime = false" )]
        public async Task SuspendableClock_CumulateUnloadedTime_tests_Async( string mode )
        {
            using var handler = TestHelper.CreateDomainHandler( nameof( SuspendableClock_CumulateUnloadedTime_tests_Async ), startTimer: true, serviceProvider: null );

            DateTime initialExpected = Util.UtcMinValue;
            await handler.Domain.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                var r = new ObservableReminder( DateTime.UtcNow.AddMilliseconds( enoughMilliseconds / 2 ) );
                var c = new SuspendableClock();
                Debug.Assert( c.IsActive && c.CumulateUnloadedTime, "By default, CumulateUnloadedTime is true." );
                if( mode == "CumulateUnloadedTime = false" )
                {
                    c.CumulateUnloadedTime = false;
                }
                r.Elapsed += Reminder_Elapsed;
                r.SuspendableClock = c;
                r.IsActive.Should().BeTrue( "The clock is active and the duetime is later." );

                initialExpected = r.DueTimeUtc;
            } );
            // We pause for 2*enoughMilliseconds before reloading.
            handler.ReloadNewDomain( TestHelper.Monitor, idempotenceCheck: false, pauseReloadMilliseconds: 2*enoughMilliseconds );
            var minUpdated = initialExpected.AddMilliseconds( 2 * enoughMilliseconds );
            // Using Modify to trigger the timed events without waiting for the AutoTimer to fire.
            await handler.Domain.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                var r = handler.Domain.TimeManager.Reminders.Single();
                if( mode == "CumulateUnloadedTime = false" )
                {
                    r.IsActive.Should().BeFalse( "Already fired." );
                    r.DueTimeUtc.Should().Be( Util.UtcMinValue );
                }
                else
                {
                    r.DueTimeUtc.Should().BeCloseTo( minUpdated, TimeSpan.FromMilliseconds( enoughMilliseconds / 6 ) );
                }
            } );
        }

        [Test]
        public async Task SuspendableClock_serialization_Async()
        {
            ReminderHasElapsed = false;
            ClockIsActiveChanged = false;

            using var handler = TestHelper.CreateDomainHandler( nameof( SuspendableClock_serialization_Async ), startTimer: true, serviceProvider: null );
            StupidAutoCounter counter = null!;
            ObservableReminder reminder = null!;
            SuspendableClock clock = null!;
            await handler.Domain.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                counter = new StupidAutoCounter( (3 * enoughMilliseconds) / 5 );
                counter.IsRunning.Should().BeTrue();
                reminder = new ObservableReminder( DateTime.UtcNow.AddMilliseconds( enoughMilliseconds / 2 ) );
                reminder.Elapsed += Reminder_Elapsed;

                clock = new SuspendableClock();
                counter.SuspendableClock = clock;
                reminder.SuspendableClock = clock;
                clock.IsActiveChanged += Clock_IsActiveChanged;

                clock.IsActive.Should().BeTrue();
                counter.IsRunning.Should().BeTrue();

            } );


            handler.Domain.Read( TestHelper.Monitor, () =>
            {
                ClockIsActiveChanged.Should().BeFalse();
                ReminderHasElapsed.Should().BeFalse();
                counter.Count.Should().Be( 1, "AutoCounter fires immediately." );
            } );

            handler.ReloadNewDomain( TestHelper.Monitor, idempotenceCheck: false );

            handler.Domain.Read( TestHelper.Monitor, () =>
            {
                ReminderHasElapsed.Should().BeFalse();
                ClockIsActiveChanged.Should().BeFalse();

                counter = handler.Domain.AllObjects.OfType<StupidAutoCounter>().Single();
                clock = handler.Domain.AllInternalObjects.OfType<SuspendableClock>().Single();
                reminder = handler.Domain.TimeManager.Reminders.Single();
            } );
            TestHelper.Monitor.Info( "Start sleeping..." );
            Thread.Sleep( enoughMilliseconds );
            TestHelper.Monitor.Info( "End sleeping..." );
            handler.Domain.Read( TestHelper.Monitor, () =>
            {
                ReminderHasElapsed.Should().BeTrue();
                ClockIsActiveChanged.Should().BeFalse();

                reminder.IsActive.Should().BeFalse( "Reminder fired. Is is now inactive." );
                counter.Count.Should().Be( 2 );
                counter.IsRunning.Should().BeTrue();
            } );
            await handler.Domain.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                ClockIsActiveChanged.Should().BeFalse();
                clock.IsActive = false;
                ClockIsActiveChanged.Should().BeTrue();
                ClockIsActiveChanged = false;

                counter.IsRunning.Should().BeFalse();
                reminder.IsActive.Should().BeFalse();

            } );

            ReminderHasElapsed = false;
            handler.ReloadNewDomain( TestHelper.Monitor, idempotenceCheck: true );
            Thread.Sleep( enoughMilliseconds );
            handler.ReloadNewDomain( TestHelper.Monitor, idempotenceCheck: true );
            ReminderHasElapsed.Should().BeFalse();

            handler.Domain.Read( TestHelper.Monitor, () =>
            {
                counter = handler.Domain.AllObjects.OfType<StupidAutoCounter>().Single();
                clock = handler.Domain.AllInternalObjects.OfType<SuspendableClock>().Single();
                reminder = handler.Domain.TimeManager.Reminders.Single();
                counter.Count.Should().Be( 2 );
            } );

            // Reactivating the clock: the counter starts again.
            await handler.Domain.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                ClockIsActiveChanged.Should().BeFalse();
                clock.IsActive = true;
                ClockIsActiveChanged.Should().BeTrue();
                ClockIsActiveChanged = false;

                counter.IsRunning.Should().BeTrue();
                reminder.IsActive.Should().BeFalse( "Reminder has already fired." );

            } );

            Thread.Sleep( enoughMilliseconds );

            handler.Domain.Read( TestHelper.Monitor, () =>
            {
                ReminderHasElapsed.Should().BeFalse();
                counter.Count.Should().Be( 4 );
                reminder.IsActive.Should().BeFalse();
            } );

            TestHelper.Monitor.Info( "*** Reactivating the Reminder. ***" );
            await handler.Domain.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                reminder.DueTimeUtc = DateTime.UtcNow.AddMilliseconds( enoughMilliseconds / 2 );
                reminder.IsActive.Should().BeTrue();

            } );

            handler.ReloadNewDomain( TestHelper.Monitor, idempotenceCheck: false );
            Thread.Sleep( enoughMilliseconds );

            ReminderHasElapsed.Should().BeTrue();
            ClockIsActiveChanged.Should().BeFalse();

            await handler.Domain.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                handler.Domain.TimeManager.IsRunning.Should().BeTrue();
                handler.Domain.TimeManager.Stop();
                handler.Domain.TimeManager.IsRunning.Should().BeFalse();
                clock = handler.Domain.AllInternalObjects.OfType<SuspendableClock>().Single();
                clock.IsActive = false;

            } );

            handler.ReloadNewDomain( TestHelper.Monitor, idempotenceCheck: true );
        }

        static bool ClockIsActiveChanged = false;

        static void Clock_IsActiveChanged( object sender, ObservableDomainEventArgs e )
        {
            ClockIsActiveChanged = true;
        }

        static bool ReminderHasElapsed = false;

        static void Reminder_Elapsed( object sender, ObservableReminderEventArgs e )
        {
            ReminderHasElapsed = true;
        }
    }
}
