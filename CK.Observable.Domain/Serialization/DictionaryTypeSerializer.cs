using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    class DictionaryTypeSerializer<TKey,TValue> : ITypeSerializationDriver<Dictionary<TKey, TValue>>
    {
        readonly ITypeSerializationDriver<TKey> _key;
        readonly ITypeSerializationDriver<TValue> _value;

        public DictionaryTypeSerializer( ITypeSerializationDriver<TKey> key, ITypeSerializationDriver<TValue> value )
        {
            Debug.Assert( key != null );
            Debug.Assert( value != null );
            _key = key;
            _value = value;
        }

        public Type Type => typeof( Dictionary<TKey, TValue> );

        public void WriteData( BinarySerializer w, Dictionary<TKey, TValue> o )
        {
            w.WriteDictionaryContent( o?.Count ?? 0, o, _key, _value );
        }

        public void WriteData( BinarySerializer w, object o ) => WriteData( w, (Dictionary<TKey,TValue>)o );

        public void WriteTypeInformation( BinarySerializer s ) => s.WriteSimpleType( Type );

    }
}
