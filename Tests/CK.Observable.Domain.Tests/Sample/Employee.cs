using CK.Core;

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

        protected Employee( IBinaryDeserializerContext d ) : base( d )
        {
            var r = d.StartReading();
            Garage = (Garage)r.ReadObject();
        }

        void Write( BinarySerializer s )
        {
            s.WriteObject( Garage );
        }

        public Garage Garage { get; set; }

        protected override void OnDisposed( ObservableDomainEventArgs args, bool isReloading )
        {
            if( !isReloading )
            {
                Garage.Employees.Remove( this );
            }
            base.OnDisposed( args, isReloading );
        }

        public override string ToString() => $"'Employee {FirstName} {LastName} - {Garage?.CompanyName ?? "(no company name)"}'";

    }
}
