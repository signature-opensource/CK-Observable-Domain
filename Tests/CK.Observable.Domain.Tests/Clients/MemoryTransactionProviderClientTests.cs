using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CK.BinarySerialization;
using CK.Core;
using FluentAssertions;
using NUnit.Framework;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Domain.Tests.Clients
{
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
            client.CurrentSerialNumber.Should().Be( -1, "No snapshot yet." );
            client.CompressionKind.Should().Be( CompressionKind.None );

            using var d = new ObservableDomain<TestObservableRootObject>( TestHelper.Monitor,
                                                                          nameof( an_initial_snapshot_is_always_taken_whatever_the_SkipTransactionCount_is_Async ),
                                                                          startTimer: false,
                                                                          client: client );
            client.CurrentSerialNumber.Should().Be( -1,
                "An initial snapshot will be taken at the start of the very first transaction (otherwise we could not protect the very fist Modify)." );

            await d.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                client.CurrentSerialNumber.Should().Be( 0, "First transaction seen by the client: an initial snapshot is ALWAYS taken." );

                d.Root.Prop1 = "Hello";
                d.Root.Prop2 = $"World n°1";
            } );

            d.TransactionSerialNumber.Should().Be( 1, "First real transaction number is 1." );
            client.CurrentSerialNumber.Should().Be( skipTransactionCount switch
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
                client.CurrentSerialNumber.Should().Be( skipTransactionCount switch
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
            d.TransactionSerialNumber.Should().Be( 0, "Just created: 0." );

            IReadOnlyList<ObservableEvent>? observableEvents = null;
            d.TransactionDone += ( d, ev ) => observableEvents = ev.Events;

            // Failed first transaction (will be rolled back).
            var transactionResult = await d.TryModifyAsync( TestHelper.Monitor, () =>
            {
                d.Root.Prop1 = "Hello";
                d.Root.Prop2 = "World";
                throw new Exception( "Exception during Modify(). This is a test exception." );
            }, considerRolledbackAsFailure: false );
            transactionResult.Success.Should().BeTrue( "This is the roll back." );
            Debug.Assert( transactionResult.RollbackedInfo != null );
            transactionResult.RollbackedInfo.Failure.Success.Should().BeFalse();
            transactionResult.RollbackedInfo.IsSafeRollback.Should().BeTrue( "Rollback to the initial start of the transaction is Safe." );
            transactionResult.RollbackedInfo.IsDangerousRollback.Should().BeFalse();
            observableEvents.Should().NotBeNull().And.BeEmpty( "No observable events (but TransactionDone has been called)." );
            observableEvents = null;
            d.Read( TestHelper.Monitor, () =>
            {
                d.TransactionSerialNumber.Should().Be( 0, "Back to creation." );
                d.Root.Prop1.Should().BeNull();
                d.Root.Prop2.Should().BeNull();
            } );

            // First real transaction (will be lost).
            await d.ModifyThrowAsync( TestHelper.Monitor, () => d.Root.Prop1 = "Hello" );
            d.TransactionSerialNumber.Should().Be( 1 );
            observableEvents.Should().NotBeNull().And.NotBeEmpty();
            observableEvents = null;

            // Second transaction (will also be lost).
            await d.ModifyThrowAsync( TestHelper.Monitor, () => d.Root.Prop2 = "World" );
            d.TransactionSerialNumber.Should().Be( 2 );
            observableEvents.Should().NotBeNull().And.NotBeEmpty();
            observableEvents = null;

            // Failing...
            transactionResult = await d.TryModifyAsync( TestHelper.Monitor, () =>
            {
                d.Root.Prop1 = "This will NOT be set! (since a rollback will be done)";
                throw new Exception( "Exception during Modify(). This is a test exception." );
            }, considerRolledbackAsFailure: false );

            transactionResult.Success.Should().BeTrue( "This is the roll back." );
            Debug.Assert( transactionResult.RollbackedInfo != null );
            transactionResult.RollbackedInfo.Failure.Success.Should().BeFalse();
            transactionResult.RollbackedInfo.IsSafeRollback.Should().BeFalse( "Rollback to an older state is..." );
            transactionResult.RollbackedInfo.IsDangerousRollback.Should().BeTrue( "Dangerous!" );
            d.Read( TestHelper.Monitor, () =>
            {
                d.TransactionSerialNumber.Should().Be( 0, "Back to creation." );
                d.Root.Prop1.Should().BeNull();
                d.Root.Prop2.Should().BeNull();
            } );
            observableEvents.Should().NotBeNull().And.BeEmpty( "No observable events (but TransactionDone has been called)." );
        }

        [Test]
        public async Task when_SkipTransactionCount_is_Minus_1_a_lodaded_domain_is_rolled_back_to_its_load_state_Async()
        {
            var initial = new ObservableDomain<TestObservableRootObject>( TestHelper.Monitor,
                                                                          nameof( when_SkipTransactionCount_is_Minus_1_a_lodaded_domain_is_rolled_back_to_its_load_state_Async ),
                                                                          startTimer: true,
                                                                          new ConcreteMemoryTransactionProviderClient() );
            initial.TransactionSerialNumber.Should().Be( 0, "Just created: 0." );
            await initial.ModifyThrowAsync( TestHelper.Monitor, () => initial.Root.Prop1 = "Loaded State." );
            initial.TransactionSerialNumber.Should().Be( 1, "Ready to be reloaded: 1." );

            var client = new ConcreteMemoryTransactionProviderClient() {  SkipTransactionCount = -1 };

            using var d = TestHelper.CloneDomain( initial, client: client );
            initial.IsDisposed.Should().BeTrue( "CloneDomain disposed the initial domain." );

            d.TransactionSerialNumber.Should().Be( 1, "Just loaded: 1." );
            // Failed transaction (will be rolled back to the loaded state).
            var transactionResult = await d.TryModifyAsync( TestHelper.Monitor, () =>
            {
                d.Root.Prop1 = "Hello";
                d.Root.Prop2 = "World";
                throw new Exception( "Exception during Modify(). This is a test exception." );
            }, considerRolledbackAsFailure: false );
            transactionResult.Success.Should().BeTrue( "This is the roll back." );
            Debug.Assert( transactionResult.RollbackedInfo != null );
            transactionResult.RollbackedInfo.Failure.Success.Should().BeFalse();
            transactionResult.RollbackedInfo.IsSafeRollback.Should().BeTrue( "Rollback to the initial start of the transaction is Safe." );
            transactionResult.RollbackedInfo.IsDangerousRollback.Should().BeFalse();
            d.Read( TestHelper.Monitor, () =>
            {
                d.TransactionSerialNumber.Should().Be( 1, "Back to loaded state." );
                d.Root.Prop1.Should().Be( "Loaded State." );
                d.Root.Prop2.Should().BeNull();
            } );

            // First real transaction (will be lost).
            await d.ModifyThrowAsync( TestHelper.Monitor, () => d.Root.Prop1 = "Hello" );
            d.TransactionSerialNumber.Should().Be( 2 );

            // Second transaction (will also be lost).
            await d.ModifyThrowAsync( TestHelper.Monitor, () => d.Root.Prop2 = "World" );
            d.TransactionSerialNumber.Should().Be( 3 );

            // Failing...
            transactionResult = await d.TryModifyAsync( TestHelper.Monitor, () =>
            {
                d.Root.Prop1 = "This will NOT be set! (since a rollback will be done)";
                throw new Exception( "Exception during Modify(). This is a test exception." );
            }, considerRolledbackAsFailure: false );

            transactionResult.Success.Should().BeTrue( "This is the roll back." );
            Debug.Assert( transactionResult.RollbackedInfo != null );
            transactionResult.RollbackedInfo.Failure.Success.Should().BeFalse();
            transactionResult.RollbackedInfo.IsSafeRollback.Should().BeFalse( "Rollback to an older state is..." );
            transactionResult.RollbackedInfo.IsDangerousRollback.Should().BeTrue( "Dangerous!" );
            d.Read( TestHelper.Monitor, () =>
            {
                d.TransactionSerialNumber.Should().Be( 1, "Dangerous roll back to loaded state." );
                d.Root.Prop1.Should().Be( "Loaded State." );
                d.Root.Prop2.Should().BeNull();
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
            events.Count.Should().Be( 4 );

            // Raise exception during Write()
            events = null;
            var transactionResult = await d.TryModifyAsync( TestHelper.Monitor, () =>
            {
                d.Root.Prop1 = "This will";
                d.Root.Prop2 = "be set even when Write fails";
                d.Root.TestBehavior__ThrowOnWrite = true;
            } );

            transactionResult.Errors.Should().BeEmpty( $"No errors happened during Modify()" );
            transactionResult.ClientError.Should().NotBeNull();

            events.Should().BeNull( "OnSuccessfulTransaction has not been raised." );
            transactionResult.IsCriticalError.Should().BeTrue( "Since Write fails, this is CRITICAL!" );
            d.Read( TestHelper.Monitor, () =>
            {
                d.Root.Prop1.Should().Be( "This will" );
                d.Root.Prop2.Should().Be( "be set even when Write fails" );
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
            observableEvents.Should().NotBeNull().And.NotBeEmpty();
            observableEvents = null;

            // Raise exception during Modify()
            var transactionResult = await d.TryModifyAsync( TestHelper.Monitor, () =>
            {
                d.Root.Prop1 = "This will";
                d.Root.Prop2 = "never be set";
                throw new Exception( "Exception during Modify(). This is a test exception." );
            } );
            observableEvents.Should().NotBeNull().And.BeEmpty( "No observable events (but TransactionDone has been called)." );

            transactionResult.Errors.Should().NotBeEmpty( $"Errors happened during Modify()" );
            transactionResult.ClientError.Should().BeNull( "No client errors happened" );
            transactionResult.IsCriticalError.Should().BeFalse( "The domain is in a valid state, this is not CRITICAL." );
            d.Read( TestHelper.Monitor, () =>
            {
                d.Root.Prop1.Should().Be( "Hello" );
                d.Root.Prop2.Should().Be( "World" );
                d.AllObjects.Count.Should().Be( 1 );
                d.AllRoots.Count.Should().Be( 1 );
                d.Root.Should().Be( (TestObservableRootObject)d.AllObjects.First() );
                d.Root.Should().Be( (TestObservableRootObject)d.AllRoots.First() );
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

                d2.TimeManager.IsRunning.Should().BeTrue();

                d2.Read( TestHelper.Monitor, () =>
                {
                    d2.Root.Prop1.Should().Be( "Hello" );
                    d2.Root.Prop2.Should().Be( "World" );
                    d2.AllObjects.Count.Should().Be( 1 );
                    d2.AllRoots.Count.Should().Be( 1 );
                } );
            }
        }

        [Test]
        public async Task WriteSnapshotTo_creates_valid_stream_for_Client_OnDomainCreated_Task_Async()
        {
            var client1 = new TestMemoryTransactionProviderClient();
            using var d1 = new ObservableDomain<TestObservableRootObject>( TestHelper.Monitor,
                                                                           nameof( WriteSnapshotTo_creates_valid_stream_for_Client_OnDomainCreated_Task_Async ),
                                                                           startTimer: false,
                                                                           client: client1 );
            // Initial successful Modify
            await d1.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                d1.Root.Prop1 = "Hello";
                d1.Root.Prop2 = "World";
            } );

            // Get snapshot stream
            using( MemoryStream domainStream = client1.CreateStreamFromSnapshot() )
            {
                // Create domain using a client with this snapshot
                using var d2 = new ObservableDomain<TestObservableRootObject>( TestHelper.Monitor,
                                                                               nameof( WriteSnapshotTo_creates_valid_stream_for_Client_OnDomainCreated_Task_Async ),
                                                                               startTimer: true,
                                                                               client: new TestMemoryTransactionProviderClient( domainStream ) );
                // The client.OnDomainCreated did its job: the domain has been loaded
                // from the initial domain stream.
                d2.Read( TestHelper.Monitor, () =>
                {
                    d2.Root.Prop1.Should().Be( "Hello" );
                    d2.Root.Prop2.Should().Be( "World" );
                    d2.AllObjects.Count.Should().Be( 1 );
                    d2.AllRoots.Count.Should().Be( 1 );
                } );
            }
        }

        [Test]
        public async Task ObservableDomain_loads_from_Client_when_given_both_Client_and_ctor_Stream_Async()
        {
            var client1 = new TestMemoryTransactionProviderClient();
            using var d1 = new ObservableDomain<TestObservableRootObject>( TestHelper.Monitor,
                                                                           nameof( ObservableDomain_loads_from_Client_when_given_both_Client_and_ctor_Stream_Async ),
                                                                           startTimer: true,
                                                                           client: client1 );
            // Initial successful Modify
            await d1.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                d1.Root.Prop1 = "Hello";
                d1.Root.Prop2 = "World";
            } );
            // Get snapshot stream
            using( var domainStream1 = client1.CreateStreamFromSnapshot() )
            {
                // Change domain a bit and get second stream
                await d1.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    d1.Root.Prop1 = "Hello 2";
                    d1.Root.Prop2 = "World 2";
                } );
                using( MemoryStream domainStream2 = client1.CreateStreamFromSnapshot() )
                {
                    // Create domain using BOTH ctor Stream (domainStream1)
                    // AND custom load from OnDomainCreated (domainStream2)
                    using var d2 = new ObservableDomain<TestObservableRootObject>( TestHelper.Monitor,
                                                                                   nameof( ObservableDomain_loads_from_Client_when_given_both_Client_and_ctor_Stream_Async ),
                                                                                   new TestMemoryTransactionProviderClient(domainStream2),
                                                                                   RewindableStream.FromStream( domainStream1 ) );

                    d2.Read( TestHelper.Monitor, () =>
                    {
                        d2.Root.Prop1.Should().Be( "Hello 2" );
                        d2.Root.Prop2.Should().Be( "World 2" );
                        d2.AllObjects.Count.Should().Be( 1 );
                        d2.AllRoots.Count.Should().Be( 1 );
                    } );
                }
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
                d.Root.Invoking( x => x.Destroy() )
                    .Should().Throw<InvalidOperationException>( "Roots still can't be disposed during regular operation" );
                throw new Exception( "Exception during Modify(). This is a test exception." );
            } );

            d.Read( TestHelper.Monitor, () =>
            {
                restoredObservableObject = d.Root;
                d.Root.Prop1.Should().Be( "Hello" );
                d.Root.Prop2.Should().Be( "World" );
            } );

            restoredObservableObject.Should().NotBe( initialObservableObject );
            initialObservableObject.IsDestroyed.Should().BeTrue( "Root was disposed following a reload" );
            restoredObservableObject.IsDestroyed.Should().BeFalse();
        }

    }
}
