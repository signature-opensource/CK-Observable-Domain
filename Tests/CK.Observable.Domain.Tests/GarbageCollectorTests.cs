using CK.Core;
using CK.Observable.Domain.Tests.Sample;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Domain.Tests
{

    [TestFixture]
    public class GarbageCollectorTests
    {
        [Test]
        public async Task garbage_collect_observable_objects()
        {
            using var od = new ObservableDomain<RootSample.ApplicationState>( TestHelper.Monitor, nameof( garbage_collect_observable_objects ), true );

            od.Modify( TestHelper.Monitor, () =>
            {
                var p = new RootSample.Product( new RootSample.ProductInfo( "Pow", 3712 ) );
                od.Root.ProductStateList.Add( p );
                var p2 = new RootSample.Product( new RootSample.ProductInfo( "Pow2", 3712 ) );
                od.Root.ProductStateList.Add( p2 );
            } );
            od.CurrentLostObjectTracker.Should().BeNull();
            od.EnsureLostObjectTracker( TestHelper.Monitor ).HasIssues.Should().BeFalse();
            od.CurrentLostObjectTracker.Should().BeSameAs( od.EnsureLostObjectTracker( TestHelper.Monitor ) );

            od.Modify( TestHelper.Monitor, () =>
            {
                od.Root.ProductStateList.RemoveAt( 0 );
            } );

            od.EnsureLostObjectTracker( TestHelper.Monitor ).HasIssues.Should().BeTrue();
            od.CurrentLostObjectTracker.UnreacheableObservables.Should().HaveCount( 1 );
            var lostPow = od.CurrentLostObjectTracker.UnreacheableObservables[0].As<RootSample.Product>();
            lostPow.IsDestroyed.Should().BeFalse();
            lostPow.ProductInfo.Name.Should().Be( "Pow" );

            var r = await od.GarbageCollectAsync( TestHelper.Monitor );
            r.Should().BeTrue();

            lostPow.IsDestroyed.Should().BeTrue();
            od.EnsureLostObjectTracker( TestHelper.Monitor ).HasIssues.Should().BeFalse();
        }

        [SerializationVersion( 0 )]
        class Machine : ObservableObject
        {
            public Machine()
            {
            }

            public SuspendableClock? Clock { get; set; }

            public InternalObject? Internal { get; set; }

            public ObservableObject? Observable { get; set; }

            Machine( IBinaryDeserializer r, TypeReadInfo info )
            {
                Clock = (SuspendableClock?)r.ReadObject();
                Internal = (InternalObject?)r.ReadObject();
                Observable = (ObservableObject?)r.ReadObject();
            }

            void Write( BinarySerializer w )
            {
                w.WriteObject( Clock );
                w.WriteObject( Internal );
                w.WriteObject( Observable );
            }
        }

        [SerializationVersion( 0 )]
        class InternalSample : InternalObject
        {
            public InternalSample()
            {
            }

            public string? Name { get; set; }

            InternalSample( IBinaryDeserializer r, TypeReadInfo info )
            {
                Name = r.ReadNullableString();
            }

            void Write( BinarySerializer w )
            {
                w.WriteNullableString( Name );
            }
        }

        [SerializationVersion( 0 )]
        class ObservableSample : ObservableObject
        {
            public ObservableSample()
            {
            }

            public string? Name { get; set; }

            ObservableSample( IBinaryDeserializer r, TypeReadInfo info )
            {
                Name = r.ReadNullableString();
            }

            void Write( BinarySerializer w )
            {
                w.WriteNullableString( Name );
            }
        }

        [SerializationVersion( 0 )]
        class Root : ObservableRootObject
        {
            public ObservableList<ObservableObject> Objects { get; }

            public ObservableList<ObservableTimedEventBase> TimedEvents { get; }

            public Root()
            {
                Objects = new ObservableList<ObservableObject>();
                TimedEvents = new ObservableList<ObservableTimedEventBase>();
            }

            Root( IBinaryDeserializer r, TypeReadInfo info )
                : base( RevertSerialization.Default )
            {
                Objects = (ObservableList<ObservableObject>)r.ReadObject()!;
                TimedEvents = (ObservableList<ObservableTimedEventBase>)r.ReadObject()!;
            }

            void Write( BinarySerializer w )
            {
                w.WriteObject( Objects );
                w.WriteObject( TimedEvents );
            }

            public void RemindFromPool( DateTime dueTime, SafeEventHandler<ObservableReminderEventArgs> callback )
            {
                Domain.Remind( dueTime, callback );
            }

        }

        [Test]
        public async Task garbage_collect_internal_object()
        {
            using var od = new ObservableDomain<Root>( TestHelper.Monitor, nameof( garbage_collect_internal_object ), true );

            od.Modify( TestHelper.Monitor, () =>
            {
                var m = new Machine();
                m.Internal = new InternalSample() { Name = "Demo" };
                od.Root.Objects.Add( m );
            } );
            od.CurrentLostObjectTracker.Should().BeNull();
            od.EnsureLostObjectTracker( TestHelper.Monitor ).HasIssues.Should().BeFalse();
            od.CurrentLostObjectTracker.Should().BeSameAs( od.EnsureLostObjectTracker( TestHelper.Monitor ) );

            od.Modify( TestHelper.Monitor, () =>
            {
                od.Root.Objects[0].As<Machine>().Internal = null;
            } );

            od.EnsureLostObjectTracker( TestHelper.Monitor ).HasIssues.Should().BeTrue();
            od.CurrentLostObjectTracker.UnreacheableInternals.Should().HaveCount( 1 );
            var lost = od.CurrentLostObjectTracker.UnreacheableInternals[0].As<InternalSample>();
            lost.IsDestroyed.Should().BeFalse();
            lost.Name.Should().Be( "Demo" );

            (await od.GarbageCollectAsync( TestHelper.Monitor )).Should().BeTrue();

            lost.IsDestroyed.Should().BeTrue();
            od.EnsureLostObjectTracker( TestHelper.Monitor ).HasIssues.Should().BeFalse();
        }

        [Test]
        public async Task garbage_collect_ObservableTimedEventBase_that_have_no_callback()
        {
            using var od = new ObservableDomain<Root>( TestHelper.Monitor, nameof( garbage_collect_ObservableTimedEventBase_that_have_no_callback ), true );

            od.Modify( TestHelper.Monitor, () =>
            {
                for( int i = 0; i < 10; ++i )
                {
                    ObservableTimedEventBase t = (i % 2) == 0
                                                    ? new ObservableTimer( $"A timer n°{i}.", DateTime.UtcNow.AddDays( 2 ) )
                                                    : new ObservableReminder( DateTime.UtcNow.AddDays( 2 ) );
                    od.Root.TimedEvents.Add( t );
                }
            } );
            od.CurrentLostObjectTracker.Should().BeNull();
            od.EnsureLostObjectTracker( TestHelper.Monitor ).HasIssues.Should().BeFalse();
            od.CurrentLostObjectTracker.Should().BeSameAs( od.EnsureLostObjectTracker( TestHelper.Monitor ) );
            od.TimeManager.AllObservableTimedEvents.Should().HaveCount( 10 );

            od.Modify( TestHelper.Monitor, () =>
            {
                for( int i = 0; i < 5; ++i )
                {
                    od.Root.TimedEvents.RemoveAt( i );
                }
            } );
            od.EnsureLostObjectTracker( TestHelper.Monitor ).HasIssues.Should().BeTrue();
            od.TimeManager.AllObservableTimedEvents.Should().HaveCount( 10 );
            od.CurrentLostObjectTracker.UnreacheableTimedObjects.Should().HaveCount( 5 );

            (await od.GarbageCollectAsync( TestHelper.Monitor )).Should().BeTrue();

            od.TimeManager.AllObservableTimedEvents.Should().HaveCount( 5 );
            od.EnsureLostObjectTracker( TestHelper.Monitor ).HasIssues.Should().BeFalse();
        }

        [Test]
        public async Task ObservableTimedEventBase_with_callback_are_not_lost()
        {
            static void T_Elapsed( object sender, ObservableTimerEventArgs e )
            {
            }
            static void R_Elapsed( object sender, ObservableReminderEventArgs e )
            {
            }

            using var od = new ObservableDomain<Root>( TestHelper.Monitor, nameof( ObservableTimedEventBase_with_callback_are_not_lost ), true );

            od.Modify( TestHelper.Monitor, () =>
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
            od.TimeManager.AllObservableTimedEvents.Should().HaveCount( 10 );

            od.CurrentLostObjectTracker.Should().BeNull();
            var c = od.EnsureLostObjectTracker( TestHelper.Monitor );
            c.HasIssues.Should().BeTrue();
            c.UnreacheableTimedObjects.Should().HaveCount( 6 );

            (await od.GarbageCollectAsync( TestHelper.Monitor )).Should().BeTrue();

            od.TimeManager.AllObservableTimedEvents.Should().HaveCount( 4 );
            od.EnsureLostObjectTracker( TestHelper.Monitor ).HasIssues.Should().BeFalse();
        }

        [Test]
        public async Task pooled_reminders_are_not_GCed_when_half_or_less_of_them_are_inactive()
        {
            static void R_Elapsed( object sender, ObservableReminderEventArgs e )
            {
            }
            using var od = new ObservableDomain<Root>( TestHelper.Monitor, nameof( pooled_reminders_are_not_GCed_when_half_or_less_of_them_are_inactive ), true );

            od.Modify( TestHelper.Monitor, () =>
            {
                for( int i = 0; i < 50; ++i )
                {
                    if( (i % 2) == 0 )
                    {
                        od.Root.RemindFromPool( DateTime.UtcNow.AddDays( 1 ), R_Elapsed );
                    }
                    else
                    {
                        od.Root.RemindFromPool( DateTime.UtcNow.AddMilliseconds( 200 ), R_Elapsed );
                    }
                }
            } );
            od.TimeManager.AllObservableTimedEvents.Should().HaveCount( 50 );
            od.CurrentLostObjectTracker.Should().BeNull();
            var c = od.EnsureLostObjectTracker( TestHelper.Monitor );
            c.HasIssues.Should().BeFalse();
            c.UnusedPooledReminderCount.Should().Be( 0 );

            await Task.Delay( 250 );

            c = od.EnsureLostObjectTracker( TestHelper.Monitor );
            c.UnusedPooledReminderCount.Should().Be( 25 );

            (await od.GarbageCollectAsync( TestHelper.Monitor )).Should().BeTrue();

            c = od.EnsureLostObjectTracker( TestHelper.Monitor );
            od.TimeManager.AllObservableTimedEvents.Should().HaveCount( 50 );
            c.UnusedPooledReminderCount.Should().Be( 25, "There is exactly half the number of unused." );

        }

        [Test]
        public async Task pooled_reminders_are_GCed_when_more_than_half_of_them_are_inactive()
        {
            static void R_Elapsed( object sender, ObservableReminderEventArgs e )
            {
            }
            using var od = new ObservableDomain<Root>( TestHelper.Monitor, nameof( pooled_reminders_are_GCed_when_more_than_half_of_them_are_inactive ), true );

            od.Modify( TestHelper.Monitor, () =>
            {
                for( int i = 0; i < 30; ++i )
                {
                    if( (i % 3) == 0 )
                    {
                        od.Root.RemindFromPool( DateTime.UtcNow.AddDays( 1 ), R_Elapsed );
                    }
                    else
                    {
                        od.Root.RemindFromPool( DateTime.UtcNow.AddMilliseconds( 200 ), R_Elapsed );
                    }
                }
            } );
            od.TimeManager.AllObservableTimedEvents.Should().HaveCount( 30 );
            od.CurrentLostObjectTracker.Should().BeNull();
            var c = od.EnsureLostObjectTracker( TestHelper.Monitor );
            c.HasIssues.Should().BeFalse();
            c.UnusedPooledReminderCount.Should().Be( 0 );

            await Task.Delay( 250 );

            c = od.EnsureLostObjectTracker( TestHelper.Monitor );
            c.UnusedPooledReminderCount.Should().Be( 20 );
            c.ShouldTrimPooledReminders.Should().BeTrue();

            (await od.GarbageCollectAsync( TestHelper.Monitor )).Should().BeTrue();
            od.TimeManager.AllObservableTimedEvents.Should().HaveCount( 20 );

            c = od.EnsureLostObjectTracker( TestHelper.Monitor );
            c.UnusedPooledReminderCount.Should().Be( 10 );
            c.ShouldTrimPooledReminders.Should().BeFalse();

            ObservableDomain.IdempotenceSerializationCheck( TestHelper.Monitor, od );
        }


    }
}
