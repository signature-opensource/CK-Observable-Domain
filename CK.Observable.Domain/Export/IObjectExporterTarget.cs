using System;

namespace CK.Observable
{
    public interface IObjectExporterTarget
    {
        /// <summary>
        /// Resets any internal state so that any contextual information are lost.
        /// </summary>
        void ResetContext();

        void EmitNull();
        void EmitReference( int num );
        void EmitEmptyObject( int num );
        void EmitString( string value );
        void EmitStartObject( int num, ObjectExportedKind kind );
        void EmitPropertyName( string name );
        void EmitEndObject( int num, ObjectExportedKind kind );
        void EmitDouble( double o );
        void EmitSingle( float o );
        void EmitBool( bool o );
        void EmitChar( char o );
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
