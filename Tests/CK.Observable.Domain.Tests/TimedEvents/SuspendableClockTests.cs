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
        public void Ô_temps_suspend_ton_vol()
        {
            using( var d = new ObservableDomain( TestHelper.Monitor, nameof( Ô_temps_suspend_ton_vol ), startTimer: true ) )
            {
                AutoCounter counter = null;
                SuspendableClock clock = null;
                d.Modify( TestHelper.Monitor, () =>
                {
                    counter = new AutoCounter( 100 );
                    counter.IsRunning.Should().BeTrue();

                    clock = new SuspendableClock();
                    counter.SuspendableClock = clock;

                    clock.IsActive.Should().BeTrue();
                    counter.IsRunning.Should().BeTrue();

                } ).Success.Should().BeTrue();

                int intermediateCount = 0;
                Thread.Sleep( 100 * 5 );
                d.Modify( TestHelper.Monitor, () =>
                {
                    TestHelper.Monitor.Trace( $"counter.Count = {counter.Count}." );
                    (intermediateCount = counter.Count).Should().Match( c => c == 6 );
                    clock.IsActive = false;
                    counter.IsRunning.Should().BeFalse( "The bound clock is suspended." );

                } ).Success.Should().BeTrue();

                Thread.Sleep( 100 * 5 );
                using( d.AcquireReadLock() )
                {
                    TestHelper.Monitor.Trace( $"counter.Count = {counter.Count}." );
                    counter.Count.Should().Be( intermediateCount );
                }
                d.Modify( TestHelper.Monitor, () =>
                {
                    clock.IsActive = true;
                    counter.IsRunning.Should().BeTrue( "Back to business." );

                } ).Success.Should().BeTrue();

                Thread.Sleep( 100 * 5 );
                using( d.AcquireReadLock() )
                {
                    TestHelper.Monitor.Trace( $"counter.Count = {counter.Count}." );
                    counter.Count.Should().BeCloseTo( intermediateCount + 5, 1 );
                }
            }
        }

        const int enoughMilliseconds = 500;

        [TestCase( "" )]
        [TestCase( "CumulateUnloadedTime = false" )]
        public void SuspendableClock_CumulateUnloadedTime_tests( string mode )
        {
            using var handler = TestHelper.CreateDomainHandler( nameof( SuspendableClock_CumulateUnloadedTime_tests ), startTimer: true, serviceProvider: null );

            DateTime initialExpected = Util.UtcMinValue;
            handler.Domain.Modify( TestHelper.Monitor, () =>
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
            handler.Reload( TestHelper.Monitor, idempotenceCheck: false, pauseReloadMilliseconds: 2*enoughMilliseconds );
            var minUpdated = initialExpected.AddMilliseconds( 2 * enoughMilliseconds );
            // Using Modify to trigger the timed events without waiting for the AutoTimer to fire.
            handler.Domain.Modify( TestHelper.Monitor, () =>
            {
                var r = handler.Domain.TimeManager.Reminders.Single();
                if( mode == "CumulateUnloadedTime = false" )
                {
                    r.IsActive.Should().BeFalse( "Already fired." );
                    r.DueTimeUtc.Should().Be( Util.UtcMinValue );
                }
                else
                {
                    r.DueTimeUtc.Should().BeCloseTo( minUpdated, enoughMilliseconds / 6 );
                }
            } ).Success.Should().BeTrue();
        }

        [Test]
        public void SuspendableClock_serialization()
        {
            ReminderHasElapsed = false;
            ClockIsActiveChanged = false;

            using var handler = TestHelper.CreateDomainHandler( nameof( SuspendableClock_serialization ), startTimer: true, serviceProvider: null );
            AutoCounter counter = null;
            ObservableReminder reminder = null;
            SuspendableClock clock = null;
            handler.Domain.Modify( TestHelper.Monitor, () =>
            {
                counter = new AutoCounter( (7*enoughMilliseconds) / 10 );
                counter.IsRunning.Should().BeTrue();
                reminder = new ObservableReminder( DateTime.UtcNow.AddMilliseconds( enoughMilliseconds / 2 ) );
                reminder.Elapsed += Reminder_Elapsed;

                clock = new SuspendableClock();
                counter.SuspendableClock = clock;
                reminder.SuspendableClock = clock;
                clock.IsActiveChanged += Clock_IsActiveChanged;

                clock.IsActive.Should().BeTrue();
                counter.IsRunning.Should().BeTrue();

            } ).Success.Should().BeTrue();


            using( handler.Domain.AcquireReadLock() )
            {
                ClockIsActiveChanged.Should().BeFalse();
                ReminderHasElapsed.Should().BeFalse();
                counter.Count.Should().Be( 1, "AutoCounter fires immediately." );
            }

            handler.Reload( TestHelper.Monitor, idempotenceCheck: false );

            using( handler.Domain.AcquireReadLock() )
            {
                ReminderHasElapsed.Should().BeFalse();
                ClockIsActiveChanged.Should().BeFalse();

                counter = handler.Domain.AllObjects.OfType<AutoCounter>().Single();
                clock = handler.Domain.AllInternalObjects.OfType<SuspendableClock>().Single();
                reminder = handler.Domain.TimeManager.Reminders.Single();
            }
            TestHelper.Monitor.Info( "Start sleeping..." );
            Thread.Sleep( enoughMilliseconds );
            TestHelper.Monitor.Info( "End sleeping..." );
            using( handler.Domain.AcquireReadLock() )
            {
                ReminderHasElapsed.Should().BeTrue();
                ClockIsActiveChanged.Should().BeFalse();

                reminder.IsActive.Should().BeFalse( "Reminder fired. Is is now inactive." );
                counter.Count.Should().Be( 2 );
                counter.IsRunning.Should().BeTrue();
            }
            handler.Domain.Modify( TestHelper.Monitor, () =>
            {
                ClockIsActiveChanged.Should().BeFalse();
                clock.IsActive = false;
                ClockIsActiveChanged.Should().BeTrue();
                ClockIsActiveChanged = false;

                counter.IsRunning.Should().BeFalse();
                reminder.IsActive.Should().BeFalse();

            } ).Success.Should().BeTrue();

            ReminderHasElapsed = false;
            handler.Reload( TestHelper.Monitor, idempotenceCheck: true );
            Thread.Sleep( enoughMilliseconds );
            handler.Reload( TestHelper.Monitor, idempotenceCheck: true );
            ReminderHasElapsed.Should().BeFalse();

            using( handler.Domain.AcquireReadLock() )
            {
                counter = handler.Domain.AllObjects.OfType<AutoCounter>().Single();
                clock = handler.Domain.AllInternalObjects.OfType<SuspendableClock>().Single();
                reminder = handler.Domain.TimeManager.Reminders.Single();
                counter.Count.Should().Be( 2 );
            }

            // Reactivating the clock: the counter starts again.
            handler.Domain.Modify( TestHelper.Monitor, () =>
            {
                ClockIsActiveChanged.Should().BeFalse();
                clock.IsActive = true;
                ClockIsActiveChanged.Should().BeTrue();
                ClockIsActiveChanged = false;

                counter.IsRunning.Should().BeTrue();
                reminder.IsActive.Should().BeFalse( "Reminder has already fired." );

            } ).Success.Should().BeTrue();

            Thread.Sleep( enoughMilliseconds );

            using( handler.Domain.AcquireReadLock() )
            {
                ReminderHasElapsed.Should().BeFalse();
                counter.Count.Should().Be( 3 );
                reminder.IsActive.Should().BeFalse();
            }

            TestHelper.Monitor.Info( "*** Reactivating the Reminder. ***" );
            handler.Domain.Modify( TestHelper.Monitor, () =>
            {
                reminder.DueTimeUtc = DateTime.UtcNow.AddMilliseconds( enoughMilliseconds / 2 );
                reminder.IsActive.Should().BeTrue();

            } ).Success.Should().BeTrue();

            handler.Reload( TestHelper.Monitor, idempotenceCheck: false );
            Thread.Sleep( enoughMilliseconds );

            ReminderHasElapsed.Should().BeTrue();
            ClockIsActiveChanged.Should().BeFalse();
        }

        static bool ReminderHasElapsed = false;
        static bool ClockIsActiveChanged = false;

        static void Clock_IsActiveChanged( object sender, ObservableDomainEventArgs e )
        {
            ClockIsActiveChanged = true;
        }

        static void Reminder_Elapsed( object sender, ObservableReminderEventArgs e )
        {
            ReminderHasElapsed = true;
        }
    }
}
