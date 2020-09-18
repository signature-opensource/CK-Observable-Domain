using CK.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace CK.Observable
{
    /// <summary>
    /// Specializes <see cref="CKBinaryWriter"/> to be able to serialize objects graph.
    /// </summary>
    public class BinarySerializer : CKBinaryWriter
    {
        readonly Dictionary<Type, TypeInfo> _types;
        // This is a fix.
        // Until the serialization is refactored...
        readonly internal Dictionary<object, int> _seen;
        readonly ISerializerResolver _drivers;
        BinaryFormatter _binaryFormatter;

        int _debugModeCounter;
        int _debugSentinel;

        readonly struct TypeInfo
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
        /// <param name="drivers">Optional driver resolver to use. Uses <see cref="SerializerRegistry.Default"/> by default.</param>
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
                case Type type:
                    {
                        Write( (byte)SerializationMarker.Type );
                        Write( type );
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

        /// <summary>
        /// Gets whether this serializer is currently in debug mode.
        /// Initially defaults to false.
        /// </summary>
        public bool IsDebugMode => _debugModeCounter > 0;

        /// <summary>
        /// Activates or deactivates the debug mode. This is cumulative so that scoped activations are handled:
        /// activation/deactivation should be paired and <see cref="IsDebugMode"/> must be used to
        /// know whether debug mode is actually active.
        /// </summary>
        /// <param name="active">Whether the debug mode should be activated, deactivated or (when null) be left as it is.</param>
        public void DebugWriteMode( bool? active )
        {
            if( active.HasValue )
            {
                if( active.Value )
                {
                    Write( (byte)182 );
                    ++_debugModeCounter;
                }
                else
                {
                    Write( (byte)181 );
                    --_debugModeCounter;
                }
            }
            else Write( (byte)180 );
        }

        /// <summary>
        /// Writes a sentinel that must be read back by <see cref="IBinaryDeserializer.DebugCheckSentinel"/>.
        /// If <see cref="IsDebugMode"/> is false, nothing is written.
        /// </summary>
        /// <param name="fileName">Current file name that wrote the data. Used to build the <see cref="InvalidDataException"/> message if sentinel cannot be read back.</param>
        /// <param name="line">Current line number that wrote the data. Used to build the <see cref="InvalidDataException"/> message if sentinel cannot be read back.</param>
        public void DebugWriteSentinel( [CallerFilePath]string fileName = null, [CallerLineNumber] int line = 0 )
        {
            if( IsDebugMode )
            {
                Write( 987654321 );
                Write( _debugSentinel++ );
                Write( fileName + '(' + line.ToString() + ')' );
            }
        }

        /// <summary>
        /// Writes a type.
        /// </summary>
        /// <param name="t">The type to write. Can be null.</param>
        public void Write( Type t )
        {
            ITypeSerializationDriver driver = _drivers.FindDriver( t );
            if( driver != null ) driver.WriteTypeInformation( this );
            else WriteSimpleType( t );
        }

        internal bool WriteSimpleType( Type t )
        {
            if( DoWriteSimpleType( t ) )
            {
                // Fake version when the type is new: AutoTypeRegistry serializes the base class
                // (it directly calls DoWriteSimpleType) right after their own version that is,
                // by design, positive. -1 stops the chain.
                WriteSmallInt32( -1 );
                return true;
            }
            return false;
        }

        /// <summary>
        /// Writes the type, returning true if the type has been written for the first time
        /// or false if it has been previsously written.
        /// </summary>
        /// <param name="t">Type to serialize. Can be null.</param>
        /// <returns>True if the type has been written, false if it was already serialized.</returns>
        internal bool DoWriteSimpleType( Type? t )
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
                Write( (byte)3 );
                WriteNonNegativeSmallInt32( info.Number );
            }
            return false;
        }

        /// <summary>
        /// Magic yet simple helper to check the serialization implementation: the object (and potentially the whole graph behind)
        /// is serialized then deserialized and the result of the deserialization is the serialized again.
        /// Once this 3 steps have been done, the bytes that are the result of the first serialization are checked against the ones of the second serialization.
        /// The 2 byte sequences must be exactly the same.
        /// </summary>
        /// <param name="o">The object to check.</param>
        /// <param name="services">Optional services that deserialization may require.</param>
        /// <param name="throwOnFailure">False to log silently fail and return false.</param>
        /// <returns>True on success, fasle on error (if <paramref name="throwOnFailure"/> is false).</returns>
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
                        r.ImplementationServices.ExecutePostDeserializationActions();
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

    }
}
