using CK.Core;

namespace CK.Observable.Domain.Tests.Sample
{
    [SerializationVersion( 0 )]
    public class Mechanic : Employee
    {
        public Mechanic( Garage garage )
            : base( garage )
        {
        }

        protected Mechanic( IBinaryDeserializerContext d ) : base( d )
        {
            var r = d.StartReading();
            CurrentCar = (Car)r.ReadObject();
        }

        void Write( BinarySerializer s )
        {
            s.WriteObject( CurrentCar );
        }

        public Car CurrentCar { get; set; }

        public MechanicLevel Level { get; set; }

        void OnCurrentCarChanged( object before, object after )
        {
            if( IsDeserializing ) return;
            Monitor.Info( $"{ToString()} has a new Car: {CurrentCar?.ToString() ?? "null"}." );
            if( CurrentCar != null ) CurrentCar.CurrentMechanic = this;
            else ((Car)before).CurrentMechanic = null;
        }

        public override string ToString() => $"'Mechanic {FirstName} {LastName}'";
    }
}
