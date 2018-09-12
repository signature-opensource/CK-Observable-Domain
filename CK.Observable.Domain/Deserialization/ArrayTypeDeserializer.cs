using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    class ArrayTypeDeserializer<T> : IDeserializationDriver<T[]>
    {
        readonly IDeserializationDriver<T> _item;

        public ArrayTypeDeserializer( IDeserializationDriver<T> item )
        {
            Debug.Assert( item != null );
            _item = item;
        }

        public string AssemblyQualifiedName => typeof(T[]).AssemblyQualifiedName;

        public T[] ReadInstance( BinaryDeserializer r, ObjectStreamReader.TypeReadInfo readInfo )
        {
            bool monoType = r.Reader.ReadBoolean();
            if( monoType )
            {
                var len = r.Reader.ReadSmallInt32();
                if( len == -1 ) return null;
                if( len == 0 ) return Array.Empty<T>();
                var result = new T[len];
                for( int i = 0; i < result.Length; ++i )
                {
                    result[i] = _item.ReadInstance( r, null ); 
                }
            }
            else
            {
                for( int i = 0; i < result.Length; ++i )
                {
                    result[i] = r.Reader.ReadObjects( r, null );
                }

            }
        }

        object IDeserializationDriver.ReadInstance( BinaryDeserializer r, ObjectStreamReader.TypeReadInfo readInfo ) => ReadInstance( r, readInfo );
    }
}
