using CK.Core;
using CK.Observable.Domain.Tests.Sample;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Domain.Tests
{
    [TestFixture]
    public class ExportTests
    {
        [Test]
        public void exporting_and_altering_simple()
        {
            var eventCollector = new TransactionEventCollector();

            var d = new ObservableDomain( eventCollector, TestHelper.Monitor );
            d.TransactionSerialNumber.Should().Be( 0, "Nothing happened yet." );

            string initial = d.ExportToString();

            d.Modify( () =>
            {
            } ).Should().NotBeNull( "A null list of events is because an error occurred." );

            d.TransactionSerialNumber.Should().Be( 1, "Even if nothing changed, TransactionNumber is incremented." );
            eventCollector.WriteEventsFrom( 0 ).Should().Be( @"{""N"":1,""E"":[]}", "No event occured." );

            d.Modify( () =>
            {
                new Car( "Hello!" );
            } ).Should().NotBeNull( "A null list of events is because an error occurred." );

            string t2 = eventCollector.WriteEventsFrom( 0 );

            d.Modify( () =>
            {
                d.AllObjects.Single().Dispose();
            } ).Should().NotBeNull();

            string t3 = eventCollector.WriteEventsFrom( 2 );

            d.Modify( () =>
            {
                new MultiPropertyType();

            } ).Should().NotBeNull();

            string t4 = eventCollector.WriteEventsFrom( 3 );

            d.Modify( () =>
            {
                var m = d.AllObjects.OfType<MultiPropertyType>().Single();
                m.ChangeAll( "Pouf", 3, new Guid( "{B681AD83-A276-4A5C-A11A-4A22469B6A0D}" ) );

            } ).Should().NotBeNull();

            string t5 = eventCollector.WriteEventsFrom( 4 );

            d.Modify( () =>
            {
                var m = d.AllObjects.OfType<MultiPropertyType>().Single();
                m.SetDefaults();

            } ).Should().NotBeNull();

            string t6 = eventCollector.WriteEventsFrom( 5 );

            d.Modify( () =>
            {
                d.AllObjects.OfType<MultiPropertyType>().Single().Dispose();
                var l = new ObservableList<string>();
                l.Add( "One" );
                l.Add( "Two" );

            } ).Should().NotBeNull();

            string t7 = eventCollector.WriteEventsFrom( 6 );

            d.Modify( () =>
            {
                var l = d.AllObjects.OfType<ObservableList<string>>().Single();
                l[0] = "Three";
            } ).Should().NotBeNull();

            string t8 = eventCollector.WriteEventsFrom( 7 );

        }

        [Test]
        public void exporting_and_altering_sample()
        {
            var eventCollector = new TransactionEventCollector();

            var d = SampleDomain.CreateSample( eventCollector );

            d.TransactionSerialNumber.Should().Be( 1 );

            string initial = d.ExportToString();

            TestHelper.Monitor.Info( initial );
            d.Modify( () =>
            {
                var g2 = d.AllObjects.OfType<Garage>().Single( g => g.CompanyName == null );
                g2.CompanyName = "Signature Code";
            } );
            string t1 = eventCollector.WriteEventsFrom( 1 );

            d.Modify( () =>
            {
                var g2 = d.AllObjects.OfType<Garage>().Single( g => g.CompanyName == "Signature Code" );
                g2.Cars.Clear();
                var newOne = new Mechanic( g2 ) { FirstName = "X", LastName = "Y" };
            } );
            string t2 = eventCollector.WriteEventsFrom( 2 );

            d.Modify( () =>
            {
                var spi = d.AllObjects.OfType<Mechanic>().Single( m => m.LastName == "Spinelli" );
                spi.Dispose();
            } );
            string t3 = eventCollector.WriteEventsFrom( 3 );

            d.Modify( () =>
            {
                var g1 = d.AllObjects.OfType<Garage>().Single( g => g.CompanyName == "Boite" );
                g1.ReplacementCar.Remove( g1.Cars[0] );
            } );
            string t4 = eventCollector.WriteEventsFrom( 3 );

        }

        [Test]
        public void exporting_and_altering_ApplicatinState()
        {
            var eventCollector = new TransactionEventCollector();

            var d = new ObservableDomain<RootSample.ApplicationState>( eventCollector );
            d.Modify( () =>
            {
                var p1 = new RootSample.Product( "Name n°1", 12 );
                p1.ExtraData.Add( "Toto", "TVal" );
                p1.ExtraData.Add( "Tata", "TVal" );
                d.Root.Products.Add( new RootSample.ProductState( p1 ) { Name = "Product n°1" } );
            } );
            d.Root.Products[0].GetOId().Should().Be( 4, "Product n°1 OId is 4." );
            d.TransactionSerialNumber.Should().Be( 1 );

            string initial = d.ExportToString();
            initial.Should().ContainAll( "Name n°1", "Product n°1", @"""CurrentProduct"":null" );
            initial.Should().Contain( @"[""Toto"",""TVal""]" );
            initial.Should().Contain( @"[""Tata"",""TVal""]" );
            d.Modify( () =>
            {
                var p2 = new RootSample.Product( "Name n°2", 22 );
                p2.ExtraData.Add( "Ex2", ">>Ex2" );
                d.Root.Products.Add( new RootSample.ProductState( p2 ) { Name = "Product n°2" } );
                d.Root.CurrentProduct = d.Root.Products[1];
            } );
            d.Root.Products[1].GetOId().Should().Be( 3, "Product n°2 OId is 3." );

            string t1 = eventCollector.WriteEventsFrom( 1 );
            // p2 is the object n°3.
            t1.Should().Contain( @"[""N"",3,""""]" );
            // p2.ExtraData is exported as a Map.
            t1.Should().Contain( @"[""Ex2"","">>Ex2""]" );
            // ApplicationState.CurrentProduct is p2:
            t1.Should().Contain( @"[""C"",0,3,{"">"":3}]" );

            d.Modify( () =>
            {
                d.Root.CurrentProduct.Name.Should().Be( "Product n°2" );
                d.Root.SkipToNextProduct();
                d.Root.CurrentProduct.Name.Should().Be( "Product n°1" );
            } );
            string t2 = eventCollector.WriteEventsFrom( 2 );
            // Switch to Product n°1 (OId is 4).
            t2.Should().Contain( @"[""C"",0,3,{"">"":4}]" );
        }

    }
}
