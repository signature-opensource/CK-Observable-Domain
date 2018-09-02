using CK.Observable.Domain.Tests.Sample;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable.Domain.Tests
{
    [TestFixture]
    public class TransactionManagerTests
    {
        [Test]
        public void Without_transaction_manager_the_very_first_operation_is_transacted()
        {
            var d = new ObservableDomain();
            d.TransactionSerialNumber.Should().Be( 0 );
            var events = d.Modify( () =>
            {
                new Car( "V1" );
                new Car( "V2" );
                d.AllObjects.Should().HaveCount( 2 );
                throw new Exception( "Failure." );
            } );
            events.Should().BeNull();
            d.TransactionSerialNumber.Should().Be( 0 );
            d.AllObjects.Should().HaveCount( 0 );
            d.GetFreeList().Should().BeEmpty();
        }

        [Test]
        public void transaction_manager_with_rollbacks()
        {
            var d = SempleDomain.CreateSample( new SecureInMemoryTransactionManager() );
            d.TransactionSerialNumber.Should().Be( 1 );
            var events = SempleDomain.TransactedSetPaulMincLastName( d, "No-More-Minc" );
            events.Should().NotBeNull();
            d.TransactionSerialNumber.Should().Be( 2 );
            d.AllObjects.OfType<Person>().Single( x => x.FirstName == "Paul" ).LastName.Should().Be( "No-More-Minc" );

            events = SempleDomain.TransactedSetPaulMincLastName( d, "Minc" );
            events.Should().NotBeNull();
            d.TransactionSerialNumber.Should().Be( 3 );
            SempleDomain.CheckSampleGarage1( d );

            events = SempleDomain.TransactedSetPaulMincLastName( d, "No-More-Minc", throwException: true );
            events.Should().BeNull();
            d.TransactionSerialNumber.Should().Be( 3 );
            SempleDomain.CheckSampleGarage1( d );
        }

        static void Check( IReadOnlyList<IObservableEvent> events, params string[] e )
        {
            events.Should().HaveCount( e.Length );
            events.Select( ev => ev.ToString() ).Should().ContainInOrder( e );
        }
    }
}
