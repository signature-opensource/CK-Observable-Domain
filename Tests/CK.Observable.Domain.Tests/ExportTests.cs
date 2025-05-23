using CK.Core;
using CK.Observable.Domain.Tests.Sample;
using Shouldly;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Domain.Tests;


[TestFixture]
public class ExportTests
{
    static JsonEventCollector.TransactionEvent? LastEvent = null;
    static void TrackLastEvent( IActivityMonitor m, JsonEventCollector.TransactionEvent e ) => LastEvent = e;

    [Test]
    public async Task doc_demo_Async()
    {
        var eventCollector = new JsonEventCollector();
        using( var d = new ObservableDomain(TestHelper.Monitor, nameof(doc_demo_Async), startTimer: true ) )
        {
            eventCollector.CollectEvent( d, false );
            Car car = null!;
            await d.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                car = new Car( "Titine" );
            } );
            var initial = d.ExportToString();

            await d.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                car.Destroy();
            } );

            await d.ModifyThrowAsync( TestHelper.Monitor, () =>
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

            var last = d.ExportToString();
            Console.WriteLine( last );
        }
    }

    [Test]
    public async Task exporting_and_altering_simple_Async()
    {
        var eventCollector = new JsonEventCollector();

        using( var d = new ObservableDomain( TestHelper.Monitor, "TEST", startTimer: true ) )
        {
            eventCollector.CollectEvent( d, false );
            eventCollector.LastEventChanged.Sync += TrackLastEvent;
            d.TransactionSerialNumber.ShouldBe( 0, "Nothing happened yet." );

            var initial = d.ExportToString();

            await d.ModifyThrowAsync( TestHelper.Monitor, null );
            await Task.Delay( 20 );

            d.TransactionSerialNumber.ShouldBe( 1, "Even if nothing changed, TransactionNumber is incremented." );
            LastEvent.TransactionNumber.ShouldBe( 1 );
            LastEvent.ExportedEvents.ShouldBeEmpty();

            // Transaction number 1 is not kept: null means "I can't give you the diff, do a full export!".
            eventCollector.GetTransactionEvents( 0 ).Events.ShouldBeNull();
            eventCollector.GetTransactionEvents( 1 ).Events.ShouldBeEmpty();
            // Transaction number 1 is not kept: empty means "I can't give you the diff: your transaction number is too big.".
            var r2 = eventCollector.GetTransactionEvents( 2 );
            r2.TransactionNumber.ShouldBe( 0 );
            r2.Events.ShouldBeEmpty();

            await d.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                new Car( "Hello!" );
            } );
            await Task.Delay( 20 );

            LastEvent.TransactionNumber.ShouldBe( 2 );
            string t2 = LastEvent.ExportedEvents;

            await d.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                d.AllObjects.Items.Single().Destroy();
            } );
            await Task.Delay( 20 );

            string t3 = LastEvent.ExportedEvents;

            await d.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                new MultiPropertyType();

            } );
            await Task.Delay( 20 );

            string t4 = LastEvent.ExportedEvents;

            await d.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                var m = d.AllObjects.Items.OfType<MultiPropertyType>().Single();
                m.ChangeAll( "Pouf", 3, new Guid( "{B681AD83-A276-4A5C-A11A-4A22469B6A0D}" ) );

            } );
            await Task.Delay( 20 );

            string t5 = LastEvent.ExportedEvents;

            await d.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                var m = d.AllObjects.Items.OfType<MultiPropertyType>().Single();
                m.SetDefaults();

            } );
            await Task.Delay( 20 );

            string t6 = LastEvent.ExportedEvents;

            await d.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                d.AllObjects.Items.OfType<MultiPropertyType>().Single().Destroy();
                var l = new ObservableList<string>();
                l.Add( "One" );
                l.Add( "Two" );

            } );
            await Task.Delay( 20 );

            string t7 = LastEvent.ExportedEvents;

            await d.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                var l = d.AllObjects.Items.OfType<ObservableList<string>>().Single();
                l[0] = "Three";
            } );
            await Task.Delay( 20 );

            string t8 = LastEvent.ExportedEvents;

        }
    }


    [Test]
    public async Task GetTransactionEvents_semantics_Async()
    {
        using( var d = new ObservableDomain(TestHelper.Monitor, "TEST", startTimer: true ) )
        {
            var eventCollector = new JsonEventCollector( d );
            eventCollector.LastEventChanged.Sync += TrackLastEvent;

            d.TransactionSerialNumber.ShouldBe( 0, "Nothing happened yet." );
            var r0 = eventCollector.GetTransactionEvents( 0 );
            r0.TransactionNumber.ShouldBe( 0 );
            r0.Events.ShouldBeNull( "Asking for 0: a full export must be made." );
            var r1 = eventCollector.GetTransactionEvents( 1 );
            r1.TransactionNumber.ShouldBe( 0 );
            r1.Events.ShouldBeEmpty( "Asking for any number greater or equal to the current transaction number: empty means transaction number is too big." );
            var r2 = eventCollector.GetTransactionEvents( 2 );
            r2.TransactionNumber.ShouldBe( 0 );
            r2.Events.ShouldBeEmpty();

            ObservableList<int>? oneObject = null;
            await d.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                oneObject = new ObservableList<int>();

            } );
            Debug.Assert( oneObject != null );
            await Task.Delay( 20 );

            d.TransactionSerialNumber.ShouldBe( 1, "TransactionNumber is incremented." );
            LastEvent.TransactionNumber.ShouldBe( 1 );
            LastEvent.ExportedEvents.ShouldBeEmpty( "The event n°1 is special, it is sent empty: a full export must be made." );
            var event1 = LastEvent;

            r0 = eventCollector.GetTransactionEvents( 0 );
            r0.Events.ShouldBeNull( "Asking for 0: a full export must always be made." );
            r1 = eventCollector.GetTransactionEvents( 1 );
            r1.Events.ShouldBeEmpty( "Asking for any number greater or equal to the current transaction number: empty means transaction number is too big." );
            r2 = eventCollector.GetTransactionEvents( 2 );
            r2.Events.ShouldBeEmpty();

            await d.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                oneObject.Add( 1 );

            } );
            await Task.Delay( 20 );
            d.TransactionSerialNumber.ShouldBe( 2, "TransactionNumber is incremented." );
            LastEvent.TransactionNumber.ShouldBe( 2 );
            LastEvent.ExportedEvents.ShouldBe( "[\"I\",0,0,1]" );
            var event2 = LastEvent;

            r0 = eventCollector.GetTransactionEvents( 0 );
            r0.TransactionNumber.ShouldBe( 2 );
            r0.Events.ShouldBeNull( "Asking for 0: a full export must always be made." );
            r1 = eventCollector.GetTransactionEvents( 1 );
            r1.Events.ShouldBe( new[] { event2 } );
            r2 = eventCollector.GetTransactionEvents( 2 );
            r2.Events.ShouldBeEmpty();

            await d.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                oneObject.Destroy();

            } );
            await Task.Delay( 20 );

            d.TransactionSerialNumber.ShouldBe( 3, "TransactionNumber is incremented." );
            LastEvent.TransactionNumber.ShouldBe( 3 );
            LastEvent.ExportedEvents.ShouldBe( "[\"D\",0]" );
            var event3 = LastEvent;

            r0 = eventCollector.GetTransactionEvents( 0 );
            r0.Events.ShouldBeNull( "Asking for 0: a full export must always be made." );
            r1 = eventCollector.GetTransactionEvents( 1 );
            r1.Events.ShouldBe( new[] { event2, event3 } );
            r2 = eventCollector.GetTransactionEvents( 2 );
            r2.Events.ShouldBe( new[] { event3 } );
        }
    }

    [Test]
    public async Task exporting_and_altering_sample_Async()
    {
        var eventCollector = new JsonEventCollector();
        eventCollector.LastEventChanged.Sync += TrackLastEvent;

        using( var d = await SampleDomain.CreateSampleAsync() )
        {
            eventCollector.CollectEvent( d, false );
            d.TransactionSerialNumber.ShouldBe( 1 );

            var initial = d.ExportToString();
            Debug.Assert( initial != null );

            TestHelper.Monitor.Info( initial );
            await d.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                var g2 = d.AllObjects.Items.OfType<Garage>().Single( g => g.CompanyName == null );
                g2.CompanyName = "Signature Code";
            } );
            await Task.Delay( 20 );
            Debug.Assert( LastEvent != null );

            LastEvent.TransactionNumber.ShouldBe( 2 );
            string t1 = LastEvent.ExportedEvents;
            t1.ShouldBe( "[\"C\",16,0,\"Signature Code\"]" );

            await d.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                var g2 = d.AllObjects.Items.OfType<Garage>().Single( g => g.CompanyName == "Signature Code" );
                g2.Cars.Clear();
                var newOne = new Mechanic( g2 ) { FirstName = "X", LastName = "Y" };
            } );
            await Task.Delay( 20 );
            LastEvent.TransactionNumber.ShouldBe( 3 );
            string t2 = LastEvent.ExportedEvents;

            await d.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                var spi = d.AllObjects.Items.OfType<Mechanic>().Single( m => m.LastName == "Spinelli" );
                spi.Destroy();
            } );
            await Task.Delay( 20 );
            LastEvent.TransactionNumber.ShouldBe( 4 );
            string t3 = LastEvent.ExportedEvents;
            t3.ShouldBe( "[\"R\",17,5],[\"D\",25]" );

            await d.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                var g1 = d.AllObjects.Items.OfType<Garage>().Single( g => g.CompanyName == "Boite" );
                g1.ReplacementCar.Remove( g1.Cars[0] );
            } );
            await Task.Delay( 20 );
            LastEvent.TransactionNumber.ShouldBe( 5 );
            string t4 = LastEvent.ExportedEvents;
            t4.ShouldBe( "[\"K\",3,{\"=\":4}]" );

        }
    }

    [Test]
    public async Task exporting_and_altering_ApplicationState_Async()
    {
        var eventCollector = new JsonEventCollector();
        eventCollector.LastEventChanged.Sync += TrackLastEvent;

        using( var d = new ObservableDomain<RootSample.ApplicationState>( TestHelper.Monitor,
                                                                          "TEST",
                                                                          startTimer: true,
                                                                          client: new Clients.ConcreteMemoryTransactionProviderClient() ) )
        {
            eventCollector.CollectEvent( d, false );
            await d.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                var p1 = new RootSample.ProductInfo( "Name n°1", 12 );
                p1.ExtraData.Add( "Toto", "TVal" );
                p1.ExtraData.Add( "Tata", "TVal" );
                d.Root.Products.Add( p1.Name, p1 );
                d.Root.ProductStateList.Add( new RootSample.Product( p1 ) { Name = "Product n°1" } );
                d.Root.CurrentProductState = d.Root.ProductStateList[0];
            } );
            await Task.Delay( 20 );
            Debug.Assert( LastEvent != null );

            d.Root.ProductStateList[0].OId.Index.ShouldBe( 7, "Product n°1 OId.Index is 7." );
            d.TransactionSerialNumber.ShouldBe( 1 );

            var initial = d.ExportToString().ShouldNotBeNull();
            initial.ShouldContain( "Name n°1" );
            initial.ShouldContain( "Product n°1" );
            initial.ShouldContain( @"""CurrentProductState"":{"">"":7}" );
            initial.ShouldContain( @"[""Toto"",""TVal""]" );
            initial.ShouldContain( @"[""Tata"",""TVal""]" );
            await d.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                var p2 = new RootSample.ProductInfo( "Name n°2", 22 );
                d.Root.Products.Add( p2.Name, p2 );
                p2.ExtraData.Add( "Ex2", ">>Ex2" );
                d.Root.ProductStateList.Add( new RootSample.Product( p2 ) { Name = "Product n°2" } );
                d.Root.CurrentProductState = d.Root.ProductStateList[1];
            } );
            d.Root.ProductStateList[1].OId.Index.ShouldBe( 6, "Product n°2 OId.Index is 6." );

            await Task.Delay( 20 );
            string t1 = LastEvent.ExportedEvents;
            // p2 is the object n°5.
            t1.ShouldContain( @"[""N"",6,""""]" );
            // p2.ExtraData is exported as a Map.
            t1.ShouldContain( @"[""Ex2"","">>Ex2""]" );
            // ApplicationState.CurrentProduct is p2:
            t1.ShouldContain( @"[""C"",0,1,{""="":6}]" );

            await d.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                Debug.Assert( d.Root.CurrentProductState != null );
                d.Root.CurrentProductState.Name.ShouldBe( "Product n°2" );
                d.Root.SkipToNextProduct();
                d.Root.CurrentProductState.Name.ShouldBe( "Product n°1" );
            } );
            await Task.Delay( 20 );
            string t2 = LastEvent.ExportedEvents;
            // Switch to Product n°1 (OId is 7).
            t2.ShouldContain( @"[""C"",0,1,{""="":7}]" );
        }
    }


    [SerializationVersion(0)]
    public class TryingToExportNotExportableProperties1 : ObservableObject
    {
        public TryingToExportNotExportableProperties1()
        {
        }

        TryingToExportNotExportableProperties1( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
            : base( BinarySerialization.Sliced.Instance )
        {
        }

        public static void Write( BinarySerialization.IBinarySerializer s, in TryingToExportNotExportableProperties1 o )
        {
        }

        // ObservableObjects and InternalObjects MUST NOT interact with any domain directly.
        public ObservableDomain? ThisIsVeryBad { get; }
    }

    [SerializationVersion(0)]
    public class TryingToExportNotExportableProperties2 : ObservableObject
    {
        public TryingToExportNotExportableProperties2()
        {
        }

        TryingToExportNotExportableProperties2( BinarySerialization.IBinaryDeserializer d, BinarySerialization.ITypeReadInfo info )
           : base( BinarySerialization.Sliced.Instance )
        {
        }

        public static void Write( BinarySerialization.IBinarySerializer s, in TryingToExportNotExportableProperties2 o )
        {
        }

        // This is also bad: the DomainView is a small struct that isolates the domain
        // and is tied to this object reference.
        // Each ObservableObjects and InternalObjects have their own and must interact only with it.
        public DomainView ThisIsBad => Domain;
    }

    [SerializationVersion( 0 )]
    public class TryingToExportNotExportableProperties3 : ObservableObject
    {
        public TryingToExportNotExportableProperties3()
        {
        }

        TryingToExportNotExportableProperties3( BinarySerialization.IBinaryDeserializer s, BinarySerialization.ITypeReadInfo info )
           : base( BinarySerialization.Sliced.Instance )
        {
        }

        public static void Write( BinarySerialization.IBinarySerializer s, in TryingToExportNotExportableProperties3 o )
        {
        }

        // Error on property can be set, but this obviously prevents the whole type to be exported.
        [NotExportable( Error = "Missed..." )]
        public int NoWay { get; }
    }

    [Test]
    public async Task ObservableDomain_and_DomainView_is_NotExportable_and_any_other_types_can_be_Async()
    {
        using var d = new ObservableDomain(TestHelper.Monitor, nameof(ObservableDomain_and_DomainView_is_NotExportable_and_any_other_types_can_be_Async), startTimer: true );
        var eventCollector = new JsonEventCollector( d );
        await d.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            d.TransactionSerialNumber.ShouldBe( 0 );
            new TryingToExportNotExportableProperties1();

        } );

        Util.Invokable( () => d.ExportToString() )
            .ShouldThrow<CKException>()
            .Message.ShouldBe( "Exporting 'ObservableDomain' is forbidden: No interaction with the ObservableDomain must be made from the observable objects." );

        await d.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            d.AllObjects.Items.Single().Destroy();
            new TryingToExportNotExportableProperties2();

        } );

        Util.Invokable( () => d.ExportToString() )
            .ShouldThrow<CKException>()
            .Message.ShouldBe( "Exporting 'DomainView' is forbidden: DomainView must not be exposed. Only the protected Domain should be used." );

        await d.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            d.AllObjects.Items.Single().Destroy();
            new TryingToExportNotExportableProperties3();
        } );

        Util.Invokable( () => d.ExportToString() )
            .ShouldThrow<CKException>()
            .Message.ShouldBe( "Exporting 'TryingToExportNotExportableProperties3.NoWay' is forbidden: Missed..." );
    }

    [SerializationVersion(0)]
    public class TimerAndRemiderProperties : ObservableObject
    {
        public TimerAndRemiderProperties()
        {
            Timer = new ObservableTimer( DateTime.UtcNow.AddDays( 5 ), 1000 );
            Reminder = new ObservableReminder( Timer.DueTimeUtc );
        }

        TimerAndRemiderProperties( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
        {
            Timer = r.ReadObject<ObservableTimer>();
            Reminder = r.ReadObject<ObservableReminder>();
        }

        public static void Write( BinarySerialization.IBinarySerializer s, in TimerAndRemiderProperties o )
        {
            s.WriteObject( o.Timer );
            s.WriteObject( o.Reminder );
        }

        public ObservableTimer Timer { get; }

        public ObservableReminder Reminder { get; }

        public int ThisIsExported { get; set; }
    }

    [Test]
    public async Task timers_and_reminders_are_NotExportable_Async()
    {
        using var d = new ObservableDomain(TestHelper.Monitor, nameof(timers_and_reminders_are_NotExportable_Async), startTimer: true );
        var eventCollector = new JsonEventCollector( d );
        // To skip the initial transaction where no events are collectable.
        await d.ModifyThrowAsync( TestHelper.Monitor, null );

        await d.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            d.TransactionSerialNumber.ShouldBe( 1, "Not incremented yet (still inside the transaction n°2)." );
            new TimerAndRemiderProperties();
        } );
        var export = d.ExportToString().ShouldNotBeNull();
        export.ShouldNotContain( "Timer" );
        export.ShouldNotContain( "Reminder" );
        export.ShouldContain( "ThisIsExported" );
        var events = eventCollector.GetTransactionEvents( 1 ).Events!.Single().ExportedEvents;
        events.ShouldNotContain( "Timer" );
        events.ShouldNotContain( "Reminder" );
        events.ShouldContain( "ThisIsExported" );
    }

    [SerializationVersion(0)]
    public class NormalizedPathProperty : ObservableObject
    {
        public NormalizedPathProperty()
        {
            NormalizedPath = new NormalizedPath( "C:/A/b/c/d/e/f" );
        }

        NormalizedPathProperty( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
        {
            NormalizedPath = r.ReadValue<NormalizedPath>();
        }

        public static void Write( BinarySerialization.IBinarySerializer s, in NormalizedPathProperty o )
        {
            s.WriteValue( o.NormalizedPath );
        }

        public NormalizedPath NormalizedPath { get; }
    }

    [Test]
    public async Task NormalizedPaths_are_exportable_Async()
    {
        using var d = new ObservableDomain(TestHelper.Monitor, nameof(NormalizedPaths_are_exportable_Async), startTimer: true );
        var eventCollector = new JsonEventCollector( d );

        // To skip the initial transaction where no events are collectable.
        await d.ModifyThrowAsync( TestHelper.Monitor, null );

        await d.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            d.TransactionSerialNumber.ShouldBe( 1, "Not incremented yet (still inside the transaction n°2)." );
            new NormalizedPathProperty();
        } );

        d.ExportToString().ShouldContain( "NormalizedPath" );
        var events = eventCollector.GetTransactionEvents( 1 ).Events!.Single().ExportedEvents;
        events.ShouldContain( "NormalizedPath" );
    }

    [SerializationVersion( 0 )]
    public sealed class ExportableObjectWithNotExportableProperty : ObservableObject
    {
        public string ExportableProperty { get; set; }

        [NotExportable]
        public string NotExportableProperty { get; set; }

        public ExportableObjectWithNotExportableProperty()
        {
            ExportableProperty = "Hello";
            NotExportableProperty = "World";
        }

        ExportableObjectWithNotExportableProperty(
            BinarySerialization.IBinaryDeserializer r,
            BinarySerialization.ITypeReadInfo info
            )
            : base( BinarySerialization.Sliced.Instance )
        {
            ExportableProperty = r.Reader.ReadString();
            NotExportableProperty = r.Reader.ReadString();
        }

        public static void Write( BinarySerialization.IBinarySerializer s, ExportableObjectWithNotExportableProperty o )
        {
            s.Writer.Write( o.ExportableProperty );
            s.Writer.Write( o.NotExportableProperty );
        }
    }

    [SerializationVersion( 0 )]
    public sealed class InternalObjectAreNotExportableByDesign : InternalObject
    {
        public string NotExportableClassProperty { get; set; }

        public InternalObjectAreNotExportableByDesign()
        {
            NotExportableClassProperty = "HelloNotExportable";
        }

        InternalObjectAreNotExportableByDesign( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
            : base( BinarySerialization.Sliced.Instance )
        {
            NotExportableClassProperty = r.Reader.ReadString();
        }

        public static void Write( BinarySerialization.IBinarySerializer s, InternalObjectAreNotExportableByDesign o )
        {
            s.Writer.Write( o.NotExportableClassProperty );
        }
    }

    [SerializationVersion( 0 )]
    public sealed class ExportableObjectWithNotExportableClass : ObservableObject
    {
        [NotExportable]
        public InternalObjectAreNotExportableByDesign NotExportableClass { get; set; }

        public ExportableObjectWithNotExportableClass()
        {
            NotExportableClass = new InternalObjectAreNotExportableByDesign();
        }

        ExportableObjectWithNotExportableClass( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info
        )
            : base( BinarySerialization.Sliced.Instance )
        {
            NotExportableClass = r.ReadObject<InternalObjectAreNotExportableByDesign>();
        }

        public static void Write( BinarySerialization.IBinarySerializer s, ExportableObjectWithNotExportableClass o )
        {
            s.WriteObject( o.NotExportableClass );
        }
    }

    [Test]
    public async Task Empty_events_do_not_cause_a_new_event_Async()
    {
        using var d = new ObservableDomain(TestHelper.Monitor, nameof(Empty_events_do_not_cause_a_new_event_Async), startTimer: true );
        var eventCollector = new JsonEventCollector( d );

        // To skip the initial transaction where no events are collectable.
        await d.ModifyThrowAsync( TestHelper.Monitor, null );
        // N = 1

        ExportableObjectWithNotExportableProperty o = null!;
        await d.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            d.TransactionSerialNumber.ShouldBe( 1, "Not incremented yet (still inside the transaction n°2)." );
            o = new ExportableObjectWithNotExportableProperty();
        } );
        // N = 2

        string jsonExport = d.ExportToString()!;
        DomainJsonExport ex = JsonSerializer.Deserialize<DomainJsonExport>( jsonExport )!;
        ex.N.ShouldBe( 2, "_transactionSerialNumber = 2" );
        ex.C.ShouldBe( 1, "_actualObjectCount = 1" );
        ex.P.Count.ShouldBe( 4, "4 properties exist" );
        ex.O.Count.ShouldBe( 2, "2 indexed objects exist" );
        ex.R.Count.ShouldBe( 0, "There are no roots in this test domain" );

        eventCollector.LastEvent.ShouldNotBeNull( "There was an event sent on N = 2" );
        var transactionEvent = eventCollector.LastEvent!;
        transactionEvent.TransactionNumber.ShouldBe( 2, "_transactionSerialNumber = 2" );
        transactionEvent.LastExportedTransactionNumber.ShouldBe( 0, "There were no events before" );
        transactionEvent.ExportedEvents.ShouldNotBeNullOrEmpty();

        await d.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            o.NotExportableProperty = "World2";
        } );
        // N = 3
        // No event here

        eventCollector.LastEvent.ShouldBe( transactionEvent, "No event was sent" );
        transactionEvent.TransactionNumber.ShouldBe( 2, "Previous transactionSerialNumber = 2" );

        await d.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            o.ExportableProperty = "Hello2";
        } );
        // N = 4

        eventCollector.LastEvent.ShouldNotBe( transactionEvent, "A new event was sent" );
        eventCollector.LastEvent.ShouldNotBeNull( "There was an event sent on N = 4" );
        transactionEvent = eventCollector.LastEvent!;
        transactionEvent.TransactionNumber.ShouldBe( 4, "transactionSerialNumber = 4" );
        transactionEvent.LastExportedTransactionNumber.ShouldBe( 2, "Previous T with events = 2" );
        transactionEvent.TimeUtc.ShouldBe( DateTime.UtcNow, tolerance: TimeSpan.FromMinutes( 2 ) );
        transactionEvent.ExportedEvents.ShouldNotBeNullOrEmpty();
    }

    sealed class DomainJsonExport
    {
        [JsonPropertyName( "N" )] public int N { get; set; }
        [JsonPropertyName( "C" )] public int C { get; set; }
        [JsonPropertyName( "P" )] public List<string> P { get; set; } = new();
        [JsonPropertyName( "O" )] public List<JsonElement> O { get; set; } = new();
        [JsonPropertyName( "R" )] public List<int> R { get; set; } = new();
    }

    [Test]
    public async Task NotExportable_types_and_properties_should_not_leak_in_export_and_events_Async()
    {
        using var d = new ObservableDomain(TestHelper.Monitor, nameof(Empty_events_do_not_cause_a_new_event_Async), startTimer: true );
        var eventCollector = new JsonEventCollector( d );

        // To skip the initial transaction where no events are collectable.
        await d.ModifyThrowAsync( TestHelper.Monitor, null );
        // N = 1

        ExportableObjectWithNotExportableClass o = null!;
        await d.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            d.TransactionSerialNumber.ShouldBe( 1, "Not incremented yet (still inside the transaction n°2)." );
            o = new ExportableObjectWithNotExportableClass();
        }, waitForDomainPostActionsCompletion: true );
        // N = 2

        eventCollector.LastEvent.ShouldNotBeNull( "There was an event sent on N = 2" );
        eventCollector.LastEvent.ExportedEvents
            .ShouldNotContain( "HelloNotExportable", "This property value is in a type marked NotExportable." );
        eventCollector.LastEvent.ExportedEvents
            .ShouldNotContain( "NotExportableClassProperty", "This property name is in a InternalObject." );
        // This is NOT good!
        // A Property marked NotExportable SHOULD NOT leak (even its name)...
        //eventCollector.LastEvent!.ExportedEvents.ShouldNotContain( "NotExportableClass", "This property name is on a Property marked NotExportable." );

        string jsonExport = null!;
        Action act = () =>
        {
            jsonExport = d.ExportToString()!;
        };
        act.ShouldNotThrow("Exporting a Type with a Property marked NotExportable, having a type marked NotExportable should be okay");

        DomainJsonExport export = JsonSerializer.Deserialize<DomainJsonExport>( jsonExport )!;
        export.N.ShouldBe( 2, "_transactionSerialNumber = 2" );

        var transactionEvent = eventCollector.LastEvent!;
        transactionEvent.TransactionNumber.ShouldBe( 2, "_transactionSerialNumber = 2" );
        transactionEvent.LastExportedTransactionNumber.ShouldBe( 0, "There were no events before" );
        transactionEvent.TimeUtc.ShouldBe( DateTime.UtcNow, TimeSpan.FromMinutes( 2 ) );
        transactionEvent.ExportedEvents.ShouldNotBeNullOrEmpty();

        jsonExport.ShouldNotContain( "HelloNotExportable", "This property value is in a type marked NotExportable." );
        jsonExport.ShouldNotContain( "NotExportableClassProperty", "This property name is in a type marked NotExportable." );
        // This is NOT good!
        // A Property marked NotExportable SHOULD NOT leak.
        //jsonExport.ShouldNotContain( "NotExportableClass", "This property name is on a Property marked NotExportable." );
    }
}
