using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace CK.Observable
{
    /// <summary>
    /// Deserializer for complex object graph.
    /// </summary>
    public partial class BinaryDeserializer : CKBinaryReader, IBinaryDeserializer, IBinaryDeserializerImpl
    {
        readonly IDeserializerResolver _drivers;
        readonly TypeReadInfo _objectReadTypeInfo;
        readonly List<string> _typesIdx;
        readonly Dictionary<string, TypeReadInfo> _typeInfos;
        readonly List<object> _objects;

        readonly List<Action> _postDeserializationActions;
        readonly Stack<ConstructorContext?> _ctorContextStack;
        BinaryFormatter? _binaryFormatter;

        int _debugModeCounter;
        int _debugSentinel;
        string? _lastWriteSentinel;
        string? _lastReadSentinel;

        class ConstructorContext
        {
            public readonly TypeReadInfo ReadInfo;
            public int CurrentIndex;

            public ConstructorContext( TypeReadInfo readInfo )
            {
                CurrentIndex = -1;
                ReadInfo = readInfo;
            }
        }

        /// <summary>
        /// Initializes a new <see cref="BinarySerializer"/> on a stream.
        /// </summary>
        /// <param name="stream">The stream to deserialize.</param>
        /// <param name="services">Optional services.</param>
        /// <param name="drivers"></param>
        /// <param name="leaveOpen"></param>
        /// <param name="encoding"></param>
        public BinaryDeserializer(
            Stream stream,
            IServiceProvider? services = null,
            IDeserializerResolver? drivers = null,
            bool leaveOpen = false,
            Encoding? encoding = null )
            : base( stream, encoding ?? Encoding.UTF8, leaveOpen )
        {
            Services = new SimpleServiceContainer( services );
            _typesIdx = new List<string>();
            _typeInfos = new Dictionary<string, TypeReadInfo>();
            _objects = new List<object>();
            _postDeserializationActions = new List<Action>();
            _ctorContextStack = new Stack<ConstructorContext?>();
            _drivers = drivers ?? DeserializerRegistry.Default;
            _objectReadTypeInfo = new TypeReadInfo( _drivers );
        }

        /// <summary>
        /// Gets a configurable container of services available for constructor
        /// injection in the deserialized instances.
        /// </summary>
        public SimpleServiceContainer Services { get; }

        IServiceProvider IBinaryDeserializer.Services => Services;

        void IBinaryDeserializerImpl.OnPostDeserialization( Action a )
        {
            if( a == null ) throw new ArgumentNullException();
            _postDeserializationActions.Add( a );
        }

        /// <summary>
        /// Gets a set of low level methods and helpers.
        /// </summary>
        public IBinaryDeserializerImpl ImplementationServices => this;

        IDeserializerResolver IBinaryDeserializerImpl.Drivers => _drivers;

        object IBinaryDeserializerImpl.CreateUninitializedInstance( Type t, bool isTrackedObject )
        {
            var o = System.Runtime.Serialization.FormatterServices.GetUninitializedObject( t );
            if( isTrackedObject ) _objects.Add( o );
            return o;
        }

        T IBinaryDeserializerImpl.TrackObject<T>( T o )
        {
            if( o == null ) throw new ArgumentException( nameof( o ) );
            _objects.Add( o );
            return o;
        }

        int IBinaryDeserializerImpl.PreTrackObject()
        {
            _objects.Add( null );
            return _objects.Count - 1;
        }

        T IBinaryDeserializerImpl.TrackPreTrackedObject<T>( T o, int num )
        {
            if( o == null ) throw new ArgumentException( nameof( o ) );
            _objects[num] = o;
            return o;
        }

        void IBinaryDeserializerImpl.ExecutePostDeserializationActions() => ExecutePostDeserializationActions();

        void ExecutePostDeserializationActions()
        {
            foreach( var action in _postDeserializationActions )
            {
                action();
            }
            _postDeserializationActions.Clear();
        }

        /// <summary>
        /// Gets or sets the global serialization version (<see cref="IBinaryDeserializer.SerializationVersion"/>).
        /// This should be set once at the start of the read.
        /// </summary>
        public int SerializationVersion { get; set; }

        /// <summary>
        /// Gets whether this deserializer is currently in debug mode.
        /// </summary>
        public bool IsDebugMode => _debugModeCounter > 0;

        /// <summary>
        /// Updates the current debug mode that must have been written by <see cref="BinarySerializer.DebugWriteMode(bool?)"/>.
        /// </summary>
        /// <returns>Whether the debug mode is currently active or not.</returns>
        public bool DebugReadMode()
        {
            switch( ReadByte() )
            {
                case 182: ++_debugModeCounter; break;
                case 181: --_debugModeCounter; break;
                case 180: break;
                default: throw new InvalidDataException( $"Expected DebugMode byte marker." );
            }
            return IsDebugMode;
        }

        /// <summary>
        /// Checks the existence of a sentinel written by <see cref="BinarySerializer.DebugWriteSentinel"/>.
        /// An <see cref="InvalidDataException"/> is thrown if <see cref="IsDebugMode"/> is true and the sentinel cannot be read.
        /// </summary>
        /// <param name="fileName">Current file name used to build the <see cref="InvalidDataException"/> message if sentinel cannot be read back.</param>
        /// <param name="line">Current line number used to build the <see cref="InvalidDataException"/> message if sentinel cannot be read back.</param>
        public void DebugCheckSentinel( [CallerFilePath]string? fileName = null, [CallerLineNumber] int line = 0 )
        {
            if( !IsDebugMode ) return;
            bool success = false;
            Exception? e = null;
            try
            {
                success = ReadInt32() == 987654321
                          && ReadInt32() == _debugSentinel
                          && (_lastWriteSentinel = ReadString()) != null;
            }
            catch( Exception ex )
            {
                e = ex;
            }
            if( !success )
            {
                var msg = $"Sentinel check failure: expected reading sentinel n°{_debugSentinel} at {fileName}({line}). Last successful sentinel was written at {_lastWriteSentinel} and read at {_lastReadSentinel}.";
                throw new InvalidDataException( msg, e );
            }
            ++_debugSentinel;
            _lastReadSentinel = fileName + '(' + line.ToString() + ')';
        }

        /// <summary>
        /// Reads an expected string and throws an <see cref="InvalidDataException"/> if it cannot be read.
        /// This is typically used if (and when) <see cref="IsDebugMode"/> is true but can be used independently.
        /// </summary>
        /// <param name="expected">The expected string to read. It cannot be null, empty or whitespace.</param>
        public void ReadString( string expected )
        {
            if( String.IsNullOrWhiteSpace( expected ) ) throw new ArgumentException( "Must not be empty or whitespace.", nameof(expected) );
            string? read = null;
            Exception? e = null;
            try
            {
                read = ReadString();
            }
            catch( Exception ex )
            {
                e = ex;
            }
            if( read != expected )
            {
                var msg = $"Read string failure: expected string '{expected}' but got '{(e!=null?"<see the inner exception>" : read)}'.";
                throw new InvalidDataException( msg, e );
            }
        }

        /// <summary>
        /// Reads an object previously written by <see cref="BinarySerializer.WriteObject(object)"/>.
        /// </summary>
        /// <returns>The object read, possibly in an intermediate state.</returns>
        public object ReadObject()
        {
            var b = (SerializationMarker)ReadByte();
            switch( b )
            {
                case SerializationMarker.Null: return null;
                case SerializationMarker.String: return ReadString();
                case SerializationMarker.Int32: return ReadInt32();
                case SerializationMarker.Double: return ReadDouble();
                case SerializationMarker.Char: return ReadChar();
                case SerializationMarker.Boolean: return ReadBoolean();
                case SerializationMarker.UInt32: return ReadUInt32();
                case SerializationMarker.Float: return ReadSingle();
                case SerializationMarker.DateTime: return ReadDateTime();
                case SerializationMarker.Guid: return ReadGuid();
                case SerializationMarker.TimeSpan: return ReadTimeSpan();
                case SerializationMarker.DateTimeOffset: return ReadDateTimeOffset();
                case SerializationMarker.Reference: return ReadReference();
                case SerializationMarker.Type: return ReadType();
            }
            Debug.Assert( b == SerializationMarker.EmptyObject
                          || b == SerializationMarker.ObjectBinaryFormatter
                          || b == SerializationMarker.StructBinaryFormatter
                          || b == SerializationMarker.Object
                          || b == SerializationMarker.Struct );
            object result;
            if( b == SerializationMarker.EmptyObject )
            {
                _objects.Add( result = new object() );
            }
            else if( b == SerializationMarker.ObjectBinaryFormatter || b == SerializationMarker.StructBinaryFormatter )
            {
                if( _binaryFormatter == null ) _binaryFormatter = new BinaryFormatter();
                result = _binaryFormatter.Deserialize( BaseStream );
                if( b == SerializationMarker.ObjectBinaryFormatter )
                {
                    _objects.Add( result );
                }
            }
            else 
            {
                var info = ReadTypeReadInfo( b == SerializationMarker.Object );
                var d = info.GetDeserializationDriver( _drivers );
                if( d == null )
                {
                    throw new InvalidOperationException( $"Unable to find a deserialization driver for Assembly Qualified Name '{info.TypeName}'." );
                }
                result = d.ReadInstance( this, info );
            }
            Debug.Assert( result.GetType().IsClass == !(result is ValueType) );
            return result;
        }

        object ReadReference()
        {
            int idx = ReadInt32();
            if( idx >= _objects.Count )
            {
                throw new InvalidDataException( $"Unable to resolve reference {idx}. Current is {_objects.Count}." );
            }
            if( _objects[idx] == null )
            {
                throw new InvalidDataException( $"Unable to resolve reference {idx}. Object has not been created or has not been registered." );
            }
            return _objects[idx];
        }

        /// <summary>
        /// Reads a <see cref="Type"/> written by <see cref="BinarySerializer.Write(Type)"/>.
        /// </summary>
        /// <param name="throwIfMissing">
        /// By default a <see cref="TypeLoadException"/> is thrown if the Type cannot be resolved.
        /// False to simply return null.
        /// </param>
        /// <returns>The Type or null if <paramref name="throwIfMissing"/> is false and the type is null or cannot be resolved.</returns>
        public Type? ReadType( bool throwIfMissing = true )
        {
            var info = ReadTypeReadInfo( true );
            if( info == null ) return null;
            var t = info.LocalType;
            if( t == null && throwIfMissing ) throw new TypeLoadException( $"Unable to locally resolve the Type: '{info.TypeName}'." );
            return t;
        }

        TypeReadInfo? ReadTypeReadInfo( bool isTrackedObject )
        {
            TypeReadInfo leaf;
            string sT = DoReadOneTypeName( out bool newType );
            if( sT == null ) return null;
            if( sT.Length == 0 ) return _objectReadTypeInfo;
            if( newType )
            {
                int version = ReadSmallInt32();
                leaf = new TypeReadInfo( sT, version, isTrackedObject );
                var current = leaf;
                _typeInfos.Add( sT, leaf );
                if( version >= 0 )
                {
                    string parent = DoReadOneTypeName( out newType );
                    while( parent != null )
                    {
                        if( newType )
                        {
                            var pInfo = new TypeReadInfo( parent, ReadSmallInt32(), isTrackedObject );
                            current.SetBaseType( pInfo );
                            _typeInfos.Add( parent, pInfo );
                            current = pInfo;
                            parent = DoReadOneTypeName( out newType );
                        }
                        else
                        {
                            current.SetBaseType( _typeInfos[parent] );
                            break;
                        }
                    }
                    leaf.EnsureTypePath();
                }
            }
            else leaf = _typeInfos[sT];
            return leaf;
        }

        string DoReadOneTypeName( out bool newType )
        {
            newType = false;
            switch( ReadByte() )
            {
                case 0: return null;
                case 1: return String.Empty;
                case 2:
                    {
                        var t = ReadString();
                        _typesIdx.Add( t );
                        newType = true;
                        return t;
                    }
                case 3: return _typesIdx[ReadNonNegativeSmallInt32()];
                default: throw new InvalidDataException();
            }
        }

        /// <summary>
        /// Dispose overload that, when <paramref name="disposing"/> is true, ensures
        /// that post actions are called.
        /// </summary>
        /// <param name="disposing">True when coming from Dispose method, false from the finalizer.</param>
        protected override void Dispose( bool disposing )
        {
            base.Dispose( disposing );
            if( disposing )
            {
                ExecutePostDeserializationActions();
            }
        }

    }
}
