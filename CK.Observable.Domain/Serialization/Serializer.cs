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
    public class Serializer : CKBinaryWriter
    {
        readonly Dictionary<Type, TypeInfo> _types;
        readonly Dictionary<object, int> _seen;

        class PureRefEquality : IEqualityComparer<object>
        {
            public new bool Equals( object x, object y ) => ReferenceEquals( x, y );

            public int GetHashCode( object obj ) => obj.GetHashCode();
        }

        static readonly PureRefEquality RefEquality = new PureRefEquality();

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

        internal Serializer( Stream output, bool leaveOpen, Encoding encoding = null )
            : base( output, encoding ?? Encoding.UTF8, leaveOpen )
        {
            _types = new Dictionary<Type, TypeInfo>();
            _seen = new Dictionary<object, int>( RefEquality );
        }

        public void WriteObject( object o )
        {
            if( o == null )
            {
                Write( (byte)0 );
                return;
            }
            Type t = o.GetType();
            int idxSeen = -1;
            if( t.IsClass )
            {
                if( _seen.TryGetValue( o, out var num ) )
                {
                    Write( (byte)1 );
                    Write( num );
                    return;
                }
                idxSeen = _seen.Count;
                _seen.Add( o, _seen.Count );
                if( t == typeof( object ) )
                {
                    Write( (byte)2 );
                    Write( idxSeen );
                    return;
                }
            }
            Write( (byte)3 );
            Write( idxSeen );
            var driver = SerializableTypes.FindDriver( t, TypeSerializationKind.Serializable );
            Debug.Assert( driver != null );
            driver.WriteTypeInformation( this );
            driver.WriteData( this, o );
        }

        public void WriteType( Type t )
        {
            var driver = SerializableTypes.FindDriver( t, TypeSerializationKind.None );
            if( driver != null )
            {
                if( driver is SerializableTypes.TypeInfo typeBased )
                {
                    DoWriteSerializableType( typeBased );
                }
                else
                {
                    if( DoWriteSimpleType( t ) ) WriteSmallInt32( -1 );
                }
            }
        }

        internal void DoWriteSerializableType( SerializableTypes.TypeInfo tInfo )
        {
            Debug.Assert( tInfo != null );
            while( DoWriteSimpleType( tInfo?.Type ) )
            {
                WriteSmallInt32( tInfo.Version );
                tInfo = tInfo.BaseType;
            }
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


    }
}
