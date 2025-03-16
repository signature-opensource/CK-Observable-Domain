using CK.Observable.Domain.Tests.Sample;
using Shouldly;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Domain.Tests;


[TestFixture]
public partial class GarbageCollectorTests
{
    [Test]
    public async Task garbage_collect_observable_objects_Async()
    {
        using var od = new ObservableDomain<RootSample.ApplicationState>( TestHelper.Monitor, nameof( garbage_collect_observable_objects_Async ), true );

        await od.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            var p = new RootSample.Product( new RootSample.ProductInfo( "Pow", 3712 ) );
            od.Root.ProductStateList.Add( p );
            var p2 = new RootSample.Product( new RootSample.ProductInfo( "Pow2", 3712 ) );
            od.Root.ProductStateList.Add( p2 );
        } );
        od.CurrentLostObjectTracker.ShouldBeNull();
        od.EnsureLostObjectTracker( TestHelper.Monitor ).HasIssues.ShouldBeFalse();
        od.CurrentLostObjectTracker.ShouldBeSameAs( od.EnsureLostObjectTracker( TestHelper.Monitor ) );

        await od.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            od.Root.ProductStateList.RemoveAt( 0 );
        } );

        var tracker = od.EnsureLostObjectTracker( TestHelper.Monitor ).ShouldNotBeNull();
        tracker.HasIssues.ShouldBeTrue();
        od.CurrentLostObjectTracker.ShouldBeSameAs( tracker );
        var lostPow = tracker.UnreacheableObservables.ShouldHaveSingleItem().ShouldBeOfType<RootSample.Product>();
        lostPow.IsDestroyed.ShouldBeFalse();
        lostPow.ProductInfo.Name.ShouldBe( "Pow" );

        var r = await od.GarbageCollectAsync( TestHelper.Monitor );
        r.ShouldBeTrue();

        lostPow.IsDestroyed.ShouldBeTrue();
        od.EnsureLostObjectTracker( TestHelper.Monitor ).HasIssues.ShouldBeFalse();
    }

    [Test]
    public async Task garbage_collect_internal_object_Async()
    {
        using var od = new ObservableDomain<Root>( TestHelper.Monitor, nameof( garbage_collect_internal_object_Async ), true );

        await od.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            var m = new Machine();
            m.Internal = new InternalSample() { Name = "Demo" };
            od.Root.Objects.Add( m );
        } );
        od.CurrentLostObjectTracker.ShouldBeNull();
        var initialTracker = od.EnsureLostObjectTracker( TestHelper.Monitor ).ShouldNotBeNull();
        initialTracker.HasIssues.ShouldBeFalse( "No issue." );
        od.CurrentLostObjectTracker.ShouldBeSameAs( initialTracker );

        await od.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            ((Machine)od.Root.Objects[0]).Internal = null;
        } );

        var tracker = od.EnsureLostObjectTracker( TestHelper.Monitor ).ShouldNotBeNull();
        tracker.ShouldNotBeSameAs( initialTracker );
        tracker.HasIssues.ShouldBeTrue( "Now we have an issue!" );
        tracker.ShouldBeSameAs( od.CurrentLostObjectTracker );
        tracker.UnreacheableInternals.Count.ShouldBe( 1 );
        var lost = tracker.UnreacheableInternals[0]
                    .ShouldBeOfType<InternalSample>()
                    .ShouldMatch( lost => lost.Name == "Demo"
                                          && !lost.IsDestroyed );

        (await od.GarbageCollectAsync( TestHelper.Monitor )).ShouldBeTrue();

        lost.IsDestroyed.ShouldBeTrue();
        od.EnsureLostObjectTracker( TestHelper.Monitor )
            .ShouldNotBeNull().HasIssues.ShouldBeFalse( "Issue has been fixed." );
    }

    [Test]
    public async Task garbage_collect_ObservableTimedEventBase_that_have_no_callback_Async()
    {
        using var od = new ObservableDomain<Root>( TestHelper.Monitor, nameof( garbage_collect_ObservableTimedEventBase_that_have_no_callback_Async ), true );

        await od.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            for( int i = 0; i < 10; ++i )
            {
                ObservableTimedEventBase t = (i % 2) == 0
                                                ? new ObservableTimer( $"A timer n°{i}.", DateTime.UtcNow.AddDays( 2 ) )
                                                : new ObservableReminder( DateTime.UtcNow.AddDays( 2 ) );
                od.Root.TimedEvents.Add( t );
            }
        } );
        od.CurrentLostObjectTracker.ShouldBeNull();
        od.EnsureLostObjectTracker( TestHelper.Monitor ).HasIssues.ShouldBeFalse();
        od.CurrentLostObjectTracker.ShouldBeSameAs( od.EnsureLostObjectTracker( TestHelper.Monitor ) );
        od.TimeManager.AllObservableTimedEvents.Count.ShouldBe( 10 );

        await od.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            for( int i = 0; i < 5; ++i )
            {
                od.Root.TimedEvents.RemoveAt( i );
            }
        } );
        od.EnsureLostObjectTracker( TestHelper.Monitor ).HasIssues.ShouldBeTrue();
        od.TimeManager.AllObservableTimedEvents.Count.ShouldBe( 10 );
        od.CurrentLostObjectTracker.UnreacheableTimedObjects.Count.ShouldBe( 5 );

        (await od.GarbageCollectAsync( TestHelper.Monitor )).ShouldBeTrue();

        od.TimeManager.AllObservableTimedEvents.Count.ShouldBe( 5 );
        od.EnsureLostObjectTracker( TestHelper.Monitor ).HasIssues.ShouldBeFalse();
    }

    [Test]
    public async Task ObservableTimedEventBase_with_callback_are_not_lost_Async()
    {
        static void T_Elapsed( object sender, ObservableTimerEventArgs e )
        {
        }
        static void R_Elapsed( object sender, ObservableReminderEventArgs e )
        {
        }

        using var od = new ObservableDomain<Root>( TestHelper.Monitor, nameof( ObservableTimedEventBase_with_callback_are_not_lost_Async ), true );

        await od.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            for( int i = 0; i < 10; ++i )
            {
                if( (i % 2) == 0 )
                {
                    var t = new ObservableTimer( $"A timer n°{i}.", DateTime.UtcNow.AddDays( 2 ) );
                    if( (i % 3) == 0 )
                    {
                        t.Elapsed += T_Elapsed;
                    }
                }
                else
                {
                    var r = new ObservableReminder( DateTime.UtcNow.AddDays( 2 ) );
                    if( (i % 3) == 0 )
                    {
                        r.Elapsed += R_Elapsed; ;
                    }
                }
            }
        } );
        od.TimeManager.AllObservableTimedEvents.Count.ShouldBe( 10 );

        od.CurrentLostObjectTracker.ShouldBeNull();
        var c = od.EnsureLostObjectTracker( TestHelper.Monitor );
        c.HasIssues.ShouldBeTrue();
        c.UnreacheableTimedObjects.Count.ShouldBe( 6 );

        (await od.GarbageCollectAsync( TestHelper.Monitor )).ShouldBeTrue();

        od.TimeManager.AllObservableTimedEvents.Count.ShouldBe( 4 );
        od.EnsureLostObjectTracker( TestHelper.Monitor ).HasIssues.ShouldBeFalse();
    }

    [Test]
    public async Task pooled_reminders_are_not_GCed_when_half_or_less_of_them_are_inactive_Async()
    {
        static void R_Elapsed( object sender, ObservableReminderEventArgs e )
        {
        }
        using var od = new ObservableDomain<Root>( TestHelper.Monitor, nameof( pooled_reminders_are_not_GCed_when_half_or_less_of_them_are_inactive_Async ), true );

        await od.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            for( int i = 0; i < 50; ++i )
            {
                if( (i % 2) == 0 )
                {
                    od.Root.RemindFromPool( DateTime.UtcNow.AddDays( 1 ), R_Elapsed );
                }
                else
                {
                    od.Root.RemindFromPool( DateTime.UtcNow.AddMilliseconds( 60-i ), R_Elapsed );
                }
            }
        } );
        od.TimeManager.AllObservableTimedEvents.Count.ShouldBe( 50 );
        od.CurrentLostObjectTracker.ShouldBeNull();
        var c = od.EnsureLostObjectTracker( TestHelper.Monitor );
        c.HasIssues.ShouldBeFalse();
        c.UnusedPooledReminderCount.ShouldBe( 0 );

        await Task.Delay( 250 );

        c = od.EnsureLostObjectTracker( TestHelper.Monitor );
        c.UnusedPooledReminderCount.ShouldBe( 25 );

        (await od.GarbageCollectAsync( TestHelper.Monitor )).ShouldBeTrue();

        c = od.EnsureLostObjectTracker( TestHelper.Monitor );
        od.TimeManager.AllObservableTimedEvents.Count.ShouldBe( 50 );
        c.UnusedPooledReminderCount.ShouldBe( 25, "There is exactly half the number of unused." );

    }

    [Test]
    public async Task pooled_reminders_are_GCed_when_more_than_half_of_them_are_inactive_Async()
    {
        static void R_Elapsed( object sender, ObservableReminderEventArgs e )
        {
        }
        using var od = new ObservableDomain<Root>( TestHelper.Monitor, nameof( pooled_reminders_are_GCed_when_more_than_half_of_them_are_inactive_Async ), true );

        await od.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            for( int i = 0; i < 30; ++i )
            {
                if( (i % 3) == 0 )
                {
                    od.Root.RemindFromPool( DateTime.UtcNow.AddDays( 1 ), R_Elapsed );
                }
                else
                {
                    od.Root.RemindFromPool( DateTime.UtcNow.AddMilliseconds( 50-i ), R_Elapsed );
                }
            }
        } );
        od.TimeManager.AllObservableTimedEvents.Count.ShouldBe( 30 );
        od.CurrentLostObjectTracker.ShouldBeNull();
        var c = od.EnsureLostObjectTracker( TestHelper.Monitor );
        c.HasIssues.ShouldBeFalse();
        c.UnusedPooledReminderCount.ShouldBe( 0 );

        await Task.Delay( 250 );

        c = od.EnsureLostObjectTracker( TestHelper.Monitor );
        c.UnusedPooledReminderCount.ShouldBe( 20 );
        c.ShouldTrimPooledReminders.ShouldBeTrue();

        (await od.GarbageCollectAsync( TestHelper.Monitor )).ShouldBeTrue();
        od.TimeManager.AllObservableTimedEvents.Count.ShouldBe( 20 );

        c = od.EnsureLostObjectTracker( TestHelper.Monitor );
        c.UnusedPooledReminderCount.ShouldBe( 10 );
        c.ShouldTrimPooledReminders.ShouldBeFalse();

        ObservableDomain.IdempotenceSerializationCheck( TestHelper.Monitor, od );
    }

}
