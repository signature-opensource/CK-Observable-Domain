using System;
using System.Diagnostics;
using System.IO;
using System.Text;
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace CK.Observable
{

    public class JSONExportTarget : IObjectExporterTarget
    {
        readonly JSONExportTargetOptions _options;
        readonly TextWriter _w;
        private readonly JsonSerializerOptions _jsonSerializerOptions;
        readonly StringBuilder _buffer;
        bool _commaNeeded;

        public JSONExportTarget( TextWriter w, JSONExportTargetOptions? options = null, JsonSerializerOptions? jsonSerializerOptions = null )
        {
            _w = w;
            _jsonSerializerOptions = jsonSerializerOptions ?? JsonSerializerOptions.Default;
            _options = options ?? JSONExportTargetOptions.EmptyPrefix;
            _buffer = new StringBuilder();
        }

        /// <summary>
        /// Resets any internal state so that any contextual information are lost.
        /// </summary>
        public void ResetContext()
        {
            _commaNeeded = false;
        }

        public void EmitBool( bool o )
        {
            if( _commaNeeded ) _w.Write( ',' );
            _w.Write( o ? "true" : "false" );
            _commaNeeded = true;
        }

        void EmitObjectStartWithNum( int num )
        {
            Debug.Assert( num >= 0 );
            if( _commaNeeded ) _w.Write( ',' );
            _w.Write( _options.ObjectNumberPrefix );
            _w.Write( num );
            _commaNeeded = true;
        }

        public void EmitEmptyObject( int num )
        {
            Debug.Assert( num >= 0 );
            EmitObjectStartWithNum( num );
            _w.Write( "}" );
            Debug.Assert( _commaNeeded );
        }

        public void EmitStartObject( int num, ObjectExportedKind kind )
        {
            Debug.Assert( kind != ObjectExportedKind.None );
            if( kind == ObjectExportedKind.Object )
            {
                if( num >= 0 ) EmitObjectStartWithNum( num );
                else
                {
                    if( _commaNeeded ) _w.Write( ',' );
                    _w.Write( '{' );
                    _commaNeeded = false;
                }
            }
            else
            {
                if( _commaNeeded ) _w.Write( ',' );
                if( num >= 0 )
                {
                    _w.Write( _options.GetPrefixTypeFormat( kind ), num );
                    _commaNeeded = true;
                }
                else
                {
                    if( kind != ObjectExportedKind.List )
                    {
                        throw new InvalidOperationException( $"Only List and Object export support untracked (non numbered) objects: ObjectExportedKind = {kind}" );
                    }
                    _w.Write( "[" );
                    _commaNeeded = false;
                }
            }
        }

        public void EmitEndObject( int num, ObjectExportedKind kind )
        {
            Debug.Assert( kind != ObjectExportedKind.None );
            _w.Write( kind == ObjectExportedKind.Object ? '}' : ']' );
            _commaNeeded = true;
        }

        public void EmitNull()
        {
            if( _commaNeeded ) _w.Write( ',' );
            _w.Write( "null" );
            _commaNeeded = true;
        }

        public void EmitPropertyName( string name )
        {
            EmitString( name );
            _w.Write( ':' );
            _commaNeeded = false;
        }

        public void EmitReference( int num )
        {
            Debug.Assert( num >= 0 );
            if( _commaNeeded ) _w.Write( ',' );
            _w.Write( _options.ObjectReferencePrefix );
            _w.Write( num );
            _w.Write( '}' );
            _commaNeeded = true;
        }

        public void EmitDouble( double o )
        {
            if( _commaNeeded ) _w.Write( ',' );
            _w.Write( o.ToString( _jsonSerializerOptions.CultureInfo ) );
            _commaNeeded = true;
        }

        public void EmitString( string value )
        {
            if( _commaNeeded ) _w.Write( ',' );
            _buffer.Clear();
            _buffer.Append( '"' );
            AppendJSONEscaped( _buffer, value );
            _buffer.Append( '"' );
            _w.Write( _buffer.ToString() );
            _commaNeeded = true;
        }

        public void EmitStartList()
        {
            if( _commaNeeded ) _w.Write( ',' );
            _w.Write( '[' );
            _commaNeeded = false;
        }

        public void EmitEndList()
        {
            _w.Write( ']' );
            _commaNeeded = true;
        }

        public void EmitChar( char o ) => EmitString( o.ToString( _jsonSerializerOptions.CultureInfo ) );

        public void EmitSingle( float o ) => EmitDouble( o );

        public void EmitDateTime( DateTime o ) => EmitString( _jsonSerializerOptions.DateTimeConverter( o ) );

        public void EmitTimeSpan( TimeSpan o ) => EmitString( _jsonSerializerOptions.TimeSpanConverter( o ) );

        public void EmitDateTimeOffset( DateTimeOffset o ) => EmitString( _jsonSerializerOptions.DateTimeOffsetConverter( o ) );

        public void EmitGuid( Guid o ) => EmitString( o.ToString( "N" ) );

        public void EmitByte( byte o ) => EmitDouble( o );

        public void EmitSByte( decimal o ) => EmitDouble( (double)o );

        public void EmitInt16( short o ) => EmitDouble( o );

        public void EmitUInt16( ushort o ) => EmitDouble( o );

        public void EmitInt32( int o ) => EmitDouble( o );

        public void EmitUInt32( uint o ) => EmitDouble( o );

        public void EmitInt64( long o ) => EmitDouble( o );

        public void EmitUInt64( ulong o ) => EmitDouble( o );


        /// <summary>
        /// Appends a string (that can be null) to this StringBuilder, applying JSON escaping.
        /// This does not prepend/append enclosing double quotes.
        /// </summary>
        /// <param name="this">This StringBuilder.</param>
        /// <param name="s">The string. Can be null.</param>
        /// <param name="useEscapedUnicode">True to use unicode hexadecimal code for non-ascii characters.</param>
        /// <returns>This string builder to enable fluent syntax.</returns>
        static StringBuilder AppendJSONEscaped( StringBuilder @this, string s, bool useEscapedUnicode = false )
        {
            if( s == null ) return @this;
            return DoAppendJSONEscaped( @this, s, 0, s.Length, useEscapedUnicode );
        }

        static StringBuilder DoAppendJSONEscaped( StringBuilder @this, string s, int startIndex, int count, bool useEscapedUnicode )
        {
            int markIdx = -1;
            int index = startIndex;
            while( --count >= 0 )
            {
                char c = s[index];
                if( useEscapedUnicode )
                {
                    if( c >= ' ' && c < 128 && c != '\"' && c != '\\' )
                    {
                        if( markIdx == -1 ) markIdx = index;
                        ++index;
                        continue;
                    }
                }
                else
                {
                    if( c != '\t' && c != '\n' && c != '\r' && c != '\"' && c != '\\' && c != '\0' )
                    {
                        if( markIdx == -1 ) markIdx = index;
                        ++index;
                        continue;
                    }
                }
                if( markIdx != -1 )
                {
                    @this.Append( s, markIdx, index - markIdx );
                    markIdx = -1;
                }
                switch( c )
                {
                    case '\t': @this.Append( "\\t" ); break;
                    case '\r': @this.Append( "\\r" ); break;
                    case '\n': @this.Append( "\\n" ); break;
                    case '"':
                    case '\\': @this.Append( '\\' ); @this.Append( c ); break;
                    case '\0': @this.Append( "\\u0000" ); break;
                    default:
                        if( useEscapedUnicode )
                        {
                            @this.Append( "\\u" );
                            @this.Append( ((int)c).ToString( "X4", System.Globalization.NumberFormatInfo.InvariantInfo ) );
                        }
                        else @this.Append( c );
                        break;
                }
                ++index;
            }
            if( markIdx != -1 ) @this.Append( s, markIdx, index - markIdx );
            return @this;
        }

    }
}
