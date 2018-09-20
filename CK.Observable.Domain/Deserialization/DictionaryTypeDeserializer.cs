using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    public class DictionaryTypeDeserializer<TKey,TValue> : IDeserializationDriver<Dictionary<TKey,TValue>>
    {
        readonly IDeserializationDriver<TKey> _key;
        readonly IDeserializationDriver<TValue> _value;

        public DictionaryTypeDeserializer( IDeserializationDriver<TKey> key, IDeserializationDriver<TValue> value )
        {
            if( key == null ) throw new ArgumentNullException( nameof( key ) );
            if( value == null ) throw new ArgumentNullException( nameof( value ) );
            _key = key;
            _value = value;
        }

        public string AssemblyQualifiedName => typeof( Dictionary<TKey, TValue> ).AssemblyQualifiedName;

        public Dictionary<TKey, TValue> ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  ) => DoRead( r, _key, _value );

        public static Dictionary<TKey, TValue> Read( IBinaryDeserializer r, IDeserializationDriver<TKey> key, IDeserializationDriver<TValue> value )
        {
            if( key == null ) throw new ArgumentNullException( nameof( key ) );
            if( value == null ) throw new ArgumentNullException( nameof( value ) );
            return DoRead( r, key, value );
        }

        /// <summary>
        /// Reads the content of a dictionary of <see cref="KeyValuePair{TKey, TValue}"/> that have been
        /// previously written by <see cref="DictionaryTypeSerializer{TKey, TValue}.WriteDictionaryContent(BinarySerializer, int, IEnumerable{KeyValuePair{TKey, TValue}}, ITypeSerializationDriver{TKey}, ITypeSerializationDriver{TValue})"/>.
        /// </summary>
        /// <typeparam name="TKey">Type of the key.</typeparam>
        /// <typeparam name="TValue">Type of the value.</typeparam>
        /// <param name="keyDeserialization">Key deserializer. Must not be null.</param>
        /// <param name="valueDeserialization">Value deserializer. Must not be null.</param>
        /// <returns>The dictionary content.</returns>
        public static KeyValuePair<TKey, TValue>[] ReadDictionaryContent( IBinaryDeserializer r, IDeserializationDriver<TKey> keyDeserialization, IDeserializationDriver<TValue> valueDeserialization )
        {
            if( keyDeserialization == null ) throw new ArgumentNullException( nameof( keyDeserialization ) );
            if( valueDeserialization == null ) throw new ArgumentNullException( nameof( valueDeserialization ) );
            int len = r.ReadSmallInt32();
            if( len == -1 ) return null;
            if( len == 0 ) return Array.Empty<KeyValuePair<TKey, TValue>>();
            byte b = r.ReadByte();
            bool monoTypeKey = (b & 1) != 0;
            bool monoTypeVal = (b & 2) != 0;
            var result = new KeyValuePair<TKey, TValue>[len];
            for( int i = 0; i < len; ++i )
            {
                TKey k = monoTypeKey ? keyDeserialization.ReadInstance( r, null ) : (TKey)r.ReadObject();
                TValue v = monoTypeVal ? valueDeserialization.ReadInstance( r, null ) : (TValue)r.ReadObject();
                result[i] = new KeyValuePair<TKey, TValue>( k, v );
            }
            return result;
        }


        static Dictionary<TKey, TValue> DoRead( IBinaryDeserializer r, IDeserializationDriver<TKey> key, IDeserializationDriver<TValue> value )
        {
            int version = r.ReadSmallInt32();
            if( version == -1 ) return null;
            if( version != 0 ) throw new InvalidDataException();
            var comparer = (IEqualityComparer<TKey>)r.ReadObject();
            var c = ReadDictionaryContent( r, key, value );
            Debug.Assert( c != null );
            var result = new Dictionary<TKey, TValue>( c.Length, comparer );
            if( c.Length > 0 )
            {
                r.ImplementationServices.OnPostDeserialization( () =>
                {
                    foreach( var kv in c ) result.Add( kv.Key, kv.Value );
                } );
            }
            return result;
        }

        object IDeserializationDriver.ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  ) => DoRead( r, _key, _value );
    }
}
