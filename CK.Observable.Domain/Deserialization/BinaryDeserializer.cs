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
    public class BinaryDeserializer : CKBinaryReader, IBinaryDeserializer, IBinaryDeserializerImpl, ICtorBinaryDeserializer, IBinaryDeserializerContext
    {
        readonly IDeserializerResolver _drivers;
        readonly List<string> _typesIdx;
        readonly Dictionary<string, TypeReadInfo> _typeInfos;
        readonly List<object> _objects;
        readonly List<Action> _postDeserializationActions;
        readonly Stack<ConstructorContext> _ctorContextStack;
        TypeReadInfo _currentCtorReadInfo;
        BinaryFormatter _binaryFormatter;

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

        public BinaryDeserializer(
            Stream stream,
            IServiceProvider services = null,
            IDeserializerResolver drivers = null,
            bool leaveOpen = false,
            Encoding encoding = null )
            : base( stream, encoding ?? Encoding.UTF8, leaveOpen )
        {
            Services = new SimpleServiceContainer( services );
            _typesIdx = new List<string>();
            _typeInfos = new Dictionary<string, TypeReadInfo>();
            _objects = new List<object>();
            _postDeserializationActions = new List<Action>();
            _ctorContextStack = new Stack<ConstructorContext>();
            _drivers = drivers ?? DeserializerRegistry.Default;
        }

        /// <summary>
        /// Gets a configurable container of services available for constructor
        /// injection in the deserialized instances.
        /// </summary>
        public SimpleServiceContainer Services { get; }

        IServiceProvider IBinaryDeserializer.Services => Services;

        TypeReadInfo ICtorBinaryDeserializer.CurrentReadInfo => _currentCtorReadInfo;

        void IBinaryDeserializerImpl.OnPostDeserialization( Action a )
        {
            if( a == null ) throw new ArgumentNullException();
            _postDeserializationActions.Add( a );
        }

        ICtorBinaryDeserializer IBinaryDeserializerContext.StartReading()
        {
            var head = _ctorContextStack.Peek();
            if( head != null ) _currentCtorReadInfo = head.ReadInfo.TypePath[++head.CurrentIndex];
            else _currentCtorReadInfo = null;
            return this;
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

        IBinaryDeserializerContext IBinaryDeserializerImpl.PushConstructorContext( TypeReadInfo info )
        {
            _ctorContextStack.Push( info != null ? new ConstructorContext( info ) : null );
            return this;
        }

        void IBinaryDeserializerImpl.PopConstructorContext() => _ctorContextStack.Pop();

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

        public T Read<T>( IDeserializationDriver<T> driver )
        {
            if( driver == null ) throw new ArgumentNullException( nameof( driver ) );
            var b = (SerializationMarker)ReadByte();
            switch( b )
            {
                case SerializationMarker.Null: return default( T );
                case SerializationMarker.Reference: return (T)ReadReference();
                case SerializationMarker.EmptyObject:
                    {
                        var o = new object();
                        _objects.Add( o );
                        return (T)o;
                    }
                case SerializationMarker.Object:
                case SerializationMarker.Struct:
                    {
                        return driver.ReadInstance( this, null );
                    }
                case SerializationMarker.ObjectBinaryFormatter:
                case SerializationMarker.StructBinaryFormatter:
                    {
                        if( _binaryFormatter == null ) _binaryFormatter = new BinaryFormatter();
                        object result = _binaryFormatter.Deserialize( BaseStream );
                        if( b == SerializationMarker.ObjectBinaryFormatter ) _objects.Add( result );
                        return (T)result;
                    }
                default: throw new InvalidDataException();
            }
        }


        TypeReadInfo ReadTypeReadInfo( bool isTrackedObject )
        {
            TypeReadInfo leaf;
            string sT = DoReadOneTypeName( out bool newType );
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

        protected override void Dispose( bool disposing )
        {
            base.Dispose( disposing );
            if( disposing )
            {
                ExecutePostDeserializationActions();
            }
        }

        // TODO: To be removed @next CK.Core version (transfered to CKBinaryReader).

        /// <summary>
        /// Reads a DateTime value.
        /// </summary>
        /// <returns>The DateTime read.</returns>
        public DateTime ReadDateTime()
        {
            return DateTime.FromBinary( ReadInt64() );
        }

        /// <summary>
        /// Reads a TimeSpan value.
        /// </summary>
        /// <returns>The TimeSpan read.</returns>
        public TimeSpan ReadTimeSpan()
        {
            return TimeSpan.FromTicks( ReadInt64() );
        }

        /// <summary>
        /// Reads a DateTimeOffset value.
        /// </summary>
        /// <returns>The DateTimeOffset read.</returns>
        public DateTimeOffset ReadDateTimeOffset()
        {
            return new DateTimeOffset( ReadDateTime(), TimeSpan.FromMinutes( ReadInt16() ) );
        }

        /// <summary>
        /// Reads a Guid value.
        /// </summary>
        /// <returns>The Guid read.</returns>
        public Guid ReadGuid()
        {
            return new Guid( ReadBytes( 16 ) );
        }

    }
}
