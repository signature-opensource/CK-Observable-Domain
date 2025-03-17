using CK.BinarySerialization;
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
public class ObservableObjectLifetimeTests
{
    [Test]
    public void an_observable_must_be_created_in_the_context_of_a_transaction()
    {
        using( var d = new ObservableDomain(TestHelper.Monitor, "TEST", startTimer: true ) )
        {
            Action outOfTran = () => new Car( "" );
            outOfTran.ShouldThrow<InvalidOperationException>().Message.ShouldBe( "A transaction is required (Observable objects can be created only inside a transaction)." );
        }
    }

    [Test]
    public async Task an_observable_must_be_modified_in_the_context_of_a_transaction_Async()
    {
        using( var d = new ObservableDomain(TestHelper.Monitor, "TEST", startTimer: true ) )
        {
            await d.ModifyThrowAsync( TestHelper.Monitor, () => new Car( "Yes!" ) );

            Util.Invokable( () => d.AllObjects.Items.OfType<Car>().Single().TestSpeed = 3 )
                .ShouldThrow<InvalidOperationException>().Message.ShouldBe( "A transaction is required." );
        }
    }

    class JustAnObservableObject : ObservableObject
    {
        public int Speed { get; set; }

        void Export( ObjectExporter e )
        {
        }
    }

    [Test]
    public async Task Export_can_NOT_be_called_within_a_transaction_because_of_LockRecursionPolicy_NoRecursion_Async()
    {
        using( var d = new ObservableDomain(TestHelper.Monitor, "TEST", startTimer: true ) )
        {
            await d.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                Util.Invokable( () => d.ExportToString() )
                 .ShouldThrow<LockRecursionException>();
            } );
        }
    }

    [Test]
    public async Task Save_can_be_called_from_inside_a_transaction_Async()
    {
        using( var d = new ObservableDomain(TestHelper.Monitor, "TEST", startTimer: true ) )
        {
            await d.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                Util.Invokable( () => d.Save( TestHelper.Monitor, new MemoryStream() ) )
                 .ShouldNotThrow();
            } );
        }
    }

    [Test]
    public async Task Load_and_Save_can_be_called_from_inside_a_transaction_or_outside_any_transaction_Async()
    {
        using( var s = new MemoryStream() )
        using( var d = new ObservableDomain(TestHelper.Monitor, "TEST", startTimer: true ) )
        {
            await d.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                Util.Invokable( () => d.Save( TestHelper.Monitor, s ) ).ShouldNotThrow();
                s.Position = 0;
                Util.Invokable( () => d.Load( TestHelper.Monitor, RewindableStream.FromStream( s ) ) ).ShouldNotThrow();
            } );
            s.Position = 0;
            Util.Invokable( () => d.Load( TestHelper.Monitor, RewindableStream.FromStream( s ) ) ).ShouldNotThrow();
            s.Position = 0;
            Util.Invokable( () => d.Save( TestHelper.Monitor, s ) ).ShouldNotThrow();
        }
    }

    [Test]
    public async Task Modify_and_Read_reentrant_calls_are_detected_by_the_LockRecursionPolicy_NoRecursion_Async()
    {
        using( var d = new ObservableDomain(TestHelper.Monitor, "TEST", startTimer: true ) )
        {
            // ModifyAsync( Read ) throws.
            await d.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                Util.Invokable( () => d.Read( TestHelper.Monitor, () => { } ) )
                 .ShouldThrow<LockRecursionException>();
            } );

            // This is nonsense but this test that Read( ModifyAsync ) throws.
            await d.Read( TestHelper.Monitor, async () =>
            {
                await Util.Awaitable( () => d.ModifyThrowAsync( TestHelper.Monitor, null ) )
                        .ShouldThrowAsync<LockRecursionException>();
            } );

            // ModifyAsync( ModifyAsync ) throws.
            await d.ModifyThrowAsync( TestHelper.Monitor, async () =>
            {
                await Util.Awaitable( () => d.ModifyThrowAsync( TestHelper.Monitor, null ) )
                        .ShouldThrowAsync<LockRecursionException>();
            } );

            // Read( Read ) throws.
            d.Read( TestHelper.Monitor, () =>
            {
                Util.Invokable( () => d.Read( TestHelper.Monitor, () => { } ) )
                 .ShouldThrow<LockRecursionException>();
            } );
        }
    }

    [Test]
    public async Task ObservableObject_exposes_Disposed_event_Async()
    {
        OnDestroyCalled = false;
        using( var d = new ObservableDomain(TestHelper.Monitor, "TEST", startTimer: true ) )
        {
            await d.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                var c = new Car( "Titine" );
                d.AllObjects.Items.ShouldHaveSingleItem().ShouldBeSameAs( c );
                d.AllObjects.Items.Count.ShouldBe( 1 );
                c.Destroyed += OnDestroy;
                c.Destroy();
                OnDestroyCalled.ShouldBeTrue();
                d.AllObjects.Items.ShouldBeEmpty();
            } );
        }
    }

    static bool OnDestroyCalled;

    static void OnDestroy( object sender, ObservableDomainEventArgs e ) => OnDestroyCalled = true;
}
