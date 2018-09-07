using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    public class ObjectStreamReader : CKBinaryReader
    {
        readonly BinaryDeserializer _deserializer;
        readonly List<string> _typesIdx;
        readonly Dictionary<string, TypeReadInfo> _typeInfos;
        readonly List<object> _objects;
        readonly List<Deferred> _deferredSimple;
        readonly List<Action> _deferredComplex;

        struct Deferred
        {
            public readonly Action<object> Action;
            public readonly int Number;

            public Deferred( Action<object> a, int num )
            {
                Action = a;
                Number = num;
            }
        }

        public class TypeReadInfo
        {
            /// <summary>
            /// Gets the assembly qualified name of the type.
            /// </summary>
            public string AssemblyQualifiedName { get; }

            /// <summary>
            /// Gets the version (greater or equal to 0) if this type information has been serialized
            /// by the type itself. -1 otherwise.
            /// </summary>
            public readonly int Version;

            /// <summary>
            /// Gets the base type infromation.
            /// Not null only if this type information has been serialized by the type itself and if the
            /// type was a specialized class.
            /// </summary>
            public TypeReadInfo BaseType => _baseType;

            /// <summary>
            /// Gets the types from the root inherited type (excluding Object) down to this one.
            /// Not null only if this type information has been serialized by the type itself.
            /// When not null, the list ends with this <see cref="TypeReadInfo"/> itself.
            /// </summary>
            public IReadOnlyList<TypeReadInfo> TypePath => _typePath;

            /// <summary>
            /// Gets the Type if it can be resolved locally, null otherwise.
            /// </summary>
            public Type LocalType
            {
                get
                {
                    if( !_localTypeLookupDone )
                    {
                        _localTypeLookupDone = true;
                        _localType = SimpleTypeFinder.WeakResolver( AssemblyQualifiedName, false );
                    }
                    return _localType;
                }
            }

            /// <summary>
            /// Gets the deserialization driver if it can be resolved, null otherwise.
            /// </summary>
            public IDeserializationDriver DeserializationDriver
            {
                get
                {
                    if( !_driverLookupDone )
                    {
                        _driverLookupDone = true;
                        _driver = UnifiedTypeRegistry.FindDeserializationDriver( AssemblyQualifiedName, () => LocalType );
                    }
                    return _driver;
                }
            }

            TypeReadInfo _baseType;
            TypeReadInfo[] _typePath;
            Type _localType;
            IDeserializationDriver _driver;
            bool _localTypeLookupDone;
            bool _driverLookupDone;

            internal TypeReadInfo( string t, int version )
            {
                AssemblyQualifiedName = t;
                Version = version;
            }

            internal void SetBaseType( TypeReadInfo b )
            {
                _baseType = b;
            }

            internal TypeReadInfo[] EnsureTypePath()
            {
                Debug.Assert( Version >= 0, "Must be called only for TypeBased serialization." );
                if( _typePath == null )
                {
                    if( _baseType != null )
                    {
                        var basePath = _baseType.EnsureTypePath();
                        var p = new TypeReadInfo[basePath.Length + 1];
                        Array.Copy( basePath, p, basePath.Length );
                        p[basePath.Length] = this;
                        _typePath = p;
                    }
                    else _typePath = new[] { this };
                }
                return _typePath;
            }
        }

        internal ObjectStreamReader( BinaryDeserializer d, Stream stream, bool leaveOpen = false, Encoding encoding = null )
            : base( stream, encoding ?? Encoding.UTF8, leaveOpen )
        {
            _deserializer = d;
            _typesIdx = new List<string>();
            _typeInfos = new Dictionary<string, TypeReadInfo>();
            _objects = new List<object>();
            _deferredSimple = new List<Deferred>();
            _deferredComplex = new List<Action>();
        }

        internal void ExecuteDeferredActions()
        {
            foreach( var d in _deferredSimple )
            {
                d.Action( _objects[d.Number] );
            }
            foreach( var action in _deferredComplex )
            {
                action();
            }
        }

        /// <summary>
        /// Get the type based information as it has been written.
        /// If the object has been written by an external driver, this is null.
        /// </summary>
        public TypeReadInfo CurrentReadInfo { get; internal set; }

        public void ReadObject<T>( Action<T> onRead )
        {
            ReadObject( o => onRead( (T)o ) );
        }

        public bool ReadObjects( Action<object, object> onRead )
        {
            object result1 = DoReadObject( out bool resolved1, out int idx1 );
            object result2 = DoReadObject( out bool resolved2, out int idx2 );
            if( resolved1 && resolved2 )
            {
                onRead( result1, result2 );
                return true;
            }
            _deferredComplex.Add( () => onRead( _objects[idx1], _objects[idx2] ) );
            return false;
        }

        public bool ReadObjects( Action<object, object, object> onRead )
        {
            object result1 = DoReadObject( out bool resolved1, out int idx1 );
            object result2 = DoReadObject( out bool resolved2, out int idx2 );
            object result3 = DoReadObject( out bool resolved3, out int idx3 );
            if( resolved1 && resolved2 && resolved3 )
            {
                onRead( result1, result2, result3 );
                return true;
            }
            _deferredComplex.Add( () => onRead( _objects[idx1], _objects[idx2], _objects[idx3] ) );
            return false;
        }

        public bool ReadObjects( Action<object, object, object, object> onRead )
        {
            object result1 = DoReadObject( out bool resolved1, out int idx1 );
            object result2 = DoReadObject( out bool resolved2, out int idx2 );
            object result3 = DoReadObject( out bool resolved3, out int idx3 );
            object result4 = DoReadObject( out bool resolved4, out int idx4 );
            if( resolved1 && resolved2 && resolved3 && resolved4 )
            {
                onRead( result1, result2, result3, result4 );
                return true;
            }
            _deferredComplex.Add( () => onRead( _objects[idx1], _objects[idx2], _objects[idx3], _objects[idx4] ) );
            return false;
        }

        public bool ReadObjects( Action<object, object, object, object, object> onRead )
        {
            object result1 = DoReadObject( out bool resolved1, out int idx1 );
            object result2 = DoReadObject( out bool resolved2, out int idx2 );
            object result3 = DoReadObject( out bool resolved3, out int idx3 );
            object result4 = DoReadObject( out bool resolved4, out int idx4 );
            object result5 = DoReadObject( out bool resolved5, out int idx5 );
            if( resolved1 && resolved2 && resolved3 && resolved4 && resolved5 )
            {
                onRead( result1, result2, result3, result4, result5 );
                return true;
            }
            _deferredComplex.Add( () => onRead( _objects[idx1], _objects[idx2], _objects[idx3], _objects[idx4], _objects[idx5] ) );
            return false;
        }

        public bool ReadObject( Action<object> a )
        {
            object result = DoReadObject( out bool resolved, out int idx );
            if( !resolved )
            {
                _deferredSimple.Add( new Deferred( a, idx ) );
            }
            else a( result );
            return resolved;
        }

        object DoReadObject( out bool resolved, out int idx )
        {
            resolved = true;
            idx = -1;
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

                case SerializationMarker.Reference:
                    {
                        idx = ReadInt32();
                        if( idx >= _objects.Count || _objects[idx] == null )
                        {
                            resolved = false;
                            return null;
                        }
                        return _objects[idx];
                    }
            }
            Debug.Assert( b == SerializationMarker.EmptyObject || b == SerializationMarker.Object );
            idx = ReadInt32();
            if( idx >= 0 ) _objects.Add( null );
            object result;
            if( b == SerializationMarker.EmptyObject ) result = new object();
            else
            {
                var info = ReadTypeReadInfo();
                var d = info.DeserializationDriver;
                if( d == null )
                {
                    throw new InvalidOperationException( $"Unable to find a deserialization driver for Assembly Qualified Name '{info.AssemblyQualifiedName}'." );
                }
                result = d.ReadInstance( _deserializer, info );
            }
            Debug.Assert( result.GetType().IsClass == !(result is ValueType) );
            if( idx >= 0 ) _objects[idx] = result;           
            return result;
        }

        public TypeReadInfo ReadTypeReadInfo()
        {
            TypeReadInfo leaf;
            string sT = DoReadOneTypeName( out bool newType );
            if( newType )
            {
                int version = ReadSmallInt32();
                leaf = new TypeReadInfo( sT, version );
                var current = leaf;
                _typeInfos.Add( sT, leaf );
                if( version >= 0 )
                {
                    string parent = DoReadOneTypeName( out newType );
                    while( parent != null )
                    {
                        if( newType )
                        {
                            var pInfo = new TypeReadInfo( parent, ReadSmallInt32() );
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

        // TODO: transfer thess helpers to CKBinaryReader.

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
