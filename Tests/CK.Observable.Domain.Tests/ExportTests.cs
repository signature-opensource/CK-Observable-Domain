using CK.Core;
using CK.Observable.Domain.Tests.Sample;
using FluentAssertions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Domain.Tests
{

    [TestFixture]
    public class ExportTests
    {
        static JsonEventCollector.TransactionEvent LastEvent = null;
        static void TrackLastEvent( IActivityMonitor m, JsonEventCollector.TransactionEvent e ) => LastEvent = e;

        [Test]
        public void doc_demo()
        {
            var eventCollector = new JsonEventCollector();
            using( var d = new ObservableDomain( TestHelper.Monitor, nameof( doc_demo ) ) )
            {
                eventCollector.CollectEvent( d, false );
                Car car = null;
                d.Modify( TestHelper.Monitor, () =>
                {
                    car = new Car( "Titine" );
                } );
                string initial = d.ExportToString();

                d.Modify( TestHelper.Monitor, () =>
                {
                    car.Dispose();
                } );

                d.Modify( TestHelper.Monitor, () =>
                {
                    var g = new Garage();
                    var m = new Mechanic( g ) { FirstName = "Paul" };
                    car = new Car( "Titine" );
                    car.CurrentMechanic = m;
                    // We rename Paul: only one PropertyChanged event
                    // is generated per property with the last set
                    // value.
                    m.FirstName = "Paulo!";
                } );

                Console.WriteLine( initial );

                string last = d.ExportToString();
                Console.WriteLine( last );
            }
        }


        [Test]
        public void exporting_and_altering_simple()
        {
            var eventCollector = new JsonEventCollector();

            using( var d = new ObservableDomain( TestHelper.Monitor, "TEST" ) )
            {
                eventCollector.CollectEvent( d, false );
                eventCollector.OnTransaction += TrackLastEvent;
                d.TransactionSerialNumber.Should().Be( 0, "Nothing happened yet." );

                string initial = d.ExportToString();

                d.Modify( TestHelper.Monitor, () =>
                {
                } ).Success.Should().BeTrue();

                d.TransactionSerialNumber.Should().Be( 1, "Even if nothing changed, TransactionNumber is incremented." );
                LastEvent.TransactionNumber.Should().Be( 1 );
                LastEvent.ExportedEvents.Should().BeEmpty();

                // Transaction number 1 is not kept: empty means "I can't give youy the diff, do a full export!".
                eventCollector.GetTransactionEvents( 0 ).Should().BeEmpty();
                eventCollector.GetTransactionEvents( 1 ).Should().BeEmpty();
                eventCollector.GetTransactionEvents( 2 ).Should().BeEmpty();

                d.Modify( TestHelper.Monitor, () =>
                {
                    new Car( "Hello!" );
                } ).Success.Should().BeTrue();

                LastEvent.TransactionNumber.Should().Be( 2 );
                string t2 = LastEvent.ExportedEvents;

                d.Modify( TestHelper.Monitor, () =>
                {
                    d.AllObjects.Single().Dispose();
                } ).Should().NotBeNull();

                string t3 = LastEvent.ExportedEvents;

                d.Modify( TestHelper.Monitor, () =>
                {
                    new MultiPropertyType();

                } ).Should().NotBeNull();

                string t4 = LastEvent.ExportedEvents;

                d.Modify( TestHelper.Monitor, () =>
                {
                    var m = d.AllObjects.OfType<MultiPropertyType>().Single();
                    m.ChangeAll( "Pouf", 3, new Guid( "{B681AD83-A276-4A5C-A11A-4A22469B6A0D}" ) );

                } ).Should().NotBeNull();

                string t5 = LastEvent.ExportedEvents;

                d.Modify( TestHelper.Monitor, () =>
                {
                    var m = d.AllObjects.OfType<MultiPropertyType>().Single();
                    m.SetDefaults();

                } ).Should().NotBeNull();

                string t6 = LastEvent.ExportedEvents;

                d.Modify( TestHelper.Monitor, () =>
                {
                    d.AllObjects.OfType<MultiPropertyType>().Single().Dispose();
                    var l = new ObservableList<string>();
                    l.Add( "One" );
                    l.Add( "Two" );

                } ).Should().NotBeNull();

                string t7 = LastEvent.ExportedEvents;

                d.Modify( TestHelper.Monitor, () =>
                {
                    var l = d.AllObjects.OfType<ObservableList<string>>().Single();
                    l[0] = "Three";
                } ).Should().NotBeNull();

                string t8 = LastEvent.ExportedEvents;

            }
        }

        [Test]
        public void exporting_and_altering_sample()
        {
            var eventCollector = new JsonEventCollector();
            eventCollector.OnTransaction += TrackLastEvent;

            using( var d = SampleDomain.CreateSample() )
            {
                eventCollector.CollectEvent( d, false );
                d.TransactionSerialNumber.Should().Be( 1 );

                string initial = d.ExportToString();

                TestHelper.Monitor.Info( initial );
                d.Modify( TestHelper.Monitor, () =>
                {
                    var g2 = d.AllObjects.OfType<Garage>().Single( g => g.CompanyName == null );
                    g2.CompanyName = "Signature Code";
                } );
                LastEvent.TransactionNumber.Should().Be( 2 );
                string t1 = LastEvent.ExportedEvents;
                t1.Should().Be( "[\"C\",16,0,\"Signature Code\"]" );

                d.Modify( TestHelper.Monitor, () =>
                {
                    var g2 = d.AllObjects.OfType<Garage>().Single( g => g.CompanyName == "Signature Code" );
                    g2.Cars.Clear();
                    var newOne = new Mechanic( g2 ) { FirstName = "X", LastName = "Y" };
                } );
                LastEvent.TransactionNumber.Should().Be( 3 );
                string t2 = LastEvent.ExportedEvents;

                d.Modify( TestHelper.Monitor, () =>
                {
                    var spi = d.AllObjects.OfType<Mechanic>().Single( m => m.LastName == "Spinelli" );
                    spi.Dispose();
                } );
                LastEvent.TransactionNumber.Should().Be( 4 );
                string t3 = LastEvent.ExportedEvents;
                t3.Should().Be( "[\"R\",17,5],[\"D\",25]" );

                d.Modify( TestHelper.Monitor, () =>
                {
                    var g1 = d.AllObjects.OfType<Garage>().Single( g => g.CompanyName == "Boite" );
                    g1.ReplacementCar.Remove( g1.Cars[0] );
                } );
                LastEvent.TransactionNumber.Should().Be( 5 );
                string t4 = LastEvent.ExportedEvents;
                t4.Should().Be( "[\"K\",3,{\"=\":4}]" );

            }
        }

        [Test]
        public void exporting_and_altering_ApplicationState()
        {
            var eventCollector = new JsonEventCollector();
            eventCollector.OnTransaction += TrackLastEvent;

            using( var d = new ObservableDomain<RootSample.ApplicationState>( TestHelper.Monitor, "TEST", new MemoryTransactionProviderClient() ) )
            {
                eventCollector.CollectEvent( d, false );
                d.Modify( TestHelper.Monitor, () =>
                {
                    var p1 = new RootSample.ProductInfo( "Name n°1", 12 );
                    p1.ExtraData.Add( "Toto", "TVal" );
                    p1.ExtraData.Add( "Tata", "TVal" );
                    d.Root.Products.Add( p1.Name, p1 );
                    d.Root.ProductStateList.Add( new RootSample.Product( p1 ) { Name = "Product n°1" } );
                    d.Root.CurrentProductState = d.Root.ProductStateList[0];
                } );
                d.Root.ProductStateList[0].OId.Index.Should().Be( 7, "Product n°1 OId.Index is 7." );
                d.TransactionSerialNumber.Should().Be( 1 );

                string initial = d.ExportToString();
                initial.Should().ContainAll( "Name n°1", "Product n°1", @"""CurrentProductState"":{"">"":7}" );
                initial.Should().Contain( @"[""Toto"",""TVal""]" );
                initial.Should().Contain( @"[""Tata"",""TVal""]" );
                d.Modify( TestHelper.Monitor, () =>
                {
                    var p2 = new RootSample.ProductInfo( "Name n°2", 22 );
                    d.Root.Products.Add( p2.Name, p2 );
                    p2.ExtraData.Add( "Ex2", ">>Ex2" );
                    d.Root.ProductStateList.Add( new RootSample.Product( p2 ) { Name = "Product n°2" } );
                    d.Root.CurrentProductState = d.Root.ProductStateList[1];
                } );
                d.Root.ProductStateList[1].OId.Index.Should().Be( 6, "Product n°2 OId.Index is 6." );

                string t1 = LastEvent.ExportedEvents;
                // p2 is the object n°5.
                t1.Should().Contain( @"[""N"",6,""""]" );
                // p2.ExtraData is exported as a Map.
                t1.Should().Contain( @"[""Ex2"","">>Ex2""]" );
                // ApplicationState.CurrentProduct is p2:
                t1.Should().Contain( @"[""C"",0,1,{""="":6}]" );

                d.Modify( TestHelper.Monitor, () =>
                {
                    d.Root.CurrentProductState.Name.Should().Be( "Product n°2" );
                    d.Root.SkipToNextProduct();
                    d.Root.CurrentProductState.Name.Should().Be( "Product n°1" );
                } );
                string t2 = LastEvent.ExportedEvents;
                // Switch to Product n°1 (OId is 7).
                t2.Should().Contain( @"[""C"",0,1,{""="":7}]" );
            }
        }
    }
}
