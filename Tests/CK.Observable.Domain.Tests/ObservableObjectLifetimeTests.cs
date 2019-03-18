using CK.Observable.Domain.Tests.Sample;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Linq;
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
            var d = new ObservableDomain();
            Action outOfTran = () => new Car( "" );
            outOfTran.Should().Throw<InvalidOperationException>().WithMessage( "*transaction*" );
        }

        [Test]
        public void an_observable_must_be_modified_in_the_context_of_a_transaction()
        {
            var d = new ObservableDomain();
            using( var t = d.BeginTransaction() )
            {
                new Car( "Hello" );
                t.Commit();
            }
            d.Invoking( sut => sut.AllObjects.OfType<Car>().Single().Speed = 3 )
             .Should().Throw<InvalidOperationException>().WithMessage( "*transaction*" );
        }

        class FakeReentrancy : ObservableObject
        {
            public FakeReentrancy( ObservableDomain d )
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
            var d = new ObservableDomain();
            using( d.BeginTransaction() )
            {
                Action reentrancies = () => Parallel.For( 0, 20, i =>
                {
                    var c = new FakeReentrancy( d );
                    System.Threading.Thread.Sleep( 2 );
                } );
                reentrancies.Should()
                            .Throw<AggregateException>()
                            .WithInnerException<InvalidOperationException>().WithMessage( "*reentrancy*" );
            }
        }

        [Test]
        public void reentrant_calls_are_detected()
        {
            var d = new ObservableDomain();
            using( d.BeginTransaction() )
            {
                d.Invoking( sut => sut.BeginTransaction() )
                 .Should().Throw<InvalidOperationException>()
                            .WithMessage( "*transaction*" );
            }
        }

        [Test]
        public void ObservableObject_exposes_Disposed_event()
        {
            var d = new ObservableDomain();
            d.Modify( () =>
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
