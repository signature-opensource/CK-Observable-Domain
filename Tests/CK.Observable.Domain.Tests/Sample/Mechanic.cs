using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable.Domain.Tests.Sample
{
    [SerializationVersionAttribute( 0 )]
    public class Mechanic : Employee
    {
        public Mechanic( Garage garage )
            : base( garage )
        {
        }

        protected Mechanic( Deserializer d ) : base( d )
        {
            var r = d.StartReading();
            r.ReadObject<Car>( c => CurrentCar = c );
        }

        void Write( Serializer s )
        {
            s.WriteObject( CurrentCar );
        }

        public Car CurrentCar { get; set; }

        void OnCurrentCarChanged( object before, object after )
        {
            if( IsDeserializing ) return;
            DomainMonitor.Info( $"{ToString()} has a new Car: {CurrentCar?.ToString() ?? "null"}." );
            if( CurrentCar != null ) CurrentCar.CurrentMechanic = this;
            else ((Car)before).CurrentMechanic = null;
        }

        public override string ToString() => $"'Mechanic {FirstName} {LastName}'";
    }
}
