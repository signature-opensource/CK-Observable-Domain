using CK.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    public class BinarySerializer : CKBinaryWriter
    {
        readonly Dictionary<Type, TypeInfo> _types;
        readonly Dictionary<object, int> _seen;
        readonly ISerializerResolver _drivers;
        BinaryFormatter _binaryFormatter;

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
        public BinarySerializer(
            Stream output,
            ISerializerResolver drivers = null,
            bool leaveOpen = false,
            Encoding encoding = null )
            : base( output, encoding ?? Encoding.UTF8, leaveOpen )
        {
            _types = new Dictionary<Type, TypeInfo>();
            _seen = new Dictionary<object, int>( PureObjectRefEqualityComparer<object>.Default );
            _drivers = drivers ?? SerializerRegistry.Default;
        }

        /// <summary>
        /// Finds a serialization driver for a type or null if the type is not serializable.
        /// </summary>
        /// <typeparam name="T">The type.</typeparam>
        /// <returns>The driver or null.</returns>
        public ITypeSerializationDriver<T> FindDriver<T>() => _drivers.FindDriver<T>();

        /// <summary>
        /// Writes zero or more objects from any enumerable (that can be null).
        /// </summary>
        /// <param name="count">The number of objects to write. Must be zero or positive.</param>
        /// <param name="objects">
        /// The set of at least <paramref name="count"/> objects.
        /// Can be null (in such case count must be zero).
        /// </param>
        public void WriteObjects( int count, IEnumerable objects )
        {
            if( count < 0 ) throw new ArgumentException( "Must be greater or equal to 0.", nameof( count ) );
            if( objects == null )
            {
                if( count != 0 ) throw new ArgumentNullException( nameof( objects ) );
                WriteSmallInt32( -1 );
            }
            else
            {
                WriteSmallInt32( count );
                if( count > 0 ) DoWriteObjects( count, objects );
            }
        }

        void DoWriteObjects( int count, IEnumerable objects )
        {
            foreach( var o in objects )
            {
                WriteObject( o );
                if( --count == 0 ) break;
            }
            if( count > 0 ) throw new ArgumentException( $"Not enough objects: missing {count} objects.", nameof( count ) );
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
            SerializationMarker marker;
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
                    return;
                }
                marker = SerializationMarker.Object;
            }
            else marker = SerializationMarker.Struct;
            ITypeSerializationDriver driver = _drivers.FindDriver( t );
            if( driver == null )
            {
                marker -= 2;
                Write( (byte)marker );
                if( _binaryFormatter == null ) _binaryFormatter = new BinaryFormatter();
                _binaryFormatter.Serialize( BaseStream, o );
            }
            else
            {
                Write( (byte)marker );
                driver.WriteTypeInformation( this );
                driver.WriteData( this, o );
            }
        }

        internal bool WriteSimpleType( Type t, string alias )
        {
            if( DoWriteSimpleType( t, alias ) )
            {
                WriteSmallInt32( -1 );
                return true;
            }
            return false;
        }

        internal bool DoWriteSimpleType( Type t, string alias )
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
                    Write( alias ?? info.Type.AssemblyQualifiedName );
                    return true;
                }
                Write( (byte)3 );
                WriteNonNegativeSmallInt32( info.Number );
            }
            return false;
        }

        /// <summary>
        /// Writes any list content
        /// </summary>
        /// <typeparam name="T">The item type.</typeparam>
        /// <param name="count">The number of items. Must be 0 zero or positive.</param>
        /// <param name="items">The items. Can be null (in such case, <paramref name="count"/> must be zero).</param>
        /// <param name="itemSerializer">The item serializer. Must not be null.</param>
        public void WriteListContent<T>( int count, IEnumerable<T> items, ITypeSerializationDriver<T> itemSerializer )
        {
            if( count < 0 ) throw new ArgumentException( "Must be greater or equal to 0.", nameof( count ) );
            if( itemSerializer == null ) throw new ArgumentNullException( nameof( itemSerializer ) );
            if( items == null )
            {
                if( count != 0 ) throw new ArgumentNullException( nameof( items ) );
                WriteSmallInt32( -1 );
                return;
            }
            WriteSmallInt32( count );
            if( count > 0 )
            {
                var tI = itemSerializer.Type;
                bool monoType = tI.IsSealed || tI.IsValueType;
                Write( monoType );
                if( monoType )
                {
                    foreach( var i in items )
                    {
                        itemSerializer.WriteData( this, i );
                        if( --count == 0 ) break;
                    }
                    if( count > 0 ) throw new ArgumentException( $"Not enough items: missing {count} items.", nameof( count ) );
                }
                else
                {
                    DoWriteObjects( count, items );
                }
            }
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
