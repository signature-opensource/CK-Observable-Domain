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
        readonly Deserializer _deserializer;
        readonly List<Type> _typesIdx;
        readonly Dictionary<Type, TypeBasedInfo> _typeBasedInfos;
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

        public class TypeBasedInfo
        {
            public readonly Type Type;
            public readonly int Version;
            TypeBasedInfo _baseType;
            TypeBasedInfo[] _typePath;

            public TypeBasedInfo BaseType => _baseType;

            /// <summary>
            /// Gets the types from the base types (excluding Object) up to this one.
            /// </summary>
            public IReadOnlyList<TypeBasedInfo> TypePath => _typePath;

            internal TypeBasedInfo( Type t, int version )
            {
                Type = t;
                Version = version;
            }

            internal void SetBaseType( TypeBasedInfo b )
            {
                _baseType = b;
            }

            internal TypeBasedInfo[] EnsureTypePath()
            {
                if( _typePath == null )
                {
                    if( _baseType != null )
                    {
                        var basePath = _baseType.EnsureTypePath();
                        var p = new TypeBasedInfo[basePath.Length + 1];
                        Array.Copy( basePath, p, basePath.Length );
                        p[basePath.Length] = this;
                        _typePath = p;
                    }
                    else _typePath = new[] { this };
                }
                return _typePath;
            }
        }

        internal ObjectStreamReader( Deserializer d, Stream stream, bool leaveOpen = false, Encoding encoding = null )
            : base( stream, encoding ?? Encoding.UTF8, leaveOpen )
        {
            _deserializer = d;
            _typesIdx = new List<Type>();
            _typeBasedInfos = new Dictionary<Type, TypeBasedInfo>();
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
        public TypeBasedInfo CurrentReadInfo { get; internal set; }

        public void ReadObject<T>( Action<T> assign )
        {
            ReadObject( o => assign( (T)o ) );
        }

        public bool ReadObjects( Action<object,object> a )
        {
            object result1 = DoReadObject( out bool resolved1, out int idx1 );
            object result2 = DoReadObject( out bool resolved2, out int idx2 );
            if( resolved1 && resolved2 )
            {
                a( result1, result2 );
                return true;
            }
            _deferredComplex.Add( () => a( _objects[idx1], _objects[idx2] ) );
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
                case SerializationMarker.UInt32: return ReadUInt32();
                case SerializationMarker.Float: return ReadSingle();
                case SerializationMarker.DateTime: return DateTime.FromBinary( ReadInt64() );
                case SerializationMarker.Guid: return new Guid( ReadBytes( 16 ) );

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
                Type t = ReadType();
                var driver = SerializableTypes.FindDriver( t, TypeSerializationKind.Serializable );
                if( driver == null )
                {
                    throw new InvalidDataException( $"Read type {t.Name} is no more serializable." );
                }
                // Gets the TypeBase informations if this obect was written by an external driver,
                // this is null.
                _typeBasedInfos.TryGetValue( t, out var readInfo );
                result = driver.ReadInstance( _deserializer, readInfo );
            }
            Debug.Assert( result.GetType().IsClass == !(result is ValueType) );
            if( idx >= 0 ) _objects[idx] = result;           
            return result;
        }

        public Type ReadType()
        {
            Type t = DoReadSimpleType( out bool newType );
            if( newType )
            {
                int version = ReadSmallInt32();
                if( version >= 0 )
                {
                    var leaf = new TypeBasedInfo( t, version );
                    _typeBasedInfos.Add( t, leaf );
                    var current = leaf;
                    Type parent = DoReadSimpleType( out newType );
                    while( parent != null )
                    {
                        if( newType )
                        {
                            var pInfo = new TypeBasedInfo( parent, ReadSmallInt32() );
                            current.SetBaseType( pInfo );
                            _typeBasedInfos.Add( parent, pInfo );
                            current = pInfo;
                            parent = DoReadSimpleType( out newType );
                        }
                        else
                        {
                            current.SetBaseType( _typeBasedInfos[parent] );
                            break;
                        }
                    }
                    leaf.EnsureTypePath();
                }
            }
            return t;
        }
        
        Type DoReadSimpleType( out bool newType )
        {
            newType = false;
            switch( ReadByte() )
            {
                case 0: return null;
                case 1: return typeof( object );
                case 2:
                    {
                        var t = SimpleTypeFinder.WeakResolver( ReadString(), true );
                        _typesIdx.Add( t );
                        newType = true;
                        return t;
                    }
                case 3: return _typesIdx[ReadNonNegativeSmallInt32()];
                default: throw new InvalidDataException();
            }
        }




    }
}
