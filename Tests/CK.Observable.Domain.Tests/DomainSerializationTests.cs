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
            var domain = new ObservableDomain( TestHelper.Monitor );
            domain.Modify( () =>
            {
                var car = new Car( "Hello" );
                car.Speed = 10;
            } );
            var d2 = SaveAndLoad( domain );
            var c = d2.AllObjects.OfType<Car>().Single();
            c.Name.Should().Be( "Hello" );
            c.Speed.Should().Be( 10 );

            var r = d2.Modify( () =>
            {
                c.Speed = 10000;
            } );
            r.Events.Should().HaveCount( 1 );

        }

        [Test]
        public void serialization_with_mutiple_types()
        {
            var domain = new ObservableDomain( TestHelper.Monitor );
            MultiPropertyType defValue = null;
            domain.Modify( () =>
            {
                defValue = new MultiPropertyType();
                var other = new MultiPropertyType();
                domain.AllObjects.First().Should().BeSameAs( defValue );
                domain.AllObjects.ElementAt(1).Should().BeSameAs( other );
            } );

            var d2 = SaveAndLoad( domain );
            d2.AllObjects.OfType<MultiPropertyType>().All( o => o.Equals( defValue ) );

            d2.Modify( () =>
            {
                var other = d2.AllObjects.OfType<MultiPropertyType>().ElementAt( 1 );
                other.ChangeAll( "Changed", 3, Guid.NewGuid() );
            } );
            d2.AllObjects.First().Should().Match( o => o.Equals( defValue ) );
            d2.AllObjects.ElementAt( 1 ).Should().Match( o => !o.Equals( defValue ) );

            var d3 = SaveAndLoad( d2 );
            d3.AllObjects.First().Should().Match( o => o.Equals( defValue ) );
            d3.AllObjects.ElementAt( 1 ).Should().Match( o => !o.Equals( defValue ) );
        }

        [Test]
        public void with_cycle_serialization()
        {
            var domain = new ObservableDomain( TestHelper.Monitor );
            domain.Modify( () =>
            {
                var g = new Garage();
                g.CompanyName = "Hello";
                var car = new Car( "1" );
                var m = new Mechanic( g ) { FirstName = "Hela", LastName = "Bas" };
                m.CurrentCar = car;
            } );
            var d2 = SaveAndLoad( domain );

            var g1 = domain.AllObjects.OfType<Garage>().Single();
            var g2 = d2.AllObjects.OfType<Garage>().Single();
            g2.CompanyName.Should().Be( g1.CompanyName );
            g2.GetOId().Should().Be( g1.GetOId() );
        }


        [Test]
        public void with_cycle_serialization_between_2_objects()
        {
            var domain = new ObservableDomain( TestHelper.Monitor );
            domain.Modify( () =>
            {
                var p1 = new Person() { FirstName = "A" };
                var p2 = new Person() { FirstName = "B", Friend = p1 };
                p1.Friend = p2;
            } );
            var d2 = SaveAndLoad( domain );

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
            var domain = new ObservableDomain( TestHelper.Monitor );
            domain.Modify( () =>
            {
                var p = new Person() { FirstName = "P" };
                p.Friend = p;
            } );
            var d2 = SaveAndLoad( domain );

            var p1 = domain.AllObjects.OfType<Person>().Single();
            p1.Friend.Should().BeSameAs( p1 );

            var p2 = d2.AllObjects.OfType<Person>().Single();
            p2.Friend.Should().BeSameAs( p2 );
        }

        [Test]
        public void sample_graph_serialization()
        {
            var domain = Sample.SampleDomain.CreateSample();
            var d2 = SaveAndLoad( domain );
            Sample.SampleDomain.CheckSampleGarage1( d2 );
        }

        internal static ObservableDomain SaveAndLoad( ObservableDomain domain )
        {
            using( var s = new MemoryStream() )
            {
                domain.Save( s, leaveOpen: true );
                var d = new ObservableDomain( TestHelper.Monitor );
                s.Position = 0;
                d.Load( s, leaveOpen: true );
                return d;
            }
        }


    }
}
