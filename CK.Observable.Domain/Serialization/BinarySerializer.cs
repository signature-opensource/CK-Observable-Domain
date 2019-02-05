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
        /// Gets the serialization drivers.
        /// </summary>
        public ISerializerResolver Drivers => _drivers;

        /// <summary>
        /// Writes an object that can be null and of any type.
        /// </summary>
        /// <param name="o">The object to write.</param>
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
                if( !t.IsSerializable ) throw new InvalidOperationException( $"Type {t} is not serializable." );
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

        public void Write<T>( T o, ITypeSerializationDriver<T> driver )
        {
            if( driver == null ) throw new ArgumentNullException( nameof( driver ) );
            if( o == null )
            {
                Write( (byte)SerializationMarker.Null );
                return;
            }
            SerializationMarker marker;
            Type t = o.GetType();
            int idxSeen = -1;
            // This is awful. Drivers need to be the actual handler of the full
            // serialization/deserialization process, including instance tracking,
            // regardless of the struct/class kind of the objects.
            // New object pools from CK.Core should definitly help for this.
            if( t.IsClass && t != typeof(string) )
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
            Write( (byte)marker );
            driver.WriteData( this, o );
        }

        internal bool WriteSimpleType( Type t, string alias)
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

        TypeInfo RegisterType( Type t )
        {
            var info = new TypeInfo( t, _types.Count );
            _types.Add( t, info );
            return info;
        }

        public static bool IdempotenceCheck( object o, IServiceProvider services, bool throwOnFailure = true )
        {
            try
            {
                using( var s = new MemoryStream() )
                using( var w = new BinarySerializer( s, null, true ) )
                {
                    w.WriteObject( o );
                    var originalBytes = s.ToArray();
                    s.Position = 0;
                    using( var r = new BinaryDeserializer( s, services, null ) )
                    {
                        var o2 = r.ReadObject();
                        using( var s2 = new MemoryStream() )
                        using( var w2 = new BinarySerializer( s2, null, true ) )
                        {
                            w2.WriteObject( o2 );
                            var rewriteBytes = s2.ToArray();
                            if( !originalBytes.SequenceEqual( rewriteBytes ) )
                            {
                                throw new Exception( "Reserialized bytes differ from original serialized bytes." );
                            }
                        }
                    }
                }
                return true;
            }
            catch
            {
                if( throwOnFailure ) throw;
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
