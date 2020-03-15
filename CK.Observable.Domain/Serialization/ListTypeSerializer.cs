using System;
using System.Collections.Generic;
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace CK.Observable
{
    class ListTypeSerializer<T> : ITypeSerializationDriver<List<T>>
    {
        readonly ITypeSerializationDriver<T> _itemSerializer;

        public ListTypeSerializer( ITypeSerializationDriver<T> itemSerializer )
        {
            _itemSerializer = itemSerializer;
        }

        public Type Type => typeof(List<T>);

        bool ITypeSerializationDriver.IsFinalType => false;

        void ITypeSerializationDriver<List<T>>.WriteData( BinarySerializer w, List<T> o ) => DoWriteData( w, o );

        void DoWriteData( BinarySerializer w, List<T> o ) => ArraySerializer<T>.WriteObjects( w, o?.Count ?? 0, o, _itemSerializer );

        void ITypeSerializationDriver.WriteData( BinarySerializer w, object o ) => DoWriteData( w, (List<T>)o );

        public void WriteTypeInformation( BinarySerializer s ) => s.WriteSimpleType( Type );

    }
}
