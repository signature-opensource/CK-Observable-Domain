using CK.Core;
using System.Diagnostics;

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

        protected Employee( RevertSerialization _ ) : base( _ ) { }

        Employee( IBinaryDeserializer r, TypeReadInfo? info )
            : base( RevertSerialization.Default )
        {
            Garage = (Garage)r.ReadObject();
        }

        void Write( BinarySerializer s )
        {
            s.WriteObject( Garage );
        }

        public Garage Garage { get; set; }

        protected override void OnDestroy()
        {
            Garage.Employees.Remove( this );
            base.OnDestroy();
        }

        public override string ToString() => $"'Employee {FirstName} {LastName} - {Garage?.CompanyName ?? "(no company name)"}'";

    }
}
