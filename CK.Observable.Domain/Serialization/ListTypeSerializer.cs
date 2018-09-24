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

        void ITypeSerializationDriver<List<T>>.WriteData( BinarySerializer w, List<T> o ) => DoWriteData( w, o );

        void DoWriteData( BinarySerializer w, List<T> o ) => ArraySerializer<T>.WriteObjects( w, o?.Count ?? 0, o, _itemSerializer );

        void ITypeSerializationDriver.WriteData( BinarySerializer w, object o ) => DoWriteData( w, (List<T>)o );

        public void WriteTypeInformation( BinarySerializer s ) => s.WriteSimpleType( Type, null );

    }
}
