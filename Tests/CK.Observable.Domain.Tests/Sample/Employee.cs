using AutoProperties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable.Domain.Tests.Sample
{
    [SerializationVersion( 0 )]
    public class Employee : Person
    {
        public Employee( Garage garage )
        {
            Garage = garage;
            Garage.Employees.Add( this );
        }

        protected Employee( BinaryDeserializer d ) : base( d )
        {
            var r = d.StartReading();
            r.ReadObject<Garage>( x => Garage = x );
        }

        void Write( BinarySerializer s )
        {
            s.WriteObject( Garage );
        }

        public Garage Garage { get; set; }

        protected override void OnDisposed()
        {
            Garage.Employees.Remove( this );
        }

        public override string ToString() => $"'Employee {FirstName} {LastName} - {Garage?.CompanyName ?? "(no company name)"}'";

    }
}
