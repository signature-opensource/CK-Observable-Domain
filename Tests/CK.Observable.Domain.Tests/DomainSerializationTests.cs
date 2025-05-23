using CK.Core;
using CK.Observable.Domain.Tests.Sample;
using Shouldly;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Domain.Tests;

[TestFixture]
public class DomainSerializationTests
{

    class A
    {
        public readonly string Called;

        public A()
        {
            Called = Virtual();
        }

        protected virtual string Virtual() => "A";
    }

    class B : A
    {
        protected override string Virtual() => "B";
    }

    // C# allows virtual methods to be called by the constructor of a base class.
    // Collections Write method calls a virtual WriteContent(): specialized collections can override WriteContent().
    // And collections use a virtual ReadContent( int version ) that may be use to handle specific deserialization.
    [Test]
    public void how_specialized_collections_can_handle_specific_serialization_issues()
    {
        var o = new B();
        o.Called.ShouldBe( "B" );
    }

    [Test]
    public async Task simple_serialization_Async()
    {
        using var domain = new ObservableDomain( TestHelper.Monitor, "TEST", startTimer: false );
        await domain.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            var car = new Car( "Hello" );
            car.TestSpeed = 10;
        } );

        using var d2 = TestHelper.CloneDomain( domain );
        domain.IsDisposed.ShouldBeTrue( "SaveAndLoad disposed it." );

        IReadOnlyList<ObservableEvent>? events = null;
        d2.TransactionDone += ( d, ev ) => events = ev.Events;

        var c = d2.AllObjects.Items.OfType<Car>().Single();
        c.Name.ShouldBe( "Hello" );
        c.TestSpeed.ShouldBe( 10 );

        await d2.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            c.TestSpeed = 10000;
        } );
        events.Count.ShouldBe( 1 );
    }

    [Test]
    public async Task serialization_with_mutiple_types_Async()
    {
        using var domain = new ObservableDomain( TestHelper.Monitor, nameof( serialization_with_mutiple_types_Async ), startTimer: true );
        MultiPropertyType defValue = null!;
        await domain.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            defValue = new MultiPropertyType();
            var other = new MultiPropertyType();
            domain.AllObjects.Items.First().ShouldBeSameAs( defValue );
            domain.AllObjects.Items.ElementAt( 1 ).ShouldBeSameAs( other );
        } );

        using var d2 = TestHelper.CloneDomain( domain );
        d2.AllObjects.Items.OfType<MultiPropertyType>().All( o => o.Equals( defValue ) );

        await d2.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            var other = d2.AllObjects.Items.OfType<MultiPropertyType>().ElementAt( 1 );
            other.ChangeAll( "Changed", 3, Guid.NewGuid() );
        } );
        d2.AllObjects.Items.First().ShouldBe( defValue );
        d2.AllObjects.Items.ElementAt( 1 ).ShouldNotBe( defValue );

        using( var d3 = TestHelper.CloneDomain( d2 ) )
        {
            d3.AllObjects.Items.First().ShouldBe( defValue );
            d3.AllObjects.Items.ElementAt( 1 ).ShouldNotBe( defValue );
        }
    }

    [Test]
    public async Task with_cycle_serialization_Async()
    {
        using var domain = new ObservableDomain( TestHelper.Monitor, nameof( with_cycle_serialization_Async ), startTimer: true );
        await domain.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            var g = new Garage();
            g.CompanyName = "Hello";
            var car = new Car( "1" );
            var m = new Mechanic( g ) { FirstName = "Hela", LastName = "Bas" };
            m.CurrentCar = car;
        } );
        // SaveAndLoad disposes domain.
        // Captures the Garage's OId since it is set to invalid.
        var g1OId = domain.AllObjects.Items.OfType<Garage>().Single().OId;

        using var d2 = TestHelper.CloneDomain( domain );
        var g2 = d2.AllObjects.Items.OfType<Garage>().Single();
        g2.CompanyName.ShouldBe( "Hello" );
        g2.OId.ShouldBe( g1OId );
    }


    [Test]
    public async Task with_cycle_serialization_between_2_objects_Async()
    {
        using var domain = new ObservableDomain( TestHelper.Monitor, nameof( with_cycle_serialization_between_2_objects_Async ), startTimer: true );
        await domain.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            var p1 = new Person() { FirstName = "A" };
            var p2 = new Person() { FirstName = "B", Friend = p1 };
            p1.Friend = p2;
        } );
        var pA1 = domain.AllObjects.Items.OfType<Person>().Single( p => p.FirstName == "A" );
        var pB1 = domain.AllObjects.Items.OfType<Person>().Single( p => p.FirstName == "B" );

        pA1.Friend.ShouldBeSameAs( pB1 );
        pB1.Friend.ShouldBeSameAs( pA1 );

        using var d2 = TestHelper.CloneDomain( domain );
        var pA2 = d2.AllObjects.Items.OfType<Person>().Single( p => p.FirstName == "A" );
        var pB2 = d2.AllObjects.Items.OfType<Person>().Single( p => p.FirstName == "B" );

        pA2.Friend.ShouldBeSameAs( pB2 );
        pB2.Friend.ShouldBeSameAs( pA2 );
    }

    [Test]
    public async Task ultimate_cycle_serialization_Async()
    {
        using var domain = new ObservableDomain( TestHelper.Monitor, nameof( ultimate_cycle_serialization_Async ), startTimer: true );
        await domain.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            var p = new Person() { FirstName = "P" };
            p.Friend = p;
        } );
        var p1 = domain.AllObjects.Items.OfType<Person>().Single();
        p1.Friend.ShouldBeSameAs( p1 );

        using var d2 = TestHelper.CloneDomain( domain );
        var p2 = d2.AllObjects.Items.OfType<Person>().Single();
        p2.Friend.ShouldBeSameAs( p2 );
    }

    [Test]
    public async Task sample_graph_serialization_inside_read_or_write_locks_Async()
    {
        using( var domain = await SampleDomain.CreateSampleAsync() )
        {
            using( var d2 = TestHelper.CloneDomain( domain, initialDomainDispose: false ) )
            {
                SampleDomain.CheckSampleGarage( d2 );
            }

            domain.Read( TestHelper.Monitor, () =>
            {
                using( var d = TestHelper.CloneDomain( domain, initialDomainDispose: false ) )
                {
                    SampleDomain.CheckSampleGarage( d );
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

        LoadHookTester( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
            : base( BinarySerialization.Sliced.Instance )
        {
            Count = r.Reader.ReadInt32();
        }

        public static void Write( BinarySerialization.IBinarySerializer s, in LoadHookTester o )
        {
            s.Writer.Write( o.Count );
        }

        public int Count { get; private set; }

        public ObservableTimer? Timer { get; }

    }

    [Test]
    public async Task persisting_disposed_objects_reference_tracking_Async()
    {
        // Will be disposed by SaveAndLoad.
        var d = new ObservableDomain( TestHelper.Monitor, nameof( Load_can_disable_TimeManager_Async ), startTimer: true );
        await d.ModifyThrowAsync( TestHelper.Monitor, () =>
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
        using var d2 = TestHelper.CloneDomain( d );

        await d2.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            var list = d2.AllObjects.Items.OfType<ObservableList<object>>().Single();

            int i = 0;
            var timer = (ObservableTimer)list[i++]; timer.Destroy();
            var reminder = (ObservableReminder)list[i++]; reminder.Destroy();
            var obsOject = (Person)list[i++]; obsOject.Destroy();
            var intObject = (SuspendableClock)list[i++]; intObject.Destroy();
        } );
        ObservableDomain.IdempotenceSerializationCheck( TestHelper.Monitor, d2 );

        await d2.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            var list = d2.AllObjects.Items.OfType<ObservableList<object>>().Single();
            list.Count.ShouldBe( 4 );
            foreach( IDestroyable o in list )
            {
                o.IsDestroyed.ShouldBeTrue();
            }
        } );

        Debug.Assert( d2.CurrentLostObjectTracker != null );
        d2.CurrentLostObjectTracker.ReferencedDestroyed.Count.ShouldBe( 4 );
    }

    [Test]
    public async Task Load_can_disable_TimeManager_Async()
    {
        ElapsedFired = false;

        var d = new ObservableDomain( TestHelper.Monitor, nameof( Load_can_disable_TimeManager_Async ), startTimer: true );
        await d.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            var r = new ObservableReminder( DateTime.UtcNow.AddMilliseconds( 100 ) );
            r.Elapsed += OnElapsedFire;
            d.TimeManager.IsRunning.ShouldBeTrue();
            d.TimeManager.Reminders.Single().ShouldBeSameAs( r );
            r.IsActive.ShouldBeTrue();
        } );

        using var d2 = TestHelper.CloneDomain( d, startTimer: false, pauseMilliseconds: 150 );
        d.IsDisposed.ShouldBeTrue( "Initial domain has been disposed." );

        d2.Read( TestHelper.Monitor, () =>
        {
            d2.TimeManager.IsRunning.ShouldBeFalse();
            d2.TimeManager.Reminders.Single().IsActive.ShouldBeTrue( "Not triggered by Load." );
        } );
        Thread.Sleep( 100 );
        d2.Read( TestHelper.Monitor, () =>
        {
            d2.TimeManager.IsRunning.ShouldBeFalse();
            d2.TimeManager.Reminders.Single().IsActive.ShouldBeTrue( "Waiting for TimeManager.Start()." );
            ElapsedFired.ShouldBeFalse();
        } );
        await d2.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            d2.TimeManager.IsRunning.ShouldBeFalse();
            d2.TimeManager.Reminders.Single().IsActive.ShouldBeTrue();
        } );

        ElapsedFired.ShouldBeFalse( "Still waiting." );

        await d2.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            d2.TimeManager.Start();
            d2.TimeManager.IsRunning.ShouldBeTrue();
            d2.TimeManager.Reminders.Single().IsActive.ShouldBeTrue( "Will be raised at the end of the transaction." );
        } );

        ElapsedFired.ShouldBeTrue( "TimeManager.IsRunning: reminder has fired." );

    }

    static bool ElapsedFired = false;
    static void OnElapsedFire( object sender, ObservableReminderEventArgs e )
    {
        ElapsedFired = true;
    }
}
