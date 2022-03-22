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

        public Dictionary<TKey, TValue> ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo, bool mustRead ) => DoRead( r, _key, _value, mustRead );


        static Dictionary<TKey, TValue> DoRead( IBinaryDeserializer r, IDeserializationDriver<TKey> keyDeserialization, IDeserializationDriver<TValue> valueDeserialization, bool mustRead )
        {
            if( r.ImplementationServices.SerializationVersion >= 8 )
            {
                if( !mustRead && !r.ImplementationServices.ReadNewObject<Dictionary<TKey, TValue>>( out var already ) ) return already;
                int version = r.ReadSmallInt32();
                if( version != 0 ) throw new InvalidDataException();

                int num = r.ImplementationServices.PreTrackObject();
                // This uses the legacy .net serialization.
                var comparer = (IEqualityComparer<TKey>?)r.ReadObject();
                if( comparer != null )
                {
                    // The comparer that has been deserialized is a new instance, not one of the singletons :(.
                    // To be able to serialize it back we need to "normalize" it... and we have almost no other way to do this 
                    // than to challenge it!
                    // We do this for strings basically and uses Ordinal or OrdinalIgnoreCase, for other type of keys, we remap it to the default one :(
                    //
                    // Note that there are more stuff in Net6 to handle this (like https://source.dot.net/#System.Private.CoreLib/StringComparer.cs,75).
                    //
                    if( typeof( TKey ) == typeof( string ) )
                    {
                        comparer = FindBestStringComparer( (IEqualityComparer<string>)comparer );
                    }
                    else
                    {
                        comparer = EqualityComparer<TKey>.Default;
                    }
                }

                int len = r.ReadSmallInt32();
                KeyValuePair<TKey, TValue>[] items;
                if( len == 0 )
                {
                    items = Array.Empty<KeyValuePair<TKey, TValue>>();
                }
                else
                {
                    items = new KeyValuePair<TKey, TValue>[len];
                    byte b = r.ReadByte();
                    bool monoTypeKey = (b & 1) != 0;
                    if( monoTypeKey && keyDeserialization == null ) keyDeserialization = r.ImplementationServices.Drivers.FindDriver<TKey>();
                    bool monoTypeVal = (b & 2) != 0;
                    if( monoTypeVal && valueDeserialization == null ) valueDeserialization = r.ImplementationServices.Drivers.FindDriver<TValue>();
                    for( int i = 0; i < len; ++i )
                    {
                        TKey k = monoTypeKey ? keyDeserialization.ReadInstance( r, null, false ) : (TKey)r.ReadObject()!;
                        TValue v = monoTypeVal ? valueDeserialization.ReadInstance( r, null, false ) : (TValue?)r.ReadObject();
                        items[i] = new KeyValuePair<TKey, TValue>( k, v );
                    }
                }
                var result = new Dictionary<TKey, TValue>( items.Length, comparer );
                r.ImplementationServices.TrackPreTrackedObject( result, num );
                if( items.Length > 0 )
                {
                    r.ImplementationServices.OnPostDeserialization( () =>
                    {
                        foreach( var kv in items ) result.Add( kv.Key, kv.Value );
                    } );
                }
                return result;
            }
            else
            {
                int version = r.ReadSmallInt32();
                if( version == -1 ) return null!;
                if( version != 0 ) throw new InvalidDataException();
                int num = r.ImplementationServices.PreTrackObject();
                var comparer = (IEqualityComparer<TKey>?)r.ReadObject();
                if( comparer != null )
                {
                    // The comparer that has been deserialized is a new instance, not one of the singletons :(.
                    // To be able to serialize it back we need to "normalize" it... and we have almost no other way to do this 
                    // than to challenge it!
                    // We do this for strings basically and uses Ordinal or OrdinalIgnoreCase, for other type of keys, we remap it to the default one :(
                    //
                    // Note that there are more stuff in Net6 to handle this (like https://source.dot.net/#System.Private.CoreLib/StringComparer.cs,75).
                    //
                    if( typeof( TKey ) == typeof( string ) )
                    {
                        comparer = FindBestStringComparer( (IEqualityComparer<string>)comparer );
                    }
                    else
                    {
                        comparer = EqualityComparer<TKey>.Default;
                    }
                }
                var c = ReadDictionaryContent( r, keyDeserialization, valueDeserialization );
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
        }

        static IEqualityComparer<TKey> FindBestStringComparer( IEqualityComparer<string> comparer )
        {
            if( comparer.Equals("A","a") )
            {
                // Ignore case: use OrdinalIgnoreCase
                return (IEqualityComparer<TKey>)StringComparer.OrdinalIgnoreCase;
            }
            // Case sensitive: use Ordinal
            return (IEqualityComparer<TKey>)StringComparer.Ordinal;
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
                TKey k = monoTypeKey ? keyDeserialization.ReadInstance( r, null, false ) : (TKey)r.ReadObject()!;
                TValue v = monoTypeVal ? valueDeserialization.ReadInstance( r, null, false ) : (TValue?)r.ReadObject();
                result[i] = new KeyValuePair<TKey, TValue>( k, v );
            }
            return result;
        }

        object? IDeserializationDriver.ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo, bool mustRead ) => DoRead( r, _key, _value, mustRead );
    }
}
