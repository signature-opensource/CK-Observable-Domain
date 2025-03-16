using CK.Observable.Domain.Tests.Sample;
using Shouldly;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Domain.Tests;

[TestFixture]
public class TransactionManagerTests
{
    [Test]
    public async Task transaction_works_for_the_very_first_one_Async()
    {
        using( var d = new ObservableDomain(TestHelper.Monitor, "TEST", startTimer: true, client: new Clients.ConcreteMemoryTransactionProviderClient()))
        {
            d.TransactionSerialNumber.ShouldBe( 0 );
            var result = await d.TryModifyAsync( TestHelper.Monitor, () =>
            {
                new Car( "V1" );
                new Car( "V2" );
                d.AllObjects.Items.Count.ShouldBe( 2 );
                throw new Exception( "Failure." );
            } );
            result.Errors.ShouldNotBeEmpty();
            d.TransactionSerialNumber.ShouldBe( 0 );
            d.AllObjects.Items.Count.ShouldBe( 0 );
            d.GetFreeList().ShouldBeEmpty();
        }
    }

    [Test]
    public async Task transaction_manager_with_rollbacks_Async()
    {
        using( var d = await SampleDomain.CreateSampleAsync( new Clients.ConcreteMemoryTransactionProviderClient() ) )
        {
            d.TransactionSerialNumber.ShouldBe( 1 );
            var r = await SampleDomain.TrySetPaulMincLastNameAsync( d, "No-More-Minc" );
            r.Success.ShouldBeTrue();
            d.TransactionSerialNumber.ShouldBe( 2 );
            d.AllObjects.Items.OfType<Person>().Single( x => x.FirstName == "Paul" ).LastName.ShouldBe( "No-More-Minc" );

            r = await SampleDomain.TrySetPaulMincLastNameAsync( d, "Minc" );
            r.Success.ShouldBeTrue();
            d.TransactionSerialNumber.ShouldBe( 3 );

            SampleDomain.CheckSampleGarage( d );

            r = await SampleDomain.TrySetPaulMincLastNameAsync( d, "No-More-Minc", throwException: true );
            r.Success.ShouldBeFalse();
            r.Errors.ShouldNotBeEmpty();
            d.TransactionSerialNumber.ShouldBe( 3 );

            // The domain has been rolled back.
            SampleDomain.CheckSampleGarage( d );
        }
    }
}
