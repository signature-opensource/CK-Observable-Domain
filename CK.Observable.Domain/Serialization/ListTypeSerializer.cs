using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    class ListTypeSerializer<T> : ITypeSerializationDriver<List<T>>
    {
        readonly ITypeSerializationDriver<T> _itemSerializer;

        public ListTypeSerializer( ITypeSerializationDriver<T> itemSerializer )
        {
            Debug.Assert( itemSerializer != null );
            _itemSerializer = itemSerializer;
        }

        public Type Type => typeof(List<T>);

        public void WriteData( BinarySerializer w, List<T> o ) => w.WriteListContent( o?.Count ?? 0, o, _itemSerializer );

        public void WriteData( BinarySerializer w, object o ) => WriteData( w, (List<T>)o );

        public void WriteTypeInformation( BinarySerializer s ) => s.WriteSimpleType( Type, null );

    }
}
