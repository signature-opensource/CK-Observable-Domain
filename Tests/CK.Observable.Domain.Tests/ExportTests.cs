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
            using( var d = new ObservableDomain(TestHelper.Monitor, nameof(doc_demo), startTimer: true ) )
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

            using( var d = new ObservableDomain( TestHelper.Monitor, "TEST", startTimer: true ) )
            {
                eventCollector.CollectEvent( d, false );
                eventCollector.LastEventChanged += TrackLastEvent;
                d.TransactionSerialNumber.Should().Be( 0, "Nothing happened yet." );

                string initial = d.ExportToString();

                d.Modify( TestHelper.Monitor, () =>
                {
                } ).Success.Should().BeTrue();

                d.TransactionSerialNumber.Should().Be( 1, "Even if nothing changed, TransactionNumber is incremented." );
                LastEvent.TransactionNumber.Should().Be( 1 );
                LastEvent.ExportedEvents.Should().BeEmpty();

                // Transaction number 1 is not kept: null means "I can't give you the diff, do a full export!".
                eventCollector.GetTransactionEvents( 0 ).Should().BeNull();
                eventCollector.GetTransactionEvents( 1 ).Should().BeEmpty();
                // Transaction number 1 is not kept: empty means "I can't give you the diff: tour transaction number is too big.".
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
        public void GetTransactionEvents_semantics()
        {
            using( var d = new ObservableDomain(TestHelper.Monitor, "TEST", startTimer: true ) )
            {
                var eventCollector = new JsonEventCollector( d );
                eventCollector.LastEventChanged += TrackLastEvent;

                d.TransactionSerialNumber.Should().Be( 0, "Nothing happened yet." );
                eventCollector.GetTransactionEvents( 0 ).Should().BeNull( "Asking for 0: a full export must be made." );
                eventCollector.GetTransactionEvents( 1 ).Should().BeEmpty( "Asking for any number greater or equal to the current transaction number: empty means transaction number is too big." );
                eventCollector.GetTransactionEvents( 2 ).Should().BeEmpty();

                ObservableList<int>? oneObject = null;
                d.Modify( TestHelper.Monitor, () =>
                {
                    oneObject = new ObservableList<int>();

                } ).Success.Should().BeTrue();
                Debug.Assert( oneObject != null );

                d.TransactionSerialNumber.Should().Be( 1, "TransactionNumber is incremented." );
                LastEvent.TransactionNumber.Should().Be( 1 );
                LastEvent.ExportedEvents.Should().BeEmpty( "The event n°1 is special, it is sent empty: a full export must be made." );
                var event1 = LastEvent;

                eventCollector.GetTransactionEvents( 0 ).Should().BeNull( "Asking for 0: a full export must always be made." );
                eventCollector.GetTransactionEvents( 1 ).Should().BeEmpty( "Asking for any number greater or equal to the current transaction number: empty means transaction number is too big." );
                eventCollector.GetTransactionEvents( 2 ).Should().BeEmpty();

                d.Modify( TestHelper.Monitor, () =>
                {
                    oneObject.Add( 1 );

                } ).Success.Should().BeTrue();
                d.TransactionSerialNumber.Should().Be( 2, "TransactionNumber is incremented." );
                LastEvent.TransactionNumber.Should().Be( 2 );
                LastEvent.ExportedEvents.Should().Be( "[\"I\",0,0,1]" );
                var event2 = LastEvent;

                eventCollector.GetTransactionEvents( 0 ).Should().BeNull( "Asking for 0: a full export must always be made." );
                eventCollector.GetTransactionEvents( 1 ).Should().BeEquivalentTo( new[] { event2 } );
                eventCollector.GetTransactionEvents( 2 ).Should().BeEmpty();

                d.Modify( TestHelper.Monitor, () =>
                {
                    oneObject.Dispose();

                } ).Success.Should().BeTrue();

                d.TransactionSerialNumber.Should().Be( 3, "TransactionNumber is incremented." );
                LastEvent.TransactionNumber.Should().Be( 3 );
                LastEvent.ExportedEvents.Should().Be( "[\"D\",0]" );
                var event3 = LastEvent;

                eventCollector.GetTransactionEvents( 0 ).Should().BeNull( "Asking for 0: a full export must always be made." );
                eventCollector.GetTransactionEvents( 1 ).Should().BeEquivalentTo( new[] { event2, event3 } );
                eventCollector.GetTransactionEvents( 2 ).Should().BeEquivalentTo( new[] { event3 } );

            }
        }

        [Test]
        public void exporting_and_altering_sample()
        {
            var eventCollector = new JsonEventCollector();
            eventCollector.LastEventChanged += TrackLastEvent;

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
            eventCollector.LastEventChanged += TrackLastEvent;

            using( var d = new ObservableDomain<RootSample.ApplicationState>(TestHelper.Monitor, "TEST", startTimer: true, client: new Clients.ConcreteMemoryTransactionProviderClient() ) )
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



        public class TryingToExportNotExportableProperties1 : ObservableObject
        {
            // ObservableObjects and InternalObjects MUST NOT interact with any domain directly.
            public ObservableDomain ThisIsVeryBad { get; }
        }

        public class TryingToExportNotExportableProperties2 : ObservableObject
        {
            // This is also bad: the DomainView is a small struct that isolates the domain
            // and is tied to this object reference.
            // Each ObservableObjects and InternalObjects have their own and must interact only with it.
            public DomainView ThisIsBad => Domain;
        }

        public class TryingToExportNotExportableProperties3 : ObservableObject
        {
            // Error on property can be set, but this obviously prevents the whole type to be exported.
            [NotExportable(Error = "Missed..." )]
            public int NoWay { get; }
        }

        [Test]
        public void ObservableDomain_and_DomainView_is_NotExportable_and_any_other_types_can_be()
        {
            using var d = new ObservableDomain(TestHelper.Monitor, nameof(ObservableDomain_and_DomainView_is_NotExportable_and_any_other_types_can_be), startTimer: true );
            var eventCollector = new JsonEventCollector( d );
            d.Modify( TestHelper.Monitor, () =>
            {
                d.TransactionSerialNumber.Should().Be( 0 );
                new TryingToExportNotExportableProperties1();

            } ).Success.Should().BeTrue();

            d.Invoking( x => x.ExportToString() )
                .Should().Throw<CKException>()
                .WithMessage( "Exporting 'ObservableDomain' is forbidden: No interaction with the ObservableDomain must be made from the observable objects." );

            d.Modify( TestHelper.Monitor, () =>
            {
                d.AllObjects.Single().Dispose();
                new TryingToExportNotExportableProperties2();

            } ).Success.Should().BeTrue();

            d.Invoking( x => x.ExportToString() )
                .Should().Throw<CKException>()
                .WithMessage( "Exporting 'DomainView' is forbidden: DomainView must not be exposed. Only the protected Domain should be used." );

            d.Modify( TestHelper.Monitor, () =>
            {
                d.AllObjects.Single().Dispose();
                new TryingToExportNotExportableProperties3();
            } ).Success.Should().BeTrue();

            d.Invoking( x => x.ExportToString() )
                .Should().Throw<CKException>()
                .WithMessage( "Exporting 'TryingToExportNotExportableProperties3.NoWay' is forbidden: Missed..." );
        }


        public class TimerAndRemiderProperties : ObservableObject
        {
            public TimerAndRemiderProperties()
            {
                Timer = new ObservableTimer( DateTime.UtcNow.AddDays( 5 ), 1000 );
                Reminder = new ObservableReminder( Timer.DueTimeUtc );
            }

            public ObservableTimer Timer { get; }

            public ObservableReminder Reminder { get; }

            public int ThisIsExported { get; set; }
        }

        [Test]
        public void timers_and_reminders_are_NotExportable()
        {
            using var d = new ObservableDomain(TestHelper.Monitor, nameof(timers_and_reminders_are_NotExportable), startTimer: true );
            var eventCollector = new JsonEventCollector( d );
            // To skip the initial transaction where no events are collectable.
            d.Modify( TestHelper.Monitor, null );

            TransactionResult t = d.Modify( TestHelper.Monitor, () =>
            {
                d.TransactionSerialNumber.Should().Be( 1, "Not incremented yet (still inside the transaction n°2)." );
                new TimerAndRemiderProperties();
            } );
            d.ExportToString().Should().NotContainAny( "Timer", "Reminder" ).And.Contain( "ThisIsExported" );
            var events = eventCollector.GetTransactionEvents( 1 ).Single().ExportedEvents;
            events.Should().NotContainAny( "Timer", "Reminder" ).And.Contain( "ThisIsExported" );
        }
    }
}
