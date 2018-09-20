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

        public T[] ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  ) => r.ReadArray( _item );

        object IDeserializationDriver.ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  ) => ReadInstance( r, readInfo );
    }
}
