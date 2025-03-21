using Shouldly;
using System;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Domain.Tests.Sample;

public static class SampleDomain
{
    public static async Task<ObservableDomain> CreateSampleAsync( IObservableDomainClient? tm = null )
    {
        var d = new ObservableDomain(TestHelper.Monitor, "TEST", startTimer: false, client: tm );
        await d.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            var g1 = new Garage() { CompanyName = "Boite" };
            g1.Cars.AddRange( Enumerable.Range( 0, 10 ).Select( i => new Car( $"Renault n°{i}" ) ) );
            new Person() { FirstName = "Paul", LastName = "Minc" };
            var scott = new Mechanic( g1 ) { FirstName = "Scott", LastName = "Guthrie" };
            scott.CurrentCar = g1.Cars[2];
            g1.ReplacementCar.Add( g1.Cars[0], g1.Cars[1] );
            g1.ReplacementCar.Add( g1.Cars[2], g1.Cars[3] );

            var g2 = new Garage();
            new Employee( g2 ) { FirstName = "Julien", LastName = "Mathon" };
            new Mechanic( g2 ) { FirstName = "Idriss", LastName = "Hippocrate" };
            new Mechanic( g2 ) { FirstName = "Cedric", LastName = "Legendre" };
            new Mechanic( g2 ) { FirstName = "Benjamin", LastName = "Crosnier" };
            new Mechanic( g2 ) { FirstName = "Alexandre", LastName = "Da Silva" };
            new Mechanic( g2 ) { FirstName = "Olivier", LastName = "Spinelli" };
            g2.Cars.AddRange( Enumerable.Range( 0, 10 ).Select( i => new Car( $"Volvo n°{i}" ) ) );
        } );
        CheckSampleGarage( d );
        return d;
    }

    public static Task<TransactionResult> TrySetPaulMincLastNameAsync( ObservableDomain d, string newLastName, bool throwException = false )
    {
        return d.TryModifyAsync( TestHelper.Monitor, () =>
        {
            d.AllObjects.Items.OfType<Person>().Single( x => x.FirstName == "Paul" ).LastName = newLastName;
            if( throwException ) throw new Exception( $"After Paul minc renamed to {newLastName}." );
        } );
    }

    public static void CheckSampleGarage( ObservableDomain d )
    {
        d.Read( TestHelper.Monitor, () =>
        {
            var g1 = d.AllObjects.Items.OfType<Garage>().Where( x => x.CompanyName == "Boite" ).Single();
            g1.Cars.Select( c => c.Name ).ShouldBe( Enumerable.Range( 0, 10 ).Select( i => $"Renault n°{i}" ) );
            var minc = d.AllObjects.Items.OfType<Person>().Single( x => x.FirstName == "Paul" );
            minc.LastName.ShouldBe( "Minc" );
            var scott = d.AllObjects.Items.OfType<Mechanic>().Single( x => x.FirstName == "Scott" );
            scott.CurrentCar.ShouldBeSameAs( g1.Cars[2] );
            scott.Garage.ShouldBeSameAs( g1 );
            g1.Employees.ShouldContain( scott );
            g1.ReplacementCar.Count.ShouldBe( 2 );
            g1.ReplacementCar[g1.Cars[0]].ShouldBeSameAs( g1.Cars[1] );
            g1.ReplacementCar[g1.Cars[2]].ShouldBeSameAs( g1.Cars[3] );
        } );
    }
}
