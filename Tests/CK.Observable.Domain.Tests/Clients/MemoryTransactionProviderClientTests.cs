using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CK.BinarySerialization;
using FluentAssertions;
using NUnit.Framework;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Domain.Tests.Clients
{
    public partial class MemoryTransactionProviderClientTests
    {

        [Test]
        public async Task Modify_creates_snapshot_Async()
        {
            var client = new ConcreteMemoryTransactionProviderClient();
            using var d = new ObservableDomain<TestObservableRootObject>( TestHelper.Monitor,
                                                                          nameof( Modify_creates_snapshot_Async ),
                                                                          startTimer: false,
                                                                          client: client );

            var transactionResult = await d.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                d.Root.Prop1 = "Hello";
                d.Root.Prop2 = "World";
            } );

            transactionResult.Errors.Should().BeEmpty();
            client.CurrentSerialNumber.Should().NotBe( -1, "There should have been a snapshot taken." );
            client.CurrentSerialNumber.Should().NotBe( int.MaxValue, "There should not have been a restore from stream." );
            client.CompressionKind.Should().Be( CompressionKind.None );
            client.CurrentTimeUtc.Should().BeWithin( TimeSpan.FromSeconds( 2 ) );
        }

        [Test]
        public async Task Exception_during_Write_adds_ClientError_Async()
        {
            using var d = new ObservableDomain<TestObservableRootObject>( TestHelper.Monitor,
                                                                          nameof( Exception_during_Write_adds_ClientError_Async ),
                                                                          startTimer: true,
                                                                          client: new ConcreteMemoryTransactionProviderClient() );

            IReadOnlyList<ObservableEvent>? events = null;
            d.OnSuccessfulTransaction += ( d, ev ) => events = ev.Events;

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
            var transactionResult = await d.ModifyNoThrowAsync( TestHelper.Monitor, () =>
            {
                d.Root.Prop1 = "This will";
                d.Root.Prop2 = "be set even when Write fails";
                d.Root.TestBehavior__ThrowOnWrite = true;
            } );

            transactionResult.Errors.Should().BeEmpty( $"No errors happened during Modify()" );

            events.Should().BeNull( "OnSuccessfulTransaction has not been raised." );
            transactionResult.ClientError.Should().NotBeNull();
            transactionResult.IsCriticalError.Should().BeTrue( "Since Write fails, this is CRITICAL!" );
            using( d.AcquireReadLock() )
            {
                d.Root.Prop1.Should().Be( "This will" );
                d.Root.Prop2.Should().Be( "be set even when Write fails" );
            }
        }

        [Test]
        public async Task Exception_during_Modify_rolls_ObservableDomain_back_when_SkipTransactionCount_is_0_Async()
        {
            using var d = new ObservableDomain<TestObservableRootObject>( TestHelper.Monitor,
                                                                          nameof( Exception_during_Modify_rolls_ObservableDomain_back_when_SkipTransactionCount_is_0_Async ),
                                                                          startTimer: true,
                                                                          client: new ConcreteMemoryTransactionProviderClient() );
            IReadOnlyList<ObservableEvent>? events = null;
            d.OnSuccessfulTransaction += ( d, ev ) => events = ev.Events;

            // Initial successful Modify
            await d.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                d.Root.Prop1 = "Hello";
                d.Root.Prop2 = "World";
            } );

            Debug.Assert( events != null );

            // Raise exception during Modify()
            events = null;
            var transactionResult = await d.ModifyNoThrowAsync( TestHelper.Monitor, () =>
            {
                d.Root.Prop1 = "This will";
                d.Root.Prop2 = "never be set";
                throw new Exception( "Exception during Modify(). This is a test exception." );
            } );

            transactionResult.Errors.Should().NotBeEmpty( $"Errors happened during Modify()" );
            events.Should().BeNull();
            transactionResult.ClientError.Should().BeNull( "No client errors happened" );
            transactionResult.IsCriticalError.Should().BeFalse( "The domain is in a valid state, this is not CRITICAL." );
            using( d.AcquireReadLock() )
            {
                d.Root.Prop1.Should().Be( "Hello" );
                d.Root.Prop2.Should().Be( "World" );
                d.AllObjects.Count.Should().Be( 1 );
                d.AllRoots.Count.Should().Be( 1 );
                d.Root.Should().Be( (TestObservableRootObject)d.AllObjects.First() );
                d.Root.Should().Be( (TestObservableRootObject)d.AllRoots.First() );
            }
        }

        [Test]
        public async Task Exception_during_Modify_rolls_back_when_SkipTransactionCount_is_Minus_1_Async()
        {
            var client = new ConcreteMemoryTransactionProviderClient() { SkipTransactionCount = -1 };

            using var d = new ObservableDomain<TestObservableRootObject>( TestHelper.Monitor, nameof( Exception_during_Modify_rolls_back_when_SkipTransactionCount_is_Minus_1_Async ), startTimer: true, client );
            IReadOnlyList<ObservableEvent>? events = null;
            d.OnSuccessfulTransaction += ( d, ev ) => events = ev.Events;

            // Initial successful Modify
            await d.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                d.Root.Prop1 = "Hello";
                d.Root.Prop2 = "World";
            } );
            d.TransactionSerialNumber.Should().Be( 1 );

            await d.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                d.Root.Prop1 = "Will be erased";
                d.Root.Prop2 = "Will be erased";
            } );
            d.TransactionSerialNumber.Should().Be( 2 );

            Debug.Assert( events != null );

            // Raise exception during Modify()
            events = null;
            var transactionResult = await d.ModifyNoThrowAsync( TestHelper.Monitor, () =>
            {
                d.Root.Prop1 = "This will";
                d.Root.Prop2 = "NOT be set! (since a rollback will be done)";
                throw new Exception( "Exception during Modify(). This is a test exception." );
                d.Root.Prop1 = "NOT";
                d.Root.Prop2 = "NOT";
            }, considerRolledbackAsFailure: false );

            transactionResult.Success.Should().BeTrue( "This is the roll back." );
            Debug.Assert( transactionResult.RollbackedInfo != null );
            transactionResult.RollbackedInfo.Failure.Success.Should().BeFalse();
            transactionResult.RollbackedInfo.IsDangerousRollback.Should().BeTrue();

            d.TransactionSerialNumber.Should().Be( 1, "Back to the initial transaction." );

            using( d.AcquireReadLock() )
            {
                d.Root.Prop1.Should().Be( "Hello" );
                d.Root.Prop2.Should().Be( "World" );
                d.AllObjects.Count.Should().Be( 1 );
                d.AllRoots.Count.Should().Be( 1 );
                d.Root.Should().Be( (TestObservableRootObject)d.AllObjects.First() );
                d.Root.Should().Be( (TestObservableRootObject)d.AllRoots.First() );
            }
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

                using( d2.AcquireReadLock() )
                {
                    d2.Root.Prop1.Should().Be( "Hello" );
                    d2.Root.Prop2.Should().Be( "World" );
                    d2.AllObjects.Count.Should().Be( 1 );
                    d2.AllRoots.Count.Should().Be( 1 );
                }
            }
        }

        [Test]
        public async Task WriteSnapshotTo_creates_valid_stream_for_Client_OnDomainCreated_Task()
        {
            var client1 = new TestMemoryTransactionProviderClient();
            using var d1 = new ObservableDomain<TestObservableRootObject>( TestHelper.Monitor,
                                                                           nameof( WriteSnapshotTo_creates_valid_stream_for_Client_OnDomainCreated_Task ),
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
                                                                               nameof( WriteSnapshotTo_creates_valid_stream_for_Client_OnDomainCreated_Task ),
                                                                               startTimer: true,
                                                                               client: new TestMemoryTransactionProviderClient( domainStream ) );

                using( d2.AcquireReadLock() )
                {
                    d2.Root.Prop1.Should().Be( "Hello" );
                    d2.Root.Prop2.Should().Be( "World" );
                    d2.AllObjects.Count.Should().Be( 1 );
                    d2.AllRoots.Count.Should().Be( 1 );
                }
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

                    using( d2.AcquireReadLock() )
                    {
                        d2.Root.Prop1.Should().Be( "Hello 2" );
                        d2.Root.Prop2.Should().Be( "World 2" );
                        d2.AllObjects.Count.Should().Be( 1 );
                        d2.AllRoots.Count.Should().Be( 1 );
                    }
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
            await d.ModifyNoThrowAsync( TestHelper.Monitor, () =>
            {
                d.Root.Prop1 = "This will";
                d.Root.Prop2 = "never be set";
                d.Root.Invoking( x => x.Destroy() )
                    .Should().Throw<InvalidOperationException>( "Roots still can't be disposed during regular operation" );
                throw new Exception( "Exception during Modify(). This is a test exception." );
            } );

            using( d.AcquireReadLock() )
            {
                restoredObservableObject = d.Root;
                d.Root.Prop1.Should().Be( "Hello" );
                d.Root.Prop2.Should().Be( "World" );
            }

            restoredObservableObject.Should().NotBe( initialObservableObject );
            initialObservableObject.IsDestroyed.Should().BeTrue( "Root was disposed following a reload" );
            restoredObservableObject.IsDestroyed.Should().BeFalse();
        }

    }
}
