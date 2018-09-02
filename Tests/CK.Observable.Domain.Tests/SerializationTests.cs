using CK.Observable.Domain.Tests.Sample;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Domain.Tests
{
    [TestFixture]
    public class SerializationTests
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
            GetOId( g2 ).Should().Be( GetOId( g1 ) );
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
            var domain = Sample.SempleDomain.CreateSample();
            var d2 = SaveAndLoad( domain );
            Sample.SempleDomain.CheckSampleGarage1( d2 );
        }

        static ObservableDomain SaveAndLoad( ObservableDomain domain )
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

        static FieldInfo _oIdField = typeof( ObservableObject ).GetField( "_id", BindingFlags.Instance | BindingFlags.NonPublic );

        static int GetOId( ObservableObject o )
        {
            return (int)_oIdField.GetValue( o );
        }

    }
}
