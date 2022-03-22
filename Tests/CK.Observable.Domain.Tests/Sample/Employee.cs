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

        protected Employee( BinarySerialization.Sliced _ ) : base( _ ) { }

        Employee( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
            : base( BinarySerialization.Sliced.Instance )
        {
            Garage = r.ReadObject<Garage>();
        }

        public static void Write( BinarySerialization.IBinarySerializer s, in Employee o )
        {
            s.WriteObject( o.Garage );
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
