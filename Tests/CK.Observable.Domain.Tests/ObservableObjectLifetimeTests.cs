using CK.Core;
using CK.Observable.Domain.Tests.Sample;
using FluentAssertions;
using NUnit.Framework;
using System;
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
            using( var d = new ObservableDomain( TestHelper.Monitor, "TEST" ) )
            {
                Action outOfTran = () => new Car( "" );
                outOfTran.Should().Throw<InvalidOperationException>().WithMessage( "A transaction is required*" );
            }
        }

        [Test]
        public void an_observable_must_be_modified_in_the_context_of_a_transaction()
        {
            using( var d = new ObservableDomain( TestHelper.Monitor, "TEST" ) )
            {
                using( var t = d.BeginTransaction( TestHelper.Monitor ) )
                {
                    new Car( "Hello" );
                    var result = t.Commit();
                    result.Errors.Should().BeEmpty();
                    result.Events.Should().HaveCount( 11 );
                    result.Commands.Should().BeEmpty();
                }
                d.Invoking( sut => sut.AllObjects.OfType<Car>().Single().Speed = 3 )
                 .Should().Throw<InvalidOperationException>().WithMessage( "A transaction is required." );
            }
        }

        class JustAnObservableObject : ObservableObject
        {
            public JustAnObservableObject( ObservableDomain d )
                : base( d )
            {
            }
            public int Speed { get; set; }

            void Export( ObjectExporter e )
            {
            }

        }

        [Test]
        public void concurrent_accesses_are_detected()
        {
            using( var d = new ObservableDomain( TestHelper.Monitor, "TEST" ) )
            {
                using( d.BeginTransaction( TestHelper.Monitor ) )
                {
                    Action concurrent = () => Parallel.For( 0, 20, i =>
                    {
                        var c = new JustAnObservableObject( d );
                        System.Threading.Thread.Sleep( 2 );
                    } );
                    concurrent.Should()
                                .Throw<AggregateException>()
                                .WithInnerException<InvalidOperationException>().WithMessage( "Concurrent access: no lock has been acquired." );
                }
            }
        }

        [Test]
        public void Export_can_NOT_be_called_within_a_transaction_because_of_LockRecursionPolicy_NoRecursion()
        {
            using( var d = new ObservableDomain( TestHelper.Monitor, "TEST" ) )
            {
                using( d.BeginTransaction( TestHelper.Monitor ) )
                {
                    d.Invoking( sut => sut.ExportToString() )
                     .Should().Throw<System.Threading.LockRecursionException>();
                }
            }
        }

        [Test]
        public void Save_can_be_called_from_inside_a_transaction()
        {
            using( var d = new ObservableDomain( TestHelper.Monitor, "TEST" ) )
            {
                using( d.BeginTransaction( TestHelper.Monitor ) )
                {
                    d.Invoking( sut => sut.Save( TestHelper.Monitor, new MemoryStream() ) )
                     .Should().NotThrow();
                }
            }
        }

        [Test]
        public void Load_and_Save_can_be_called_from_inside_a_transaction_or_outside_any_transaction()
        {
            using( var s = new MemoryStream() )
            using( var d = new ObservableDomain( TestHelper.Monitor, "TEST" ) )
            {
                using( d.BeginTransaction( TestHelper.Monitor ) )
                {
                    d.Invoking( sut => sut.Save( TestHelper.Monitor, s, leaveOpen: true ) ).Should().NotThrow();
                    s.Position = 0;
                    d.Invoking( sut => sut.Load( TestHelper.Monitor, s, leaveOpen: true ) ).Should().NotThrow();
                }
                s.Position = 0;
                d.Invoking( sut => sut.Load( TestHelper.Monitor, s, leaveOpen: true ) ).Should().NotThrow();
                s.Position = 0;
                d.Invoking( sut => sut.Save( TestHelper.Monitor, s, leaveOpen: true ) ).Should().NotThrow();
            }
        }

        [Test]
        public void BeginTransaction_and_AcquireReadLock_reentrant_calls_are_detected_by_the_LockRecursionPolicy_NoRecursion()
        {
            using( var d = new ObservableDomain( TestHelper.Monitor, "TEST" ) )
            {
                using( d.BeginTransaction( TestHelper.Monitor ) )
                {
                    d.Invoking( sut => sut.AcquireReadLock() )
                     .Should().Throw<System.Threading.LockRecursionException>();
                }
                using( d.AcquireReadLock() )
                {
                    d.Invoking( sut => sut.BeginTransaction( TestHelper.Monitor ) )
                     .Should().Throw<System.Threading.LockRecursionException>();
                }
                using( d.BeginTransaction( TestHelper.Monitor ) )
                {
                    d.Invoking( sut => sut.BeginTransaction( TestHelper.Monitor ) )
                     .Should().Throw<System.Threading.LockRecursionException>();
                }
                using( d.AcquireReadLock() )
                {
                    d.Invoking( sut => sut.AcquireReadLock() )
                     .Should().Throw<System.Threading.LockRecursionException>();
                }
            }
        }

        [Test]
        public void ObservableObject_exposes_Disposed_event()
        {
            using( var d = new ObservableDomain( TestHelper.Monitor, "TEST" ) )
            {
                d.Modify( TestHelper.Monitor, () =>
                {
                    var c = new Car( "Titine" );
                    d.AllObjects.Should().ContainSingle( x => x == c );
                    using( var cM = c.Monitor() )
                    {
                        c.Dispose();
                        cM.Should().Raise( "Disposed" ).WithSender( c ).WithArgs<EventArgs>( a => a == EventArgs.Empty );
                    }
                    d.AllObjects.Should().BeEmpty();
                } ).Should().NotBeNull();
            }
        }


    }
}
