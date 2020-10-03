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
        [Test]
        public void doc_demo()
        {
            var eventCollector = new JsonEventCollector();
            using( var d = new ObservableDomain( TestHelper.Monitor, nameof( doc_demo ) ) )
            {
                d.OnSuccessfulTransaction += eventCollector.OnSuccessfulTransaction;
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
                string firstEvents = eventCollector.WriteJSONEventsFrom( 1 );

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
                string secondEvents = eventCollector.WriteJSONEventsFrom( 2 );

                Console.WriteLine( initial );
                Console.WriteLine( firstEvents );
                Console.WriteLine( secondEvents );

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
                d.OnSuccessfulTransaction += eventCollector.OnSuccessfulTransaction;
                d.TransactionSerialNumber.Should().Be( 0, "Nothing happened yet." );

                string initial = d.ExportToString();

                d.Modify( TestHelper.Monitor, () =>
                {
                } ).Should().NotBeNull( "A null list of events is because an error occurred." );

                d.TransactionSerialNumber.Should().Be( 1, "Even if nothing changed, TransactionNumber is incremented." );
                eventCollector.WriteJSONEventsFrom( 0 ).Should().Be( @"{""N"":1,""E"":[]}", "No event occured." );

                d.Modify( TestHelper.Monitor, () =>
                {
                    new Car( "Hello!" );
                } ).Should().NotBeNull( "A null list of events is because an error occurred." );

                string t2 = eventCollector.WriteJSONEventsFrom( 0 );

                d.Modify( TestHelper.Monitor, () =>
                {
                    d.AllObjects.Single().Dispose();
                } ).Should().NotBeNull();

                string t3 = eventCollector.WriteJSONEventsFrom( 2 );

                d.Modify( TestHelper.Monitor, () =>
                {
                    new MultiPropertyType();

                } ).Should().NotBeNull();

                string t4 = eventCollector.WriteJSONEventsFrom( 3 );

                d.Modify( TestHelper.Monitor, () =>
                {
                    var m = d.AllObjects.OfType<MultiPropertyType>().Single();
                    m.ChangeAll( "Pouf", 3, new Guid( "{B681AD83-A276-4A5C-A11A-4A22469B6A0D}" ) );

                } ).Should().NotBeNull();

                string t5 = eventCollector.WriteJSONEventsFrom( 4 );

                d.Modify( TestHelper.Monitor, () =>
                {
                    var m = d.AllObjects.OfType<MultiPropertyType>().Single();
                    m.SetDefaults();

                } ).Should().NotBeNull();

                string t6 = eventCollector.WriteJSONEventsFrom( 5 );

                d.Modify( TestHelper.Monitor, () =>
                {
                    d.AllObjects.OfType<MultiPropertyType>().Single().Dispose();
                    var l = new ObservableList<string>();
                    l.Add( "One" );
                    l.Add( "Two" );

                } ).Should().NotBeNull();

                string t7 = eventCollector.WriteJSONEventsFrom( 6 );

                d.Modify( TestHelper.Monitor, () =>
                {
                    var l = d.AllObjects.OfType<ObservableList<string>>().Single();
                    l[0] = "Three";
                } ).Should().NotBeNull();

                string t8 = eventCollector.WriteJSONEventsFrom( 7 );

            }
        }

        [Test]
        public void exporting_and_altering_sample()
        {
            var eventCollector = new JsonEventCollector();

            using( var d = SampleDomain.CreateSample() )
            {
                d.OnSuccessfulTransaction += eventCollector.OnSuccessfulTransaction;
                d.TransactionSerialNumber.Should().Be( 1 );

                string initial = d.ExportToString();

                TestHelper.Monitor.Info( initial );
                d.Modify( TestHelper.Monitor, () =>
                {
                    var g2 = d.AllObjects.OfType<Garage>().Single( g => g.CompanyName == null );
                    g2.CompanyName = "Signature Code";
                } );
                string t1 = eventCollector.WriteJSONEventsFrom( 1 );

                d.Modify( TestHelper.Monitor, () =>
                {
                    var g2 = d.AllObjects.OfType<Garage>().Single( g => g.CompanyName == "Signature Code" );
                    g2.Cars.Clear();
                    var newOne = new Mechanic( g2 ) { FirstName = "X", LastName = "Y" };
                } );
                string t2 = eventCollector.WriteJSONEventsFrom( 2 );

                d.Modify( TestHelper.Monitor, () =>
                {
                    var spi = d.AllObjects.OfType<Mechanic>().Single( m => m.LastName == "Spinelli" );
                    spi.Dispose();
                } );
                string t3 = eventCollector.WriteJSONEventsFrom( 3 );

                d.Modify( TestHelper.Monitor, () =>
                {
                    var g1 = d.AllObjects.OfType<Garage>().Single( g => g.CompanyName == "Boite" );
                    g1.ReplacementCar.Remove( g1.Cars[0] );
                } );
                string t4 = eventCollector.WriteJSONEventsFrom( 4 );

                string t2to4 = eventCollector.WriteJSONEventsFrom( 2 );
                var combined = JObject.Parse( t2to4 )["E"].AsEnumerable();
                var oneByOne = new JArray( JObject.Parse( t2 )["E"].AsEnumerable()
                                            .Concat( JObject.Parse( t3 )["E"].AsEnumerable() )
                                            .Concat( JObject.Parse( t4 )["E"].AsEnumerable() ) );
                combined.ToString().Should().Be( oneByOne.ToString() );
            }
        }

        [Test]
        public void exporting_and_altering_ApplicationState()
        {
            var eventCollector = new JsonEventCollector();

            using( var d = new ObservableDomain<RootSample.ApplicationState>( TestHelper.Monitor, "TEST", new MemoryTransactionProviderClient() ) )
            {
                d.OnSuccessfulTransaction += eventCollector.OnSuccessfulTransaction;
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

                string t1 = eventCollector.WriteJSONEventsFrom( 1 );
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
                string t2 = eventCollector.WriteJSONEventsFrom( 2 );
                // Switch to Product n°1 (OId is 7).
                t2.Should().Contain( @"[""C"",0,1,{""="":7}]" );
            }
        }
    }
}
