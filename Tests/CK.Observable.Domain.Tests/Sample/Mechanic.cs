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

        Mechanic( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
            : base( BinarySerialization.Sliced.Instance )
        {
            CurrentCar = r.ReadNullableObject<Car>();
        }

        public static void Write( BinarySerialization.IBinarySerializer s, in Mechanic o )
        {
            s.WriteNullableObject( o.CurrentCar );
        }

        public Car? CurrentCar { get; set; }

        public MechanicLevel Level { get; set; }

        void OnCurrentCarChanged( object before, object after )
        {
            if( Domain.IsDeserializing ) return;
            Domain.Monitor.Info( $"{ToString()} has a new Car: {CurrentCar?.ToString() ?? "null"}." );
            if( CurrentCar != null ) CurrentCar.CurrentMechanic = this;
            else ((Car)before).CurrentMechanic = null;
        }

        public override string ToString() => $"'Mechanic {FirstName} {LastName}'";
    }
}
