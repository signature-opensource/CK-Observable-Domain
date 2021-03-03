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
        public const int MaxRecurse = 50;

        readonly Dictionary<Type, TypeInfo> _types;
        readonly Dictionary<object, int> _seen;
        readonly ISerializerResolver _drivers;
        int _recurseCount;
        Stack<(object, ITypeSerializationDriver)>? _deferred;
        BinaryFormatter? _binaryFormatter;

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
        /// <param name="leaveOpen">True to leave the stream opened when disposing. False to close it.</param>
        /// <param name="encoding">Optional encoding for texts. Defaults to UTF-8.</param>
        /// <param name="disposedTracker">Optional collector of disposed instance. See <see cref="DisposedTracker"/>.</param>
        public BinarySerializer(
            Stream output,
            ISerializerResolver? drivers = null,
            bool leaveOpen = false,
            Encoding? encoding = null,
            Action<IDestroyable>? disposedTracker = null )
            : base( output, encoding ?? Encoding.UTF8, leaveOpen )
        {
            _types = new Dictionary<Type, TypeInfo>();
            _seen = new Dictionary<object, int>( PureObjectRefEqualityComparer<object>.Default );
            _drivers = drivers ?? SerializerRegistry.Default;
            DisposedTracker = disposedTracker;
        }

        /// <summary>
        /// Gets the serialization drivers.
        /// </summary>
        public ISerializerResolver Drivers => _drivers;

        /// <summary>
        /// Writes an object that can be null and of any type.
        /// </summary>
        /// <param name="o">The object to write.</param>
        /// <returns>
        /// True if write, false if the object has already been
        /// written and only a reference has been written.
        /// </returns>
        public bool WriteObject( object? o )
        {
            switch( o )
            {
                case null:
                    {
                        Write( (byte)SerializationMarker.Null );
                        return true;
                    }
                case string s:
                    {
                        Write( (byte)SerializationMarker.String );
                        Write( s );
                        return true;
                    }
                case int i:
                    {
                        Write( (byte)SerializationMarker.Int32 );
                        Write( i );
                        return true;
                    }
                case double d:
                    {
                        Write( (byte)SerializationMarker.Double );
                        Write( d );
                        return true;
                    }
                case char c:
                    {
                        Write( (byte)SerializationMarker.Char );
                        Write( c );
                        return true;
                    }
                case bool b:
                    {
                        Write( (byte)SerializationMarker.Boolean );
                        Write( b );
                        return true;
                    }
                case uint ui:
                    {
                        Write( (byte)SerializationMarker.UInt32 );
                        Write( ui );
                        return true;
                    }
                case float f:
                    {
                        Write( (byte)SerializationMarker.Float );
                        Write( f );
                        return true;
                    }
                case DateTime d:
                    {
                        Write( (byte)SerializationMarker.DateTime );
                        Write( d );
                        return true;
                    }
                case Guid g:
                    {
                        Write( (byte)SerializationMarker.Guid );
                        Write( g );
                        return true;
                    }
                case TimeSpan ts:
                    {
                        Write( (byte)SerializationMarker.TimeSpan );
                        Write( ts );
                        return true;
                    }
                case DateTimeOffset ds:
                    {
                        Write( (byte)SerializationMarker.DateTimeOffset );
                        Write( ds );
                        return true;
                    }
                case Type type:
                    {
                        Write( (byte)SerializationMarker.Type );
                        Write( type );
                        return true;
                    }
            }
            SerializationMarker marker;
            Type t = o.GetType();
            if( t.IsClass )
            {
                if( _seen.TryGetValue( o, out var num ) )
                {
                    Write( (byte)SerializationMarker.Reference );
                    Write( num );
                    return false;
                }
                _seen.Add( o, _seen.Count );
                if( t == typeof( object ) )
                {
                    Write( (byte)SerializationMarker.EmptyObject );
                    return true;
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
                if( _recurseCount > MaxRecurse
                    && marker == SerializationMarker.Object
                    && driver.AllowDeferred )
                {
                    if( _deferred == null ) _deferred = new Stack<(object, ITypeSerializationDriver)>( 200 );
                    _deferred.Push( (o, driver) );
                    Write( (byte)SerializationMarker.DeferredObject );
                    driver.WriteTypeInformation( this );
                }
                else
                {
                    ++_recurseCount;
                    Write( (byte)marker );
                    driver.WriteTypeInformation( this );
                    driver.WriteData( this, o );
                    --_recurseCount;
                }
                if( _recurseCount == 0 && _deferred != null )
                {
                    while( _deferred.TryPop( out var d ) )
                    {
                        ++_recurseCount;
                        d.Item2.WriteData( this, d.Item1 );
                        --_recurseCount;
                    }
                }
            }
            return true;
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
        public void DebugWriteSentinel( [CallerFilePath]string? fileName = null, [CallerLineNumber] int line = 0 )
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
        public void Write( Type? t )
        {
            ITypeSerializationDriver driver = _drivers.FindDriver( t );
            if( driver != null ) driver.WriteTypeInformation( this );
            else WriteSimpleType( t );
        }

        internal bool WriteSimpleType( Type? t )
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
        /// Called by <see cref="AutoTypeRegistry"/> serialization drivers when a disposed <see cref="IDestroyable"/> has been
        /// written.
        /// <para>
        /// This should clearly be on "ImplementationServices" or any other of this writer extensions. But currently, the
        /// serialization is embedded inside the Observable library, so we don't care.
        /// Note that if a IDestroyableObject { bool IsDestroyed { get; } } basic interface (without Destroyed event) in the "generic" serialization library
        /// (or deeper? "System.ComponentModel.IDestroyableObject, CK.Core"?), then this could remain this way. 
        /// </para>
        /// </summary>
        public Action<IDestroyable>? DisposedTracker { get; }


        internal class CheckedWriteStream : Stream
        {
            readonly byte[] _already;
            int _position;

            public CheckedWriteStream( byte[] already )
            {
                _already = already;
            }

            public override bool CanRead => false;

            public override bool CanSeek => false;

            public override bool CanWrite => true;

            public override long Length => throw new NotSupportedException();

            public override long Position { get => _position; set => throw new NotSupportedException(); }

            public override void Flush()
            {
            }

            public override int Read( byte[] buffer, int offset, int count )
            {
                throw new NotSupportedException();
            }

            public override long Seek( long offset, SeekOrigin origin )
            {
                throw new NotSupportedException();
            }

            public override void SetLength( long value )
            {
                throw new NotSupportedException();
            }

            public override void Write( byte[] buffer, int offset, int count )
            {
                for( int i = offset; i < count; ++i )
                {
                    var actual = _already[_position++];
                    if( buffer[i] != actual )
                    {
                        throw new CKException( $"Write stream differ @{_position - 1}. Expected byte '{actual}', got '{buffer[i]}'." );
                    }
                }
            }
        }

        /// <summary>
        /// Magic yet simple helper to check the serialization implementation: the object (and potentially the whole graph behind)
        /// is serialized then deserialized and the result of the deserialization is then serialized again but in a special stream
        /// that throws a <see cref="CKException"/> as soon as a byte differ.
        /// </summary>
        /// <param name="o">The object to check.</param>
        /// <param name="services">Optional services that deserialization may require.</param>
        /// <param name="throwOnFailure">False to log silently fail and return false.</param>
        /// <returns>True on success, false on error (if <paramref name="throwOnFailure"/> is false).</returns>
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
                        using( var checker = new CheckedWriteStream( originalBytes ) )
                        using( var w2 = new BinarySerializer( checker, null, true ) )
                        {
                            w2.WriteObject( o2 );
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
