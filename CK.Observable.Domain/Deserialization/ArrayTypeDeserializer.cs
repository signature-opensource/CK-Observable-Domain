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

        public T[] ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  )
        {
            bool monoType = r.ReadBoolean();
            if( monoType )
            {
                var len = r.ReadSmallInt32();
                if( len == -1 ) return null;
                if( len == 0 ) return Array.Empty<T>();
                var result = new T[len];
                for( int i = 0; i < result.Length; ++i )
                {
                    result[i] = _item.ReadInstance( r, null ); 
                }
                return result;
            }
            return r.ReadObjectArray<T>();
        }

        object IDeserializationDriver.ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  ) => ReadInstance( r, readInfo );
    }
}
