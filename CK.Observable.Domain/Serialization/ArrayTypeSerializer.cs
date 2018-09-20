using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    class ArrayTypeSerializer<T> : ITypeSerializationDriver<T[]>
    {
        readonly ITypeSerializationDriver<T> _itemSerializer;

        public ArrayTypeSerializer( ITypeSerializationDriver<T> itemSerializer )
        {
            Debug.Assert( itemSerializer != null );
            _itemSerializer = itemSerializer;
        }

        public Type Type => typeof(T[]);

        public void WriteData( BinarySerializer w, T[] o ) => w.WriteListContent( o?.Length ?? 0, o, _itemSerializer );

        public void WriteData( BinarySerializer w, object o ) => WriteData( w, (T[])o );

        public void WriteTypeInformation( BinarySerializer s ) => s.WriteSimpleType( Type, null );
        
    }
}
