using CK.Core;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable.Domain.Tests.Sample
{
    [SerializationVersionAttribute(0)]
    public struct Position
    {
        public readonly double Latitude;
        public readonly double Longitude;

        public Position( double latitude, double longitude )
        {
            Latitude = latitude;
            Longitude = longitude;
        }

        private Position( Deserializer d )
        {
            var r = d.StartReading();
            Latitude = r.ReadDouble();
            Longitude = r.ReadDouble();
        }

        void Write( Serializer s )
        {
            s.Write( Latitude );
            s.Write( Longitude );
        }

        public override string ToString() => $"({Latitude.ToString(CultureInfo.InvariantCulture)},{Longitude.ToString( CultureInfo.InvariantCulture )})";
    }

    [SerializationVersionAttribute(0)]
    public class Car : ObservableObject
    {
        public Car( string name )
        {
            DomainMonitor.Info( $"Creating Car '{name}'." );
            Name = name;
        }

        public Car( Deserializer d ) : base( d )
        {
            var r = d.StartReading();
            Name = r.ReadNullableString();
            Speed = r.ReadInt32();
            r.ReadObject<Position>( x => Position = x );
        }

        void Write( Serializer w )
        {
            w.WriteNullableString( Name );
            w.Write( Speed );
            w.WriteObject( Position );
        }

        public string Name { get; }

        public int Speed { get; set; }

        public Position Position { get; set; }

        public Mechanic CurrentMechanic { get; set; }

        public override string ToString() => $"'Car {Name}'";

        void OnCurrentMechanicChanged( object before, object after )
        {
            if( IsDeserializing ) return;
            DomainMonitor.Info( $"{ToString()} is now repaired by: {CurrentMechanic?.ToString() ?? "null"}." );
            if( CurrentMechanic != null ) CurrentMechanic.CurrentCar = this;
            else ((Mechanic)before).CurrentCar = null;
        }

    }
}
