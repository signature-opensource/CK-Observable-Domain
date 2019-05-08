using CK.Observable.Domain.Tests.Sample;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Domain.Tests
{
    [TestFixture]
    public class TransactionManagerTests
    {
        [Test]
        public void transaction_works_for_the_very_first_one()
        {
            var d = new ObservableDomain( new SecureInMemoryTransactionManager() );
            d.TransactionSerialNumber.Should().Be( 0 );
            var result = d.Modify( () =>
            {
                new Car( "V1" );
                new Car( "V2" );
                d.AllObjects.Should().HaveCount( 2 );
                throw new Exception( "Failure." );
            } );
            result.Errors.Should().NotBeEmpty();
            d.TransactionSerialNumber.Should().Be( 0 );
            d.AllObjects.Should().HaveCount( 0 );
            d.GetFreeList().Should().BeEmpty();
        }

        [Test]
        public void transaction_manager_with_rollbacks()
        {
            var d = SampleDomain.CreateSample( new SecureInMemoryTransactionManager() );
            d.TransactionSerialNumber.Should().Be( 1 );
            TransactionResult result = SampleDomain.TransactedSetPaulMincLastName( d, "No-More-Minc" );
            result.Errors.Should().BeEmpty();
            d.TransactionSerialNumber.Should().Be( 2 );
            d.AllObjects.OfType<Person>().Single( x => x.FirstName == "Paul" ).LastName.Should().Be( "No-More-Minc" );

            result = SampleDomain.TransactedSetPaulMincLastName( d, "Minc" );
            result.Errors.Should().BeEmpty();
            d.TransactionSerialNumber.Should().Be( 3 );
            SampleDomain.CheckSampleGarage1( d );

            result = SampleDomain.TransactedSetPaulMincLastName( d, "No-More-Minc", throwException: true );
            result.Errors.Should().NotBeEmpty();
            d.TransactionSerialNumber.Should().Be( 3 );
            SampleDomain.CheckSampleGarage1( d );
        }

        static void Check( IReadOnlyList<ObservableEvent> events, params string[] e )
        {
            events.Should().HaveCount( e.Length );
            events.Select( ev => ev.ToString() ).Should().ContainInOrder( e );
        }
    }
}
