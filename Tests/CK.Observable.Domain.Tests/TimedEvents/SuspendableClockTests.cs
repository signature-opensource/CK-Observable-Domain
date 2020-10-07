using CK.Core;
using CK.Observable.Domain.Tests.Sample;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
            using( var d = new ObservableDomain( TestHelper.Monitor, nameof( Ô_temps_suspend_ton_vol ) ) )
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
                    (intermediateCount = counter.Count).Should().Match( c => c == 5 );
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

        [Test]
        public void SuspendableClock_serialization()
        {
            int enoughMilliseconds = 500;
            ReminderHasElapsed = false;

            using var handler = TestHelper.CreateDomainHandler( nameof( SuspendableClock_serialization ), serviceProvider: null );
            AutoCounter counter = null;
            ObservableReminder reminder = null;
            SuspendableClock clock = null;
            handler.Domain.Modify( TestHelper.Monitor, () =>
            {
                counter = new AutoCounter( (4 * enoughMilliseconds) / 5 );
                counter.IsRunning.Should().BeTrue();
                reminder = new ObservableReminder( DateTime.UtcNow.AddMilliseconds( enoughMilliseconds / 2 ) );
                reminder.Elapsed += Reminder_Elapsed;

                clock = new SuspendableClock();
                counter.SuspendableClock = clock;
                reminder.SuspendableClock = clock;

                clock.IsActive.Should().BeTrue();
                counter.IsRunning.Should().BeTrue();

            } ).Success.Should().BeTrue();

            using( handler.Domain.AcquireReadLock() )
            {
                ReminderHasElapsed.Should().BeFalse();
                counter.Count.Should().Be( 0 );
            }

            handler.Reload( TestHelper.Monitor, idempotenceCheck: false );
            ReminderHasElapsed.Should().BeFalse();

            using( handler.Domain.AcquireReadLock() )
            {
                counter = handler.Domain.AllObjects.OfType<AutoCounter>().Single();
                clock = handler.Domain.AllInternalObjects.OfType<SuspendableClock>().Single();
                reminder = handler.Domain.TimeManager.Reminders.Single();
            }
            Thread.Sleep( enoughMilliseconds );
            using( handler.Domain.AcquireReadLock() )
            {
                ReminderHasElapsed.Should().BeTrue();
                counter.Count.Should().Be( 1 );
                counter.IsRunning.Should().BeTrue();
                reminder.IsActive.Should().BeFalse( "Reminder fired. Is is now inactive." );
            }
            handler.Domain.Modify( TestHelper.Monitor, () =>
            {
                clock.IsActive = false;
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
                counter.Count.Should().Be( 1 );
            }

            // Reactivating the clock: the counter starts again.
            handler.Domain.Modify( TestHelper.Monitor, () =>
            {
                clock.IsActive = true;
                counter.IsRunning.Should().BeTrue();
                reminder.IsActive.Should().BeFalse( "Reminder has already fired." );

            } ).Success.Should().BeTrue();

            Thread.Sleep( enoughMilliseconds );

            using( handler.Domain.AcquireReadLock() )
            {
                ReminderHasElapsed.Should().BeFalse();
                counter.Count.Should().Be( 2 );
                reminder.IsActive.Should().BeFalse();
            }

            // Reactivating the Reminder.
            handler.Domain.Modify( TestHelper.Monitor, () =>
            {
                reminder.DueTimeUtc = DateTime.UtcNow.AddMilliseconds( enoughMilliseconds / 2 );
                reminder.IsActive.Should().BeTrue();

            } ).Success.Should().BeTrue();

            handler.Reload( TestHelper.Monitor, idempotenceCheck: true );
            Thread.Sleep( enoughMilliseconds );

            ReminderHasElapsed.Should().BeTrue();
        }

        static bool ReminderHasElapsed = false;

        static void Reminder_Elapsed( object sender, ObservableReminderEventArgs e )
        {
            ReminderHasElapsed = true;
        }
    }
}
