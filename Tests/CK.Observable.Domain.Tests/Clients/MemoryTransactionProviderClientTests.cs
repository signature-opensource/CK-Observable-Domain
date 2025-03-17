using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CK.BinarySerialization;
using CK.Core;
using Shouldly;
using NUnit.Framework;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Domain.Tests.Clients;

public partial class MemoryTransactionProviderClientTests
{

    [TestCase( -1 )]
    [TestCase( 0 )]
    [TestCase( 1 )]
    [TestCase( 2 )]
    [TestCase( 3 )]
    [TestCase( 4 )]
    public async Task an_initial_snapshot_is_always_taken_whatever_the_SkipTransactionCount_is_Async( int skipTransactionCount )
    {
        var client = new ConcreteMemoryTransactionProviderClient() { SkipTransactionCount = skipTransactionCount };
        client.CurrentSerialNumber.ShouldBe( -1, "No snapshot yet." );
        client.CompressionKind.ShouldBe( CompressionKind.None );

        using var d = new ObservableDomain<TestObservableRootObject>( TestHelper.Monitor,
                                                                      nameof( an_initial_snapshot_is_always_taken_whatever_the_SkipTransactionCount_is_Async ),
                                                                      startTimer: false,
                                                                      client: client );
        client.CurrentSerialNumber.ShouldBe( -1,
            "An initial snapshot will be taken at the start of the very first transaction (otherwise we could not protect the very fist Modify)." );

        await d.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            client.CurrentSerialNumber.ShouldBe( 0, "First transaction seen by the client: an initial snapshot is ALWAYS taken." );

            d.Root.Prop1 = "Hello";
            d.Root.Prop2 = $"World n°1";
        } );

        d.TransactionSerialNumber.ShouldBe( 1, "First real transaction number is 1." );
        client.CurrentSerialNumber.ShouldBe( skipTransactionCount switch
                                                                     {
                                                                         0 => 1, // Only the 0 SkipTransactionCount saves the first real transaction.
                                                                         _ => 0  // The other ones skip it.
                                                                     } );
        for( int i = 2; i < 15; ++i )
        {
            await d.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                d.Root.Prop1 = "Hello";
                d.Root.Prop2 = $"World n°{i}";
            } );
            client.CurrentSerialNumber.ShouldBe( skipTransactionCount switch
                                                                         { -1 => 0, // Only the initial state is kept.
                                                                            0 => i, // When SkipTransactionCount is 0, all snapshots are taken.
                                                                            _ => i - (i % (skipTransactionCount + 1)) // Skipped as said.
                                                                         },
                                                    "The 0 is always here, but then this depends on the SkipTransactionCount." );
        }
    }

    [Test]
    public async Task when_SkipTransactionCount_is_Minus_1_a_new_domain_is_rolled_back_to_its_creation_state_Async()
    {
        var client = new ConcreteMemoryTransactionProviderClient() { SkipTransactionCount = -1 };

        using var d = new ObservableDomain<TestObservableRootObject>( TestHelper.Monitor, nameof( when_SkipTransactionCount_is_Minus_1_a_new_domain_is_rolled_back_to_its_creation_state_Async ), startTimer: true, client );
        d.TransactionSerialNumber.ShouldBe( 0, "Just created: 0." );

        IReadOnlyList<ObservableEvent>? observableEvents = null;
        d.TransactionDone += ( d, ev ) => observableEvents = ev.Events;

        // Failed first transaction (will be rolled back).
        var transactionResult = await d.TryModifyAsync( TestHelper.Monitor, () =>
        {
            d.Root.Prop1 = "Hello";
            d.Root.Prop2 = "World";
            throw new Exception( "Exception during Modify(). This is a test exception." );
        }, considerRolledbackAsFailure: false );
        transactionResult.Success.ShouldBeTrue( "This is the roll back." );
        Debug.Assert( transactionResult.RollbackedInfo != null );
        transactionResult.RollbackedInfo.Failure.Success.ShouldBeFalse();
        transactionResult.RollbackedInfo.IsSafeRollback.ShouldBeTrue( "Rollback to the initial start of the transaction is Safe." );
        transactionResult.RollbackedInfo.IsDangerousRollback.ShouldBeFalse();
        observableEvents.ShouldNotBeNull().ShouldBeEmpty( "No observable events (but TransactionDone has been called)." );
        observableEvents = null;
        d.Read( TestHelper.Monitor, () =>
        {
            d.TransactionSerialNumber.ShouldBe( 0, "Back to creation." );
            d.Root.Prop1.ShouldBeNull();
            d.Root.Prop2.ShouldBeNull();
        } );

        // First real transaction (will be lost).
        await d.ModifyThrowAsync( TestHelper.Monitor, () => d.Root.Prop1 = "Hello" );
        d.TransactionSerialNumber.ShouldBe( 1 );
        observableEvents.ShouldNotBeNull().ShouldNotBeEmpty();
        observableEvents = null;

        // Second transaction (will also be lost).
        await d.ModifyThrowAsync( TestHelper.Monitor, () => d.Root.Prop2 = "World" );
        d.TransactionSerialNumber.ShouldBe( 2 );
        observableEvents.ShouldNotBeNull().ShouldNotBeEmpty();
        observableEvents = null;

        // Failing...
        transactionResult = await d.TryModifyAsync( TestHelper.Monitor, () =>
        {
            d.Root.Prop1 = "This will NOT be set! (since a rollback will be done)";
            throw new Exception( "Exception during Modify(). This is a test exception." );
        }, considerRolledbackAsFailure: false );

        transactionResult.Success.ShouldBeTrue( "This is the roll back." );
        Debug.Assert( transactionResult.RollbackedInfo != null );
        transactionResult.RollbackedInfo.Failure.Success.ShouldBeFalse();
        transactionResult.RollbackedInfo.IsSafeRollback.ShouldBeFalse( "Rollback to an older state is..." );
        transactionResult.RollbackedInfo.IsDangerousRollback.ShouldBeTrue( "Dangerous!" );
        d.Read( TestHelper.Monitor, () =>
        {
            d.TransactionSerialNumber.ShouldBe( 0, "Back to creation." );
            d.Root.Prop1.ShouldBeNull();
            d.Root.Prop2.ShouldBeNull();
        } );
        observableEvents.ShouldNotBeNull().ShouldBeEmpty( "No observable events (but TransactionDone has been called)." );
    }

    [Test]
    public async Task when_SkipTransactionCount_is_Minus_1_a_lodaded_domain_is_rolled_back_to_its_load_state_Async()
    {
        var initial = new ObservableDomain<TestObservableRootObject>( TestHelper.Monitor,
                                                                      nameof( when_SkipTransactionCount_is_Minus_1_a_lodaded_domain_is_rolled_back_to_its_load_state_Async ),
                                                                      startTimer: true,
                                                                      new ConcreteMemoryTransactionProviderClient() );
        initial.TransactionSerialNumber.ShouldBe( 0, "Just created: 0." );
        await initial.ModifyThrowAsync( TestHelper.Monitor, () => initial.Root.Prop1 = "Loaded State." );
        initial.TransactionSerialNumber.ShouldBe( 1, "Ready to be reloaded: 1." );

        var client = new ConcreteMemoryTransactionProviderClient() {  SkipTransactionCount = -1 };

        using var d = TestHelper.CloneDomain( initial, client: client );
        initial.IsDisposed.ShouldBeTrue( "CloneDomain disposed the initial domain." );

        d.TransactionSerialNumber.ShouldBe( 1, "Just loaded: 1." );
        // Failed transaction (will be rolled back to the loaded state).
        var transactionResult = await d.TryModifyAsync( TestHelper.Monitor, () =>
        {
            d.Root.Prop1 = "Hello";
            d.Root.Prop2 = "World";
            throw new Exception( "Exception during Modify(). This is a test exception." );
        }, considerRolledbackAsFailure: false );
        transactionResult.Success.ShouldBeTrue( "This is the roll back." );
        Debug.Assert( transactionResult.RollbackedInfo != null );
        transactionResult.RollbackedInfo.Failure.Success.ShouldBeFalse();
        transactionResult.RollbackedInfo.IsSafeRollback.ShouldBeTrue( "Rollback to the initial start of the transaction is Safe." );
        transactionResult.RollbackedInfo.IsDangerousRollback.ShouldBeFalse();
        d.Read( TestHelper.Monitor, () =>
        {
            d.TransactionSerialNumber.ShouldBe( 1, "Back to loaded state." );
            d.Root.Prop1.ShouldBe( "Loaded State." );
            d.Root.Prop2.ShouldBeNull();
        } );

        // First real transaction (will be lost).
        await d.ModifyThrowAsync( TestHelper.Monitor, () => d.Root.Prop1 = "Hello" );
        d.TransactionSerialNumber.ShouldBe( 2 );

        // Second transaction (will also be lost).
        await d.ModifyThrowAsync( TestHelper.Monitor, () => d.Root.Prop2 = "World" );
        d.TransactionSerialNumber.ShouldBe( 3 );

        // Failing...
        transactionResult = await d.TryModifyAsync( TestHelper.Monitor, () =>
        {
            d.Root.Prop1 = "This will NOT be set! (since a rollback will be done)";
            throw new Exception( "Exception during Modify(). This is a test exception." );
        }, considerRolledbackAsFailure: false );

        transactionResult.Success.ShouldBeTrue( "This is the roll back." );
        Debug.Assert( transactionResult.RollbackedInfo != null );
        transactionResult.RollbackedInfo.Failure.Success.ShouldBeFalse();
        transactionResult.RollbackedInfo.IsSafeRollback.ShouldBeFalse( "Rollback to an older state is..." );
        transactionResult.RollbackedInfo.IsDangerousRollback.ShouldBeTrue( "Dangerous!" );
        d.Read( TestHelper.Monitor, () =>
        {
            d.TransactionSerialNumber.ShouldBe( 1, "Dangerous roll back to loaded state." );
            d.Root.Prop1.ShouldBe( "Loaded State." );
            d.Root.Prop2.ShouldBeNull();
        } );
    }

    [Test]
    public async Task Exception_during_Write_adds_ClientError_Async()
    {
        using var d = new ObservableDomain<TestObservableRootObject>( TestHelper.Monitor,
                                                                      nameof( Exception_during_Write_adds_ClientError_Async ),
                                                                      startTimer: true,
                                                                      client: new ConcreteMemoryTransactionProviderClient() );

        IReadOnlyList<ObservableEvent>? events = null;
        d.TransactionDone += ( d, ev ) => events = ev.Events;

        // Initial successful Modify
        await d.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            d.Root.Prop1 = "Hello";
            d.Root.Prop2 = "World";
        } );
        Debug.Assert( events != null );
        events.Count.ShouldBe( 4 );

        // Raise exception during Write()
        events = null;
        var transactionResult = await d.TryModifyAsync( TestHelper.Monitor, () =>
        {
            d.Root.Prop1 = "This will";
            d.Root.Prop2 = "be set even when Write fails";
            d.Root.TestBehavior__ThrowOnWrite = true;
        } );

        transactionResult.Errors.ShouldBeEmpty( $"No errors happened during Modify()" );
        transactionResult.ClientError.ShouldNotBeNull();

        events.ShouldBeNull( "OnSuccessfulTransaction has not been raised." );
        transactionResult.IsCriticalError.ShouldBeTrue( "Since Write fails, this is CRITICAL!" );
        d.Read( TestHelper.Monitor, () =>
        {
            d.Root.Prop1.ShouldBe( "This will" );
            d.Root.Prop2.ShouldBe( "be set even when Write fails" );
        } );
    }

    [Test]
    public async Task Exception_during_Modify_rolls_ObservableDomain_back_when_SkipTransactionCount_is_0_Async()
    {
        using var d = new ObservableDomain<TestObservableRootObject>( TestHelper.Monitor,
                                                                      nameof( Exception_during_Modify_rolls_ObservableDomain_back_when_SkipTransactionCount_is_0_Async ),
                                                                      startTimer: true,
                                                                      client: new ConcreteMemoryTransactionProviderClient() );
        IReadOnlyList<ObservableEvent>? observableEvents = null;
        d.TransactionDone += ( d, ev ) => observableEvents = ev.Events;

        // Initial successful Modify
        await d.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            d.Root.Prop1 = "Hello";
            d.Root.Prop2 = "World";
        } );
        observableEvents.ShouldNotBeNull().ShouldNotBeEmpty();
        observableEvents = null;

        // Raise exception during Modify()
        var transactionResult = await d.TryModifyAsync( TestHelper.Monitor, () =>
        {
            d.Root.Prop1 = "This will";
            d.Root.Prop2 = "never be set";
            throw new Exception( "Exception during Modify(). This is a test exception." );
        } );
        observableEvents.ShouldNotBeNull().ShouldBeEmpty( "No observable events (but TransactionDone has been called)." );

        transactionResult.Errors.ShouldNotBeEmpty( $"Errors happened during Modify()" );
        transactionResult.ClientError.ShouldBeNull( "No client errors happened" );
        transactionResult.IsCriticalError.ShouldBeFalse( "The domain is in a valid state, this is not CRITICAL." );
        d.Read( TestHelper.Monitor, () =>
        {
            d.Root.Prop1.ShouldBe( "Hello" );
            d.Root.Prop2.ShouldBe( "World" );
            d.AllObjects.Count.ShouldBe( 1 );
            d.AllRoots.Count.ShouldBe( 1 );
            d.Root.ShouldBe( (TestObservableRootObject)d.AllObjects.Items.First() );
            d.Root.ShouldBe( (TestObservableRootObject)d.AllRoots.First() );
        } );
    }

    [Test]
    public async Task WriteSnapshotTo_creates_valid_stream_for_ObservableDomain_ctor_Async()
    {
        var client1 = new TestMemoryTransactionProviderClient();
        using var d1 = new ObservableDomain<TestObservableRootObject>(TestHelper.Monitor, nameof(WriteSnapshotTo_creates_valid_stream_for_ObservableDomain_ctor_Async), startTimer: true, client: client1 );
        // Initial successful Modify
        await d1.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            d1.Root.Prop1 = "Hello";
            d1.Root.Prop2 = "World";
        } );
        // Get snapshot stream
        using( var domainStream = client1.CreateStreamFromSnapshot() )
        {
            // Create domain from that snapshot
            using var d2 = new ObservableDomain<TestObservableRootObject>( TestHelper.Monitor,
                                                                           nameof( WriteSnapshotTo_creates_valid_stream_for_ObservableDomain_ctor_Async ),
                                                                           new ConcreteMemoryTransactionProviderClient(),
                                                                           RewindableStream.FromStream( domainStream ) );

            d2.TimeManager.IsRunning.ShouldBeTrue();

            d2.Read( TestHelper.Monitor, () =>
            {
                d2.Root.Prop1.ShouldBe( "Hello" );
                d2.Root.Prop2.ShouldBe( "World" );
                d2.AllObjects.Count.ShouldBe( 1 );
                d2.AllRoots.Count.ShouldBe( 1 );
            } );
        }
    }

    [Test]
    public async Task Rollback_disposes_replaced_ObservableObjects_Async()
    {
        using var d = new ObservableDomain<TestObservableRootObject>( TestHelper.Monitor,
                                                                      nameof( Rollback_disposes_replaced_ObservableObjects_Async ),
                                                                      startTimer: true,
                                                                      client: new ConcreteMemoryTransactionProviderClient() );
        // Initial successful Modify
        TestObservableRootObject initialObservableObject = null!;
        TestObservableRootObject restoredObservableObject = null!;
        await d.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            initialObservableObject = d.Root;
            d.Root.Prop1 = "Hello";
            d.Root.Prop2 = "World";
        } );
        Debug.Assert( initialObservableObject != null );

        // Raise exception during Write()
        await d.TryModifyAsync( TestHelper.Monitor, () =>
        {
            d.Root.Prop1 = "This will";
            d.Root.Prop2 = "never be set";
            Util.Invokable( () => d.Root.Destroy() )
                .ShouldThrow<InvalidOperationException>( "Roots still can't be disposed during regular operation" );
            throw new Exception( "Exception during Modify(). This is a test exception." );
        } );

        d.Read( TestHelper.Monitor, () =>
        {
            restoredObservableObject = d.Root;
            d.Root.Prop1.ShouldBe( "Hello" );
            d.Root.Prop2.ShouldBe( "World" );
        } );

        restoredObservableObject.ShouldNotBe( initialObservableObject );
        initialObservableObject.IsDestroyed.ShouldBeTrue( "Root was disposed following a reload" );
        restoredObservableObject.IsDestroyed.ShouldBeFalse();
    }

}
