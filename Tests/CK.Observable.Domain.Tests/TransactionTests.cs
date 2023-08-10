using CK.Observable.Domain.Tests.Sample;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Domain.Tests
{
    [TestFixture]
    public class TransactionManagerTests
    {
        [Test]
        public async Task transaction_works_for_the_very_first_one_Async()
        {
            using( var d = new ObservableDomain(TestHelper.Monitor, "TEST", startTimer: true, client: new Clients.ConcreteMemoryTransactionProviderClient()))
            {
                d.TransactionSerialNumber.Should().Be( 0 );
                var result = await d.TryModifyAsync( TestHelper.Monitor, () =>
                {
                    new Car( "V1" );
                    new Car( "V2" );
                    d.AllObjects.Items.Should().HaveCount( 2 );
                    throw new Exception( "Failure." );
                } );
                result.Errors.Should().NotBeEmpty();
                d.TransactionSerialNumber.Should().Be( 0 );
                d.AllObjects.Items.Should().HaveCount( 0 );
                d.GetFreeList().Should().BeEmpty();
            }
        }

        [Test]
        public async Task transaction_manager_with_rollbacks_Async()
        {
            using( var d = await SampleDomain.CreateSampleAsync( new Clients.ConcreteMemoryTransactionProviderClient() ) )
            {
                d.TransactionSerialNumber.Should().Be( 1 );
                var r = await SampleDomain.TrySetPaulMincLastNameAsync( d, "No-More-Minc" );
                r.Success.Should().BeTrue();
                d.TransactionSerialNumber.Should().Be( 2 );
                d.AllObjects.Items.OfType<Person>().Single( x => x.FirstName == "Paul" ).LastName.Should().Be( "No-More-Minc" );

                r = await SampleDomain.TrySetPaulMincLastNameAsync( d, "Minc" );
                r.Success.Should().BeTrue();
                d.TransactionSerialNumber.Should().Be( 3 );

                SampleDomain.CheckSampleGarage( d );

                r = await SampleDomain.TrySetPaulMincLastNameAsync( d, "No-More-Minc", throwException: true );
                r.Success.Should().BeFalse();
                r.Errors.Should().NotBeEmpty();
                d.TransactionSerialNumber.Should().Be( 3 );

                // The domain has been rolled back.
                SampleDomain.CheckSampleGarage( d );
            }
        }
    }
}
