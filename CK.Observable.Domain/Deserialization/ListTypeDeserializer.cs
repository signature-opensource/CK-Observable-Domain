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

        public List<T> ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo ) => r.ReadList( _item );

        object IDeserializationDriver.ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  ) => ReadInstance( r, readInfo );
    }
}
