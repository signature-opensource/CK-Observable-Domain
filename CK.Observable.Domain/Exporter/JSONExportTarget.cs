using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    public class JSONExportTarget : IObjectExporterTarget
    {
        static readonly string _prefix = '"' + "~$£€";
        static readonly string _pTypePrefix = _prefix + "þ";

        static readonly string _pRef = '{' + _prefix + ">\":";
        static readonly string _pNum = _prefix + "°\":";
        static readonly string _objectNumStart = '{' + _prefix + "°\":";
        static readonly string _pListFormat = "[{{" + _pTypePrefix + "\":[{0},\"A\"]}}";
        static readonly string _pMapFormat = "[{{" + _pTypePrefix + "\":[{0},\"M\"]}}";
        static readonly string _pSetFormat = "[{{" + _pTypePrefix + "\":[{0},\"S\"]}}";
        static readonly string[] _pTypeFormats = new string[]
            {
                _pListFormat,
                _pMapFormat,
                _pSetFormat
            };

        readonly TextWriter _w;
        bool _valueNeedComma;

        public JSONExportTarget( TextWriter w )
        {
            _w = w;
        }

        public void EmitBool( bool o )
        {
            if( _valueNeedComma ) _w.Write( ',' );
            _w.Write( o ? "true" : "false" );
            _valueNeedComma = true;
        }

        void EmitObjectStartWithNum( int num )
        {
            Debug.Assert( num >= 0 );
            if( _valueNeedComma ) _w.Write( ',' );
            _w.Write( _objectNumStart );
            _w.Write( num );
            _valueNeedComma = true;
        }

        public void EmitEmptyObject( int num )
        {
            Debug.Assert( num >= 0 );
            EmitObjectStartWithNum( num );
            _w.Write( "}" );
            Debug.Assert( _valueNeedComma );
        }

        public void EmitStartObject( int num, ObjectExportedKind kind )
        {
            Debug.Assert( kind != ObjectExportedKind.None );
            if( kind == ObjectExportedKind.Object )
            {
                if( num >= 0 ) EmitObjectStartWithNum( num );
                else
                {
                    _w.Write( '{' );
                    _valueNeedComma = false;
                }
            }
            else
            {
                if( _valueNeedComma ) _w.Write( ',' );
                Debug.Assert( (int)ObjectExportedKind.List == 1 );
                Debug.Assert( (int)ObjectExportedKind.Map == 2 );
                Debug.Assert( (int)ObjectExportedKind.Set == 3 );
                _w.Write( _pTypeFormats[(int)kind-1], num );
                _valueNeedComma = true;
            }
        }

        public void EmitEndObject( int num, ObjectExportedKind kind )
        {
            Debug.Assert( kind != ObjectExportedKind.None );
            _w.Write( kind == ObjectExportedKind.Object ? '}': ']' );
            _valueNeedComma = true;
        }

        public void EmitNull()
        {
            if( _valueNeedComma ) _w.Write( ',' );
            _w.Write( "null" );
            _valueNeedComma = true;
        }

        public void EmitObjectProperty( string name )
        {
            if( _valueNeedComma ) _w.Write( ',' );
            EmitString( name );
            _w.Write( ':' );
            _valueNeedComma = false;
        }

        public void EmitReference( int num )
        {
            Debug.Assert( num >= 0 );
            if( _valueNeedComma ) _w.Write( ',' );
            _w.Write( _pRef );
            _w.Write( num );
            _w.Write( '}' );
            _valueNeedComma = true;
        }

        public void EmitDouble( double o )
        {
            if( _valueNeedComma ) _w.Write( ',' );
            _w.Write( o.ToString( CultureInfo.InvariantCulture ) );
            _valueNeedComma = true;
        }

        public void EmitString( string value )
        {
            if( _valueNeedComma ) _w.Write( ',' );
            _w.Write( '"' );
            _w.Write( value.Replace( "\"", "\\\"" ) );
            _w.Write( '"' );
            _valueNeedComma = true;
        }

        public void EmitStartMapEntry()
        {
            if( _valueNeedComma ) _w.Write( ',' );
            _w.Write( '[' );
            _valueNeedComma = true;
        }

        public void EmitEndMapEntry()
        {
            _w.Write( ']' );
            _valueNeedComma = true;
        }

        public void EmitTimeSpan( TimeSpan o ) => EmitString( o.ToString() );

        public void EmitByte( byte o ) => EmitDouble( o );

        public void EmitChar( char o ) => EmitString( o.ToString() );

        public void EmitDateTime( DateTime o ) => EmitString( o.ToString() );

        public void EmitDateTimeOffset( DateTimeOffset o ) => EmitString( o.ToString() );

        public void EmitGuid( Guid o ) => EmitString( o.ToString() );

        public void EmitInt16( short o ) => EmitDouble( o );

        public void EmitInt32( int o ) => EmitDouble( o );

        public void EmitInt64( long o ) => EmitDouble( o );

        public void EmitSByte( decimal o ) => EmitDouble( (double)o );

        public void EmitUInt16( ushort o ) => EmitDouble( o );

        public void EmitUInt32( uint o ) => EmitDouble( o );

        public void EmitUInt64( ulong o ) => EmitDouble( o );


    }
}
