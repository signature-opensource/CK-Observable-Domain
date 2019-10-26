using System;
using System.Diagnostics;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Domain.Tests.Clients
{
    public class MemoryTransactionProviderClientTests
    {
        [Test]
        public void Modify_creates_snapshot()
        {
            var client = new MemoryTransactionProviderClient();
            var d = new ObservableDomain<TestObservableRootObject>(TestHelper.Monitor, "TEST", client);

            var transactionResult = d.Modify( TestHelper.Monitor, () =>
            {
                d.Root.Prop1 = "Hello";
                d.Root.Prop2 = "World";
            } );

            transactionResult.Errors.Should().BeEmpty();
            transactionResult.Events.Should().NotBeEmpty();
            client.CurrentSerialNumber.Should().NotBe( -1, "There should have been a snapshot taken." );
            client.CurrentSerialNumber.Should().NotBe( int.MaxValue, "There should not have been a restore from stream." );
            client.HasSnapshot.Should().BeTrue();
            client.CurrentTimeUtc.Should().BeWithin( TimeSpan.FromSeconds( 2 ) );
        }


        [Test]
        public void Exception_during_Write_adds_ClientError()
        {
            var d = new ObservableDomain<TestObservableRootObject>(TestHelper.Monitor, "TEST", new MemoryTransactionProviderClient());
            // Initial successful Modify
            d.Modify( TestHelper.Monitor, () =>
            {
                d.Root.Prop1 = "Hello";
                d.Root.Prop2 = "World";
            } );

            // Raise exception during Write()
            var transactionResult = d.Modify( TestHelper.Monitor, () =>
            {
                d.Root.Prop1 = "This will";
                d.Root.Prop2 = "be set even when Write fails";
                d.Root.TestBehavior__ThrowOnWrite = true;
            } );

            transactionResult.Errors.Should().BeEmpty( $"No errors happened during Modify()" );
            transactionResult.Events.Should().NotBeEmpty();
            transactionResult.ClientError.Should().NotBeNull();
            using( d.AcquireReadLock() )
            {
                d.Root.Prop1.Should().Be( "This will" );
                d.Root.Prop2.Should().Be( "be set even when Write fails" );
            }
        }


        [Test]
        public void Exception_during_Modify_rolls_ObservableDomain_back()
        {
            var d = new ObservableDomain<TestObservableRootObject>(TestHelper.Monitor, "TEST", new MemoryTransactionProviderClient());
            // Initial successful Modify
            d.Modify( TestHelper.Monitor, () =>
            {
                d.Root.Prop1 = "Hello";
                d.Root.Prop2 = "World";
            } );

            // Raise exception during Write()
            var transactionResult = d.Modify( TestHelper.Monitor, () =>
            {
                d.Root.Prop1 = "This will";
                d.Root.Prop2 = "never be set";
                throw new Exception( "Exception during Modify(). This is a test exception." );
            } );

            transactionResult.Errors.Should().NotBeEmpty( $"Errors happened during Modify()" );
            transactionResult.Events.Should().BeEmpty();
            transactionResult.ClientError.Should().BeNull( "No client errors happened" );
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
        public void WriteSnapshotTo_creates_valid_stream_for_ObservableDomain_ctor()
        {
            var client1 = new TestMemoryTransactionProviderClient();
            var d1 = new ObservableDomain<TestObservableRootObject>(TestHelper.Monitor, "TEST", client1);
            // Initial successful Modify
            d1.Modify( TestHelper.Monitor, () =>
            {
                d1.Root.Prop1 = "Hello";
                d1.Root.Prop2 = "World";
            } );
            // Get snapshot stream
            using( var domainStream = client1.CreateStreamFromSnapshot() )
            {
                // Create domain from that snapshot
                var d2 = new ObservableDomain<TestObservableRootObject>(
                    TestHelper.Monitor,
                    "TEST",
                    new MemoryTransactionProviderClient(),
                    domainStream
                    );

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
        public void WriteSnapshotTo_creates_valid_stream_for_Client_OnDomainCreated()
        {
            var client1 = new TestMemoryTransactionProviderClient();
            var d1 = new ObservableDomain<TestObservableRootObject>(TestHelper.Monitor, "TEST", client1);
            // Initial successful Modify
            d1.Modify( TestHelper.Monitor, () =>
            {
                d1.Root.Prop1 = "Hello";
                d1.Root.Prop2 = "World";
            } );
            // Get snapshot stream
            using( var domainStream = client1.CreateStreamFromSnapshot() )
            {
                // Create domain using a client with this snapshot
                var d2 = new ObservableDomain<TestObservableRootObject>(
                    TestHelper.Monitor
,
                    "TEST",
                    new TestMemoryTransactionProviderClient(domainStream));

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
        public void ObservableDomain_loads_from_Client_when_given_both_Client_and_ctor_Stream()
        {
            var client1 = new TestMemoryTransactionProviderClient();
            var d1 = new ObservableDomain<TestObservableRootObject>(TestHelper.Monitor, "TEST", client1);
            // Initial successful Modify
            d1.Modify( TestHelper.Monitor, () =>
            {
                d1.Root.Prop1 = "Hello";
                d1.Root.Prop2 = "World";
            } );
            // Get snapshot stream
            using( var domainStream1 = client1.CreateStreamFromSnapshot() )
            {
                // Change domain a bit and get second stream
                d1.Modify( TestHelper.Monitor, () =>
                {
                    d1.Root.Prop1 = "Hello 2";
                    d1.Root.Prop2 = "World 2";
                } );
                using( var domainStream2 = client1.CreateStreamFromSnapshot() )
                {
                    // Create domain using BOTH ctor Stream (domainStream1)
                    // AND custom load from OnDomainCreated (domainStream2)
                    var d2 = new ObservableDomain<TestObservableRootObject>(
                        TestHelper.Monitor,
                        "TEST",
                        new TestMemoryTransactionProviderClient(domainStream2),
                        domainStream1
                    );

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
        public void Rollback_disposes_replaced_ObservableObjects()
        {
            var d = new ObservableDomain<TestObservableRootObject>(TestHelper.Monitor, "TEST", new MemoryTransactionProviderClient());
            // Initial successful Modify
            TestObservableRootObject initialObservableObject = null;
            TestObservableRootObject restoredObservableObject = null;
            d.Modify( TestHelper.Monitor, () =>
            {
                initialObservableObject = d.Root;
                d.Root.Prop1 = "Hello";
                d.Root.Prop2 = "World";
            } );
            Debug.Assert( initialObservableObject != null );

            // Raise exception during Write()
            d.Modify( TestHelper.Monitor, () =>
            {
                d.Root.Prop1 = "This will";
                d.Root.Prop2 = "never be set";
                d.Root.Invoking( x => x.Dispose() )
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
            initialObservableObject.IsDisposed.Should().BeTrue( "Root was disposed following a reload" );
            restoredObservableObject.IsDisposed.Should().BeFalse();
        }
    }
}
