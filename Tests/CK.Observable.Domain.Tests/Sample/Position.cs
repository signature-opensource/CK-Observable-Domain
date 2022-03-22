using CK.Core;
using Microsoft.VisualBasic.CompilerServices;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace CK.Observable.Domain.Tests.Sample
{
    [SerializationVersion( 0 )]
    public struct Position : IEquatable<Position>, BinarySerialization.ICKSlicedSerializable
    {
        public readonly double Latitude;
        public readonly double Longitude;

        public Position( double latitude, double longitude )
        {
            Latitude = latitude;
            Longitude = longitude;
        }

        Position( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
        {
            Latitude = r.Reader.ReadDouble();
            Longitude = r.Reader.ReadDouble();
        }

        public static void Write( BinarySerialization.IBinarySerializer s, in Position o )
        {
            s.Writer.Write( o.Latitude );
            s.Writer.Write( o.Longitude );
        }

        void Export( int num, ObjectExporter e )
        {
            e.Target.EmitStartObject( num, ObjectExportedKind.Object );
            e.ExportNamedProperty( "Lat", Latitude );
            e.ExportNamedProperty( "Long", Longitude );
            e.Target.EmitEndObject( num, ObjectExportedKind.Object );
        }

        public override string ToString() => $"({Latitude.ToString( CultureInfo.InvariantCulture )},{Longitude.ToString( CultureInfo.InvariantCulture )})";


        public bool Equals( Position other ) => Latitude == other.Latitude && Longitude == other.Longitude;
        public override bool Equals( object? obj ) => obj is Position p && Equals( p );
        public override int GetHashCode() => HashCode.Combine( Latitude, Longitude );
        public static bool operator ==( Position o1, Position o2 ) => o1.Equals( o2 );
        public static bool operator !=( Position o1, Position o2 ) => !o1.Equals( o2 );
    }
}
