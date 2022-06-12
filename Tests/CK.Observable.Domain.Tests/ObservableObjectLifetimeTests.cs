using CK.BinarySerialization;
using CK.Core;
using CK.Observable.Domain.Tests.Sample;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Domain.Tests
{

    [TestFixture]
    public class ObservableObjectLifetimeTests
    {
        [Test]
        public void an_observable_must_be_created_in_the_context_of_a_transaction()
        {
            using( var d = new ObservableDomain(TestHelper.Monitor, "TEST", startTimer: true ) )
            {
                Action outOfTran = () => new Car( "" );
                outOfTran.Should().Throw<InvalidOperationException>().WithMessage( "A transaction is required (Observable objects can be created only inside a transaction)." );
            }
        }

        [Test]
        public async Task an_observable_must_be_modified_in_the_context_of_a_transaction_Async()
        {
            using( var d = new ObservableDomain(TestHelper.Monitor, "TEST", startTimer: true ) )
            {
                await d.ModifyThrowAsync( TestHelper.Monitor, () => new Car( "Yes!" ) );

                FluentActions.Invoking( () => d.AllObjects.OfType<Car>().Single().TestSpeed = 3 )
                    .Should().Throw<InvalidOperationException>().WithMessage( "A transaction is required." );
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
                    d.Invoking( sut => sut.ExportToString() )
                     .Should().Throw<LockRecursionException>();
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
                    d.Invoking( sut => sut.Save( TestHelper.Monitor, new MemoryStream() ) )
                     .Should().NotThrow();
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
                    d.Invoking( sut => sut.Save( TestHelper.Monitor, s ) ).Should().NotThrow();
                    s.Position = 0;
                    d.Invoking( sut => sut.Load( TestHelper.Monitor, RewindableStream.FromStream( s ) ) ).Should().NotThrow();
                } );
                s.Position = 0;
                d.Invoking( sut => sut.Load( TestHelper.Monitor, RewindableStream.FromStream( s ) ) ).Should().NotThrow();
                s.Position = 0;
                d.Invoking( sut => sut.Save( TestHelper.Monitor, s ) ).Should().NotThrow();
            }
        }

        [Test]
        public async Task Modify_and_AcquireReadLock_reentrant_calls_are_detected_by_the_LockRecursionPolicy_NoRecursion_Async()
        {
            using( var d = new ObservableDomain(TestHelper.Monitor, "TEST", startTimer: true ) )
            {
                await d.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    d.Invoking( sut => sut.AcquireReadLock() )
                     .Should().Throw<LockRecursionException>();
                } );
                using( d.AcquireReadLock() )
                {
                    await d.Awaiting( sut => sut.ModifyThrowAsync( TestHelper.Monitor, null ) )
                            .Should().ThrowAsync<LockRecursionException>();
                }
                await d.ModifyThrowAsync( TestHelper.Monitor, async () =>
                {
                    await d.Awaiting( sut => sut.ModifyThrowAsync( TestHelper.Monitor, null ) )
                            .Should().ThrowAsync<LockRecursionException>();
                } );
                using( d.AcquireReadLock() )
                {
                    d.Invoking( sut => sut.AcquireReadLock() )
                     .Should().Throw<LockRecursionException>();
                }
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
                    d.AllObjects.Should().ContainSingle( x => x == c );
                    d.AllObjects.Should().HaveCount( 1 );
                    c.Destroyed += OnDestroy;
                    c.Destroy();
                    OnDestroyCalled.Should().BeTrue();
                    d.AllObjects.Should().BeEmpty();
                } );
            }
        }

        static bool OnDestroyCalled;

        static void OnDestroy( object sender, ObservableDomainEventArgs e ) => OnDestroyCalled = true;
    }
}
