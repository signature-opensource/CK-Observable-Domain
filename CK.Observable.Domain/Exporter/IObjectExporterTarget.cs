using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    public interface IObjectExporterTarget
    {
        void EmitNull();
        void EmitReference( int num );
        void EmitEmptyObject( int num );
        void EmitString( string value );
        void EmitStartObject( int num, ObjectExportedKind kind );
        void EmitObjectProperty( string name );
        void EmitEndObject( int num, ObjectExportedKind kind );
        void EmitBool( bool o );
        void EmitChar( char o );
        void EmitDouble( double o );
        void EmitSByte( decimal o );
        void EmitInt16( short o );
        void EmitByte( byte o );
        void EmitUInt16( ushort o );
        void EmitInt32( int o );
        void EmitUInt32( uint o );
        void EmitInt64( long o );
        void EmitUInt64( ulong o );
        void EmitGuid( Guid o );
        void EmitDateTime( DateTime o );
        void EmitTimeSpan( TimeSpan o );
        void EmitDateTimeOffset( DateTimeOffset o );
        void EmitStartMapEntry();
        void EmitEndMapEntry();
    }

}
