using CK.Core;
using CK.Observable.Domain.Tests.Sample;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Domain.Tests
{
    [TestFixture]
    public class DomainSerializationTests
    {
        [Test]
        public void simple_serialization()
        {
            using( var domain = new ObservableDomain( TestHelper.Monitor, "TEST" ) )
            {
                domain.Modify( TestHelper.Monitor, () =>
                {
                    var car = new Car( "Hello" );
                    car.TestSpeed = 10;
                } );
                using( var d2 = TestHelper.SaveAndLoad( domain ) )
                {
                    var c = d2.AllObjects.OfType<Car>().Single();
                    c.Name.Should().Be( "Hello" );
                    c.TestSpeed.Should().Be( 10 );

                    var r = d2.Modify( TestHelper.Monitor, () =>
                    {
                        c.TestSpeed = 10000;
                    } );
                    r.Events.Should().HaveCount( 1 );
                }
            }
        }

        [Test]
        public void serialization_with_mutiple_types()
        {
            using( var domain = new ObservableDomain( TestHelper.Monitor, "TEST" ) )
            {
                MultiPropertyType defValue = null;
                domain.Modify( TestHelper.Monitor, () =>
                {
                    defValue = new MultiPropertyType();
                    var other = new MultiPropertyType();
                    domain.AllObjects.First().Should().BeSameAs( defValue );
                    domain.AllObjects.ElementAt( 1 ).Should().BeSameAs( other );
                } );

                using( var d2 = TestHelper.SaveAndLoad( domain ) )
                {
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
            }
        }

        [Test]
        public void with_cycle_serialization()
        {
            using( var domain = new ObservableDomain( TestHelper.Monitor, "TEST" ) )
            {
                domain.Modify( TestHelper.Monitor, () =>
                {
                    var g = new Garage();
                    g.CompanyName = "Hello";
                    var car = new Car( "1" );
                    var m = new Mechanic( g ) { FirstName = "Hela", LastName = "Bas" };
                    m.CurrentCar = car;
                } );
                using( var d2 = TestHelper.SaveAndLoad( domain ) )
                {
                    var g1 = domain.AllObjects.OfType<Garage>().Single();
                    var g2 = d2.AllObjects.OfType<Garage>().Single();
                    g2.CompanyName.Should().Be( g1.CompanyName );
                    g2.OId.Should().Be( g1.OId );
                }
            }
        }


        [Test]
        public void with_cycle_serialization_between_2_objects()
        {
            using( var domain = new ObservableDomain( TestHelper.Monitor, "TEST" ) )
            {
                domain.Modify( TestHelper.Monitor, () =>
                {
                    var p1 = new Person() { FirstName = "A" };
                    var p2 = new Person() { FirstName = "B", Friend = p1 };
                    p1.Friend = p2;
                } );
                using( var d2 = TestHelper.SaveAndLoad( domain ) )
                {
                    var pA1 = domain.AllObjects.OfType<Person>().Single( p => p.FirstName == "A" );
                    var pB1 = domain.AllObjects.OfType<Person>().Single( p => p.FirstName == "B" );

                    pA1.Friend.Should().BeSameAs( pB1 );
                    pB1.Friend.Should().BeSameAs( pA1 );

                    var pA2 = d2.AllObjects.OfType<Person>().Single( p => p.FirstName == "A" );
                    var pB2 = d2.AllObjects.OfType<Person>().Single( p => p.FirstName == "B" );

                    pA2.Friend.Should().BeSameAs( pB2 );
                    pB2.Friend.Should().BeSameAs( pA2 );
                }
            }
        }

        [Test]
        public void ultimate_cycle_serialization()
        {
            using( var domain = new ObservableDomain( TestHelper.Monitor, "TEST" ) )
            {
                domain.Modify( TestHelper.Monitor, () =>
                {
                    var p = new Person() { FirstName = "P" };
                    p.Friend = p;
                } );
                using( var d2 = TestHelper.SaveAndLoad( domain ) )
                {
                    var p1 = domain.AllObjects.OfType<Person>().Single();
                    p1.Friend.Should().BeSameAs( p1 );

                    var p2 = d2.AllObjects.OfType<Person>().Single();
                    p2.Friend.Should().BeSameAs( p2 );
                }
            }
        }

        [Test]
        public void sample_graph_serialization_inside_read_or_write_locks()
        {
            using( var domain = Sample.SampleDomain.CreateSample() )
            {
                using( var d2 = TestHelper.SaveAndLoad( domain ) )
                {
                    Sample.SampleDomain.CheckSampleGarage1( d2 );
                }

                using( domain.AcquireReadLock() )
                {
                    using( var d = TestHelper.SaveAndLoad( domain ) )
                    {
                        Sample.SampleDomain.CheckSampleGarage1( d );
                    }
                }

                domain.Modify( TestHelper.Monitor, () =>
                {
                    using( var d = TestHelper.SaveAndLoad( domain ) )
                    {
                        Sample.SampleDomain.CheckSampleGarage1( d );
                    }
                } );
            }
        }

    }
}
