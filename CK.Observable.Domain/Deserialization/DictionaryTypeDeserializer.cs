using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    class DictionaryTypeDeserializer<TKey,TValue> : IDeserializationDriver<Dictionary<TKey,TValue>>
    {
        readonly IDeserializationDriver<TKey> _key;
        readonly IDeserializationDriver<TValue> _value;

        public DictionaryTypeDeserializer( IDeserializationDriver<TKey> key, IDeserializationDriver<TValue> value )
        {
            Debug.Assert( key != null );
            Debug.Assert( value != null );
            _key = key;
            _value = value;
        }

        public string AssemblyQualifiedName => typeof( Dictionary<TKey, TValue> ).AssemblyQualifiedName;

        public Dictionary<TKey, TValue> ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  )
        {
            int len = r.ReadSmallInt32();
            if( len == -1 ) return null;
            var result = new Dictionary<TKey, TValue>();
            if( len > 0 )
            {

            }
            return result;
        }

        object IDeserializationDriver.ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  ) => ReadInstance( r, readInfo );
    }
}
