using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Domain.Tests.Sample
{
    public static class SempleDomain
    {
        public static ObservableDomain CreateSample()
        {
            var d = new ObservableDomain( TestHelper.Monitor );
            d.Modify( () =>
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
            CheckSampleGarage1( d );
            return d;
        }

        public static void CheckSampleGarage1( ObservableDomain d )
        {
            var g1 = d.AllObjects.OfType<Garage>().Where( x => x.CompanyName == "Boite" ).Single();
            g1.Cars.Select( c => c.Name ).Should().BeEquivalentTo( Enumerable.Range( 0, 10 ).Select( i => $"Renault n°{i}" ) );
            var minc = d.AllObjects.OfType<Person>().Single( x => x.FirstName == "Paul" );
            minc.LastName.Should().Be( "Minc" );
            var scott =  d.AllObjects.OfType<Mechanic>().Single( x => x.FirstName == "Scott" );
            scott.CurrentCar.Should().BeSameAs( g1.Cars[2] );
            scott.Garage.Should().BeSameAs( g1 );
            g1.Employees.Should().Contain( scott );
            g1.ReplacementCar.Should().HaveCount( 2 );
            g1.ReplacementCar[g1.Cars[0]].Should().BeSameAs( g1.Cars[1] );
            g1.ReplacementCar[g1.Cars[2]].Should().BeSameAs( g1.Cars[3] );
        }
    }
}
