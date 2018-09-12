using CK.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    public class BinarySerializer : CKBinaryWriter
    {
        readonly Dictionary<Type, TypeInfo> _types;
        readonly Dictionary<object, int> _seen;

        struct TypeInfo
        {
            public readonly Type Type;
            public readonly int Number;

            public TypeInfo( Type t, int number )
            {
                Type = t;
                Number = number;
            }
        }

        /// <summary>
        /// Initializes a new <see cref="BinarySerializer"/> onto a stream.
        /// </summary>
        /// <param name="output">The stream to write to.</param>
        /// <param name="leaveOpen">True to leave the stram opened when disposing. False to close it.</param>
        /// <param name="encoding">Optional encoding for texts. Defaults to UTF-8.</param>
        public BinarySerializer( Stream output, bool leaveOpen, Encoding encoding = null )
            : base( output, encoding ?? Encoding.UTF8, leaveOpen )
        {
            _types = new Dictionary<Type, TypeInfo>();
            _seen = new Dictionary<object, int>( PureObjectRefEqualityComparer<object>.Default );
        }

        public void WriteObjects( int length, IEnumerable o )
        {
            if( o == null ) WriteSmallInt32( -1 );
            else
            {
                WriteSmallInt32( length );
                foreach( var i in o ) WriteObject( i );
            }
        }

        public void WriteObject( object o )
        {
            switch( o )
            {
                case null:
                    {
                        Write( (byte)SerializationMarker.Null );
                        return;
                    }
                case string s:
                    {
                        Write( (byte)SerializationMarker.String );
                        Write( s );
                        return;
                    }
                case int i:
                    {
                        Write( (byte)SerializationMarker.Int32 );
                        Write( i );
                        return;
                    }
                case double d:
                    {
                        Write( (byte)SerializationMarker.Double );
                        Write( d );
                        return;
                    }
                case char c:
                    {
                        Write( (byte)SerializationMarker.Char );
                        Write( c );
                        return;
                    }
                case bool b:
                    {
                        Write( (byte)SerializationMarker.Boolean );
                        Write( b );
                        return;
                    }
                case uint ui:
                    {
                        Write( (byte)SerializationMarker.UInt32 );
                        Write( ui );
                        return;
                    }
                case float f:
                    {
                        Write( (byte)SerializationMarker.Float );
                        Write( f );
                        return;
                    }
                case DateTime d:
                    {
                        Write( (byte)SerializationMarker.DateTime );
                        Write( d );
                        return;
                    }
                case Guid g:
                    {
                        Write( (byte)SerializationMarker.Guid );
                        Write( g );
                        return;
                    }
                case TimeSpan ts:
                    {
                        Write( (byte)SerializationMarker.TimeSpan );
                        Write( ts );
                        return;
                    }
                case DateTimeOffset ds:
                    {
                        Write( (byte)SerializationMarker.DateTimeOffset );
                        Write( ds );
                        return;
                    }
            }
            Type t = o.GetType();
            int idxSeen = -1;
            if( t.IsClass )
            {
                if( _seen.TryGetValue( o, out var num ) )
                {
                    Write( (byte)SerializationMarker.Reference );
                    Write( num );
                    return;
                }
                idxSeen = _seen.Count;
                _seen.Add( o, _seen.Count );
                if( t == typeof( object ) )
                {
                    Write( (byte)SerializationMarker.EmptyObject );
                    Write( idxSeen );
                    return;
                }
            }
            Write( (byte)SerializationMarker.Object );
            Write( idxSeen );
            ITypeSerializationDriver driver = (o is IKnowUnifiedTypeDriver k
                                                ? k.UnifiedTypeDriver
                                                : UnifiedTypeRegistry.FindDriver( t )).SerializationDriver;
            if( driver == null )
            {
                throw new InvalidOperationException( $"Type '{t.FullName}' is not serializable." );
            }
            driver.WriteTypeInformation( this );
            driver.WriteData( this, o );
        }

        internal void DoWriteSerializableTypeBased( UnifiedTypeRegistry.TypeInfo tInfo )
        {
        }

        internal bool WriteSimpleType( Type t )
        {
            if( DoWriteSimpleType( t ) )
            {
                WriteSmallInt32( -1 );
                return true;
            }
            return false;
        }

        internal bool DoWriteSimpleType( Type t )
        {
            if( t == null ) Write( (byte)0 );
            else if( t == typeof( object ) )
            {
                Write( (byte)1 );
            }
            else
            {
                if( !_types.TryGetValue( t, out var info ) )
                {
                    info = new TypeInfo( t, _types.Count );
                    _types.Add( t, info );
                    Write( (byte)2 );
                    Write( info.Type.AssemblyQualifiedName );
                    return true;
                }
                else
                {
                    Write( (byte)3 );
                    WriteNonNegativeSmallInt32( info.Number );
                }
            }
            return false;
        }

        // TODO: To be removed @next CK.Core version (transfered to CKBinaryWriter).

        /// <summary>
        /// Writes a DateTime value.
        /// </summary>
        /// <param name="d">The value to write.</param>
        public void Write( DateTime d )
        {
            Write( d.ToBinary() );
        }

        /// <summary>
        /// Writes a TimeSpan value.
        /// </summary>
        /// <param name="t">The value to write.</param>
        public void Write( TimeSpan t )
        {
            Write( t.Ticks );
        }

        /// <summary>
        /// Writes a DateTimeOffset value.
        /// </summary>
        /// <param name="ds">The value to write.</param>
        public void Write( DateTimeOffset ds )
        {
            Write( ds.DateTime );
            Write( (short)ds.Offset.TotalMinutes );
        }

        /// <summary>
        /// Writes a DateTimeOffset value.
        /// </summary>
        /// <param name="g">The value to write.</param>
        public void Write( Guid g )
        {
            Write( g.ToByteArray() );
        }

    }
}
