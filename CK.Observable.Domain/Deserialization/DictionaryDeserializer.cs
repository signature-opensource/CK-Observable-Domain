using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace CK.Observable
{

    public class DictionaryDeserializer<TKey,TValue> : IDeserializationDriver<Dictionary<TKey,TValue>>
    {
        readonly IDeserializationDriver<TKey> _key;
        readonly IDeserializationDriver<TValue> _value;

        public DictionaryDeserializer( IDeserializationDriver<TKey> key, IDeserializationDriver<TValue> value )
        {
            _key = key;
            _value = value;
        }

        public string AssemblyQualifiedName => typeof( Dictionary<TKey, TValue> ).AssemblyQualifiedName;

        public Dictionary<TKey, TValue> ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  ) => DoRead( r, _key, _value );

        /// <summary>
        /// Reads a dictionary previously written by <see cref="DictionarySerializer{TKey, TValue}.WriteDictionaryContent(BinarySerializer, int, IEnumerable{KeyValuePair{TKey, TValue}}, ITypeSerializationDriver{TKey}, ITypeSerializationDriver{TValue})"/>.
        /// </summary>
        /// <param name="r">The binary serializer. Must not be null.</param>
        /// <param name="key">Already available key deserializer if known.</param>
        /// <param name="value">Already available value deserializer if known.</param>
        /// <returns>The dictionary.</returns>
        public static Dictionary<TKey, TValue>? Read( IBinaryDeserializer r, IDeserializationDriver<TKey> key = null, IDeserializationDriver<TValue> value = null )
        {
            if( r == null ) throw new ArgumentNullException( nameof( r ) );
            return DoRead( r, key, value );
        }

        static Dictionary<TKey, TValue>? DoRead( IBinaryDeserializer r, IDeserializationDriver<TKey> key, IDeserializationDriver<TValue> value )
        {
            int version = r.ReadSmallInt32();
            if( version == -1 ) return null;
            if( version != 0 ) throw new InvalidDataException();
            int num = r.ImplementationServices.PreTrackObject();
            var comparer = (IEqualityComparer<TKey>?)r.ReadObject();
            var c = ReadDictionaryContent( r, key, value );
            Debug.Assert( c != null );
            var result = new Dictionary<TKey, TValue>( c.Length, comparer );
            r.ImplementationServices.TrackPreTrackedObject( result, num );
            if( c.Length > 0 )
            {
                r.ImplementationServices.OnPostDeserialization( () =>
                {
                    foreach( var kv in c ) result.Add( kv.Key, kv.Value );
                } );
            }
            return result;
        }


        /// <summary>
        /// Reads the content of a dictionary of <see cref="KeyValuePair{TKey, TValue}"/> that have been
        /// previously written by <see cref="DictionarySerializer{TKey, TValue}.WriteDictionaryContent(BinarySerializer, int, IEnumerable{KeyValuePair{TKey, TValue}}, ITypeSerializationDriver{TKey}, ITypeSerializationDriver{TValue})"/>.
        /// </summary>
        /// <param name="r">The deserializer.</param>
        /// <param name="keyDeserialization">Already available key deserializer if known.</param>
        /// <param name="valueDeserialization">Already available value deserializer if known.</param>
        /// <returns>The dictionary content.</returns>
        public static KeyValuePair<TKey, TValue>[]? ReadDictionaryContent( IBinaryDeserializer r, IDeserializationDriver<TKey>? keyDeserialization = null, IDeserializationDriver<TValue>? valueDeserialization = null )
        {
            if( r == null ) throw new ArgumentNullException( nameof( r ) );
            int len = r.ReadSmallInt32();
            if( len == -1 ) return null;
            if( len == 0 ) return Array.Empty<KeyValuePair<TKey, TValue>>();
            byte b = r.ReadByte();
            bool monoTypeKey = (b & 1) != 0;
            if( monoTypeKey && keyDeserialization == null ) keyDeserialization = r.ImplementationServices.Drivers.FindDriver<TKey>();
            bool monoTypeVal = (b & 2) != 0;
            if( monoTypeVal && valueDeserialization == null ) valueDeserialization = r.ImplementationServices.Drivers.FindDriver<TValue>();

            var result = new KeyValuePair<TKey, TValue>[len];
            for( int i = 0; i < len; ++i )
            {
                TKey k = monoTypeKey ? keyDeserialization.ReadInstance( r, null ) : (TKey)r.ReadObject()!;
                TValue v = monoTypeVal ? valueDeserialization.ReadInstance( r, null ) : (TValue?)r.ReadObject();
                result[i] = new KeyValuePair<TKey, TValue>( k, v );
            }
            return result;
        }

        object? IDeserializationDriver.ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  ) => DoRead( r, _key, _value );
    }
}
