using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Text
{
    static class CKTEXT_EXTENSION
    {

        /// <summary>
        /// Appends a string to this StringBuilder, applying JSON escaping.
        /// This does not prepend/append enclosing double quotes.
        /// </summary>
        /// <param name="this">This StringBuilder.</param>
        /// <param name="s">The string.</param>
        /// <param name="useEscapedUnicode">True to use unicode hexadecimal code for non-ascii characters.</param>
        /// <returns>This string builder to enable fluent syntax.</returns>
        public static StringBuilder AppendJSONEscaped( this StringBuilder @this, string s, bool useEscapedUnicode = false )
        {
            if( s == null ) throw new ArgumentNullException( nameof( s ) );
            return DoAppendJSONEscaped( @this, s, 0, s.Length, useEscapedUnicode );
        }

        /// <summary>
        /// Appends the sub string to this StringBuilder, applying JSON escaping.
        /// This does not prepend/append enclosing double quotes.
        /// </summary>
        /// <param name="this">This StringBuilder.</param>
        /// <param name="s">The string.</param>
        /// <param name="startIndex">Start index in the string.</param>
        /// <param name="count">Number of characters to consider.</param>
        /// <param name="useEscapedUnicode">True to use unicode hexadecimal code for non-ascii characters.</param>
        /// <returns>This string builder to enable fluent syntax.</returns>
        public static StringBuilder AppendJSONEscaped( this StringBuilder @this, string s, int startIndex, int count, bool useEscapedUnicode = false )
        {
            if( s == null ) throw new ArgumentNullException( nameof( s ) );
            if( startIndex < 0 ) throw new ArgumentException( "Must greater or equal to 0.", nameof( startIndex ) );
            if( count < 0 ) throw new ArgumentException( "Must greater or equal to 0.", nameof( count ) );
            if( startIndex + count >= s.Length ) throw new ArgumentException( "Invalid startIndex/count in string." );
            return DoAppendJSONEscaped( @this, s, startIndex, count, useEscapedUnicode );
        }

        static StringBuilder DoAppendJSONEscaped( this StringBuilder @this, string s, int startIndex, int count, bool useEscapedUnicode )
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
                            @this.Append( ((int)c).ToString( "X4", NumberFormatInfo.InvariantInfo ) );
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
