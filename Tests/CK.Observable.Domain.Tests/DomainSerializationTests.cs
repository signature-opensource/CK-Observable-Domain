using CK.Core;
using CK.Observable.Domain.Tests.Sample;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Domain.Tests
{
    [TestFixture]
    public class DomainSerializationTests
    {
        [Test]
        public void simple_serialization()
        {
            using var domain = new ObservableDomain( TestHelper.Monitor, "TEST", startTimer: false );
            domain.Modify( TestHelper.Monitor, () =>
            {
                var car = new Car( "Hello" );
                car.TestSpeed = 10;
            } ).Success.Should().BeTrue();

            using var d2 = TestHelper.SaveAndLoad( domain );
            domain.IsDisposed.Should().BeTrue( "SaveAndLoad disposed it." );

            IReadOnlyList<ObservableEvent>? events = null;
            d2.OnSuccessfulTransaction += ( d, ev ) => events = ev.Events;

            var c = d2.AllObjects.OfType<Car>().Single();
            c.Name.Should().Be( "Hello" );
            c.TestSpeed.Should().Be( 10 );

            d2.Modify( TestHelper.Monitor, () =>
            {
                c.TestSpeed = 10000;
            } ).Success.Should().BeTrue();
            events.Should().HaveCount( 1 );
        }

        [Test]
        public void serialization_with_mutiple_types()
        {
            using var domain = new ObservableDomain( TestHelper.Monitor, nameof( serialization_with_mutiple_types ), startTimer: true );
            MultiPropertyType defValue = null;
            domain.Modify( TestHelper.Monitor, () =>
            {
                defValue = new MultiPropertyType();
                var other = new MultiPropertyType();
                domain.AllObjects.First().Should().BeSameAs( defValue );
                domain.AllObjects.ElementAt( 1 ).Should().BeSameAs( other );
            } );

            using var d2 = TestHelper.SaveAndLoad( domain );
            d2.AllObjects.OfType<MultiPropertyType>().All( o => o.Equals( defValue ) );

            d2.Modify( TestHelper.Monitor, () =>
            {
                var other = d2.AllObjects.OfType<MultiPropertyType>().ElementAt( 1 );
                other.ChangeAll( "Changed", 3, Guid.NewGuid() );
            } );
            d2.AllObjects.First().Should().Match( o => o.Equals( defValue ) );
            d2.AllObjects.ElementAt( 1 ).Should().Match( o => !o.Equals( defValue ) );

            using( var d3 = TestHelper.SaveAndLoad( d2 ) )
            {
                d3.AllObjects.First().Should().Match( o => o.Equals( defValue ) );
                d3.AllObjects.ElementAt( 1 ).Should().Match( o => !o.Equals( defValue ) );
            }
        }

        [Test]
        public void with_cycle_serialization()
        {
            using var domain = new ObservableDomain( TestHelper.Monitor, nameof( with_cycle_serialization ), startTimer: true );
            domain.Modify( TestHelper.Monitor, () =>
            {
                var g = new Garage();
                g.CompanyName = "Hello";
                var car = new Car( "1" );
                var m = new Mechanic( g ) { FirstName = "Hela", LastName = "Bas" };
                m.CurrentCar = car;
            } );
            using var d2 = TestHelper.SaveAndLoad( domain );
            var g1 = domain.AllObjects.OfType<Garage>().Single();
            var g2 = d2.AllObjects.OfType<Garage>().Single();
            g2.CompanyName.Should().Be( g1.CompanyName );
            g2.OId.Should().Be( g1.OId );
        }


        [Test]
        public void with_cycle_serialization_between_2_objects()
        {
            using var domain = new ObservableDomain( TestHelper.Monitor, nameof( with_cycle_serialization_between_2_objects ), startTimer: true );
            domain.Modify( TestHelper.Monitor, () =>
            {
                var p1 = new Person() { FirstName = "A" };
                var p2 = new Person() { FirstName = "B", Friend = p1 };
                p1.Friend = p2;
            } );
            using var d2 = TestHelper.SaveAndLoad( domain );
            var pA1 = domain.AllObjects.OfType<Person>().Single( p => p.FirstName == "A" );
            var pB1 = domain.AllObjects.OfType<Person>().Single( p => p.FirstName == "B" );

            pA1.Friend.Should().BeSameAs( pB1 );
            pB1.Friend.Should().BeSameAs( pA1 );

            var pA2 = d2.AllObjects.OfType<Person>().Single( p => p.FirstName == "A" );
            var pB2 = d2.AllObjects.OfType<Person>().Single( p => p.FirstName == "B" );

            pA2.Friend.Should().BeSameAs( pB2 );
            pB2.Friend.Should().BeSameAs( pA2 );
        }

        [Test]
        public void ultimate_cycle_serialization()
        {
            using var domain = new ObservableDomain( TestHelper.Monitor, nameof( ultimate_cycle_serialization ), startTimer: true );
            domain.Modify( TestHelper.Monitor, () =>
            {
                var p = new Person() { FirstName = "P" };
                p.Friend = p;
            } );
            using var d2 = TestHelper.SaveAndLoad( domain );
            var p1 = domain.AllObjects.OfType<Person>().Single();
            p1.Friend.Should().BeSameAs( p1 );

            var p2 = d2.AllObjects.OfType<Person>().Single();
            p2.Friend.Should().BeSameAs( p2 );
        }

        [Test]
        public void sample_graph_serialization_inside_read_or_write_locks()
        {
            using( var domain = Sample.SampleDomain.CreateSample() )
            {
                using( var d2 = TestHelper.SaveAndLoad( domain, skipDomainDispose: true ) )
                {
                    Sample.SampleDomain.CheckSampleGarage1( d2 );
                }

                using( domain.AcquireReadLock() )
                {
                    using( var d = TestHelper.SaveAndLoad( domain, skipDomainDispose: true ) )
                    {
                        Sample.SampleDomain.CheckSampleGarage1( d );
                    }
                }

                domain.Modify( TestHelper.Monitor, () =>
                {
                    using( var d = TestHelper.SaveAndLoad( domain, skipDomainDispose: true ) )
                    {
                        Sample.SampleDomain.CheckSampleGarage1( d );
                    }
                } );
            }
        }


        [SerializationVersion( 0 )]
        public class LoadHookTester : ObservableObject
        {
            public LoadHookTester()
            {

            }

            LoadHookTester( IBinaryDeserializer r, TypeReadInfo? info )
                : base( RevertSerialization.Default )
            {
                Count = r.ReadInt32();
            }

            void Write( BinarySerializer w )
            {
                w.Write( Count );
            }

            public int Count { get; private set; }

            public ObservableTimer Timer { get; }
 
        }

        [Test]
        public void persisting_disposed_objects_and_SaveDisposedObjectBehavior()
        {
            // Will be disposed by SaveAndLoad.
            var d = new ObservableDomain( TestHelper.Monitor, nameof( loadHooks_can_skip_the_TimedEvents_update ), startTimer: true );
            d.Modify( TestHelper.Monitor, () =>
            {
                var list = new ObservableList<object>();
                var timer = new ObservableTimer( DateTime.UtcNow.AddDays( 1 ) );
                var reminder = new ObservableReminder( DateTime.UtcNow.AddDays( 1 ) );
                var obsOject = new Person();
                // CumulateUnloadedTime changes the CumulativeOffset at reload: serialization cannot be idempotent.
                var intObject = new SuspendableClock() { CumulateUnloadedTime = false };
                list.Add( timer );
                list.Add( reminder );
                list.Add( obsOject );
                list.Add( intObject );
            } );

            // This reloads the domain instance.
            ObservableDomain.IdempotenceSerializationCheck( TestHelper.Monitor, d );

            // This disposes the domain and returns a brand new one. This doesn't throw.
            using var d2 = TestHelper.SaveAndLoad( d, saveDisposed: SaveDisposedObjectBehavior.Throw );

            d2.Modify( TestHelper.Monitor, () =>
            {
                var list = d2.AllObjects.OfType<ObservableList<object>>().Single();

                int i = 0;
                var timer = (ObservableTimer)list[i++]; timer.Destroy();
                var reminder = (ObservableReminder)list[i++]; reminder.Destroy();
                var obsOject = (Person)list[i++]; obsOject.Destroy();
                var intObject = (SuspendableClock)list[i++]; intObject.Destroy();
            } );
            ObservableDomain.IdempotenceSerializationCheck( TestHelper.Monitor, d2 );

            d2.Modify( TestHelper.Monitor, () =>
            {
                var list = d2.AllObjects.OfType<ObservableList<object>>().Single();
                list.Count.Should().Be( 4 );
                foreach( IDestroyableObject o in list )
                {
                    o.IsDestroyed.Should().BeTrue();
                }
            } );

            TestHelper.Invoking( x => x.SaveAndLoad( d2, saveDisposed: SaveDisposedObjectBehavior.Throw ) )
                .Should().Throw<CKException>()
                .WithMessage( "Found 4 disposed objects: 1 instances of 'ObservableTimer', 1 instances of 'ObservableReminder', 1 instances of 'Person', 1 instances of 'SuspendableClock'." );
        }


        [Test]
        public void loadHooks_can_skip_the_TimedEvents_update()
        {
            ElapsedFired = false;

            using var d = new ObservableDomain( TestHelper.Monitor, nameof( loadHooks_can_skip_the_TimedEvents_update ), startTimer: true );
            d.Modify( TestHelper.Monitor, () =>
            {
                var r = new ObservableReminder( DateTime.UtcNow.AddMilliseconds( 100 ) );
                r.Elapsed += OnElapsedFire;
                d.TimeManager.Reminders.Single().Should().BeSameAs( r );
                r.IsActive.Should().BeTrue();
            } );
            using var d2 = TestHelper.SaveAndLoad( d, startTimer: false, pauseMilliseconds: 150 );
            d.IsDisposed.Should().BeTrue();

            using( d2.AcquireReadLock() )
            {
                d2.TimeManager.Reminders.Single().IsActive.Should().BeTrue( "Not triggered by Load." );
            }
            Thread.Sleep( 100 );
            using( d2.AcquireReadLock() )
            {
                d2.TimeManager.Reminders.Single().IsActive.Should().BeTrue( "Will be triggered at the start of the next Modify." );
            }
            d2.Modify( TestHelper.Monitor, () =>
            {
                d2.TimeManager.Reminders.Single().IsActive.Should().BeFalse();
                ElapsedFired.Should().BeTrue();
            } );
        }

        static bool ElapsedFired = false;
        static void OnElapsedFire( object sender, ObservableReminderEventArgs e )
        {
            ElapsedFired = true;
        }
    }
}
