using System;
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
            ObservableDomain<TestObservableRootObject> d
                = new ObservableDomain<TestObservableRootObject>( client, TestHelper.Monitor );

            var transactionResult = d.Modify( () =>
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
            var d = new ObservableDomain<TestObservableRootObject>( new MemoryTransactionProviderClient(), TestHelper.Monitor );
            // Initial successful Modify
            d.Modify( () =>
            {
                d.Root.Prop1 = "Hello";
                d.Root.Prop2 = "World";
            } );

            // Raise exception during Write()
            var transactionResult = d.Modify( () =>
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
            var d = new ObservableDomain<TestObservableRootObject>( new MemoryTransactionProviderClient(), TestHelper.Monitor );
            // Initial successful Modify
            d.Modify( () =>
            {
                d.Root.Prop1 = "Hello";
                d.Root.Prop2 = "World";
            } );

            // Raise exception during Write()
            var transactionResult = d.Modify( () =>
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
            }
        }

        [SerializationVersion( 0 )]
        public class TestObservableRootObject : ObservableRootObject
        {
            public string Prop1 { get; set; }
            public string Prop2 { get; set; }

            public bool TestBehavior__ThrowOnWrite { get; set; }

            public TestObservableRootObject( ObservableDomain domain ) : base( domain )
            {
            }

            public TestObservableRootObject( IBinaryDeserializerContext d ) : base( d )
            {
                var r = d.StartReading();

                Prop1 = r.ReadNullableString();
                Prop2 = r.ReadNullableString();
            }

            void Write( BinarySerializer s )
            {
                s.WriteNullableString( Prop1 );
                s.WriteNullableString( Prop2 );

                if( TestBehavior__ThrowOnWrite ) throw new Exception( $"{nameof( TestBehavior__ThrowOnWrite )} is set. This is a test exception." );
            }
        }
    }
}
