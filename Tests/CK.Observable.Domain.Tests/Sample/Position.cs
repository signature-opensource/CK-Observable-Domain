using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable.Domain.Tests.Sample
{
    [SerializationVersionAttribute( 0 )]
    public struct Position
    {
        public readonly double Latitude;
        public readonly double Longitude;

        public Position( double latitude, double longitude )
        {
            Latitude = latitude;
            Longitude = longitude;
        }

        Position( BinaryDeserializer d )
        {
            var r = d.StartReading();
            Latitude = r.ReadDouble();
            Longitude = r.ReadDouble();
        }

        void Write( BinarySerializer s )
        {
            s.Write( Latitude );
            s.Write( Longitude );
        }

        void Export( int num, ObjectExporter e )
        {
            e.Target.EmitStartObject( num, ObjectExportedKind.Object );
            e.ExportNamedProperty( "Lat", Latitude );
            e.ExportNamedProperty( "Long", Longitude );
            e.Target.EmitEndObject( num, ObjectExportedKind.Object );
        }

        public override string ToString() => $"({Latitude.ToString( CultureInfo.InvariantCulture )},{Longitude.ToString( CultureInfo.InvariantCulture )})";
    }
}
