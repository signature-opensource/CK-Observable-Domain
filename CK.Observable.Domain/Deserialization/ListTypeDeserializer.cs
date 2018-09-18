using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    class ListTypeDeserializer<T> : IDeserializationDriver<List<T>>
    {
        readonly IDeserializationDriver<T> _item;

        public ListTypeDeserializer( IDeserializationDriver<T> item )
        {
            Debug.Assert( item != null );
            _item = item;
        }

        public string AssemblyQualifiedName => typeof(List<T>).AssemblyQualifiedName;

        public List<T> ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  )
        {
            var len = r.ReadSmallInt32();
            if( len == -1 ) return null;
            bool monoType = r.ReadBoolean();
            if( monoType )
            {
                var result = new List<T>(len);
                if( len > 0 )
                {
                    for( int i = 0; i < len; ++i )
                    {
                        result.Add( _item.ReadInstance( r, null ) );
                    }
                }
                return result;
            }
            return r.ReadObjectList<T>();
        }

        object IDeserializationDriver.ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  ) => ReadInstance( r, readInfo );
    }
}
