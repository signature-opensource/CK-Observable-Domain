using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    public class DictionaryTypeSerializer<TKey,TValue> : ITypeSerializationDriver<Dictionary<TKey, TValue>>
    {
        readonly ITypeSerializationDriver<TKey> _key;
        readonly ITypeSerializationDriver<TValue> _value;

        public DictionaryTypeSerializer( ITypeSerializationDriver<TKey> key, ITypeSerializationDriver<TValue> value )
        {
            if( key == null ) throw new ArgumentNullException( nameof( key ) );
            if( value == null ) throw new ArgumentNullException( nameof( value ) );
            _key = key;
            _value = value;
        }

        public Type Type => typeof( Dictionary<TKey, TValue> );

        public void WriteData( BinarySerializer w, Dictionary<TKey, TValue> o )
        {
            DoWrite( w, o, _key, _value );
        }

        public static void Write( BinarySerializer w, Dictionary<TKey, TValue> o, ITypeSerializationDriver<TKey> key, ITypeSerializationDriver<TValue> value )
        {
            if( key == null ) throw new ArgumentNullException( nameof( key ) );
            if( value == null ) throw new ArgumentNullException( nameof( value ) );
            DoWrite( w, o, key, value );
        }


        /// <summary>
        /// Writes a dictionary content.
        /// </summary>
        /// <param name="w">The binary serializer to use. Must not be null.</param>
        /// <param name="count">Number of items. Must be zero or positive.</param>
        /// <param name="items">The items. Can be null (in such case, <paramref name="count"/> must be 0).</param>
        /// <param name="keySerialization">Key serialization driver. Must not be null.</param>
        /// <param name="valueSerialization">Value serialization driver. Must not be null.</param>
        public static void WriteDictionaryContent(
            BinarySerializer w,
            int count,
            IEnumerable<KeyValuePair<TKey, TValue>> items,
            ITypeSerializationDriver<TKey> keySerialization,
            ITypeSerializationDriver<TValue> valueSerialization )
        {
            if( w == null ) throw new ArgumentNullException( nameof( w ) );
            if( count < 0 ) throw new ArgumentException( "Must be greater or equal to 0.", nameof( count ) );
            if( keySerialization == null ) throw new ArgumentNullException( nameof( keySerialization ) );
            if( valueSerialization == null ) throw new ArgumentNullException( nameof( valueSerialization ) );
            if( items == null )
            {
                if( count != 0 ) throw new ArgumentNullException( nameof( items ) );
                w.WriteSmallInt32( -1 );
                return;
            }
            w.WriteSmallInt32( count );
            if( count > 0 )
            {
                var tKey = keySerialization.Type;
                var tVal = valueSerialization.Type;
                bool monoTypeKey = tKey.IsSealed || tKey.IsValueType;
                bool monoTypeVal = tVal.IsSealed || tVal.IsValueType;

                int dicType = monoTypeKey ? 1 : 0;
                dicType |= monoTypeVal ? 2 : 0;
                w.Write( (byte)dicType );

                foreach( var kv in items )
                {
                    if( monoTypeKey ) keySerialization.WriteData( w, kv.Key );
                    else w.WriteObject( kv.Key );
                    if( monoTypeVal ) valueSerialization.WriteData( w, kv.Value );
                    else w.WriteObject( kv.Value );
                    if( --count == 0 ) break;
                }
                if( count > 0 ) throw new ArgumentException( $"Not enough items: missing {count} items.", nameof( count ) );
            }
        }

        static void DoWrite( BinarySerializer w, Dictionary<TKey, TValue> o, ITypeSerializationDriver<TKey> key, ITypeSerializationDriver<TValue> value )
        {
            if( o == null ) w.WriteSmallInt32( -1 );
            else
            {
                // Version
                w.WriteSmallInt32( 0 );
                w.WriteObject( o.Comparer );
                WriteDictionaryContent( w, o.Count, o, key, value );
            }
        }

        public void WriteData( BinarySerializer w, object o ) => DoWrite( w, (Dictionary<TKey,TValue>)o, _key, _value );

        public void WriteTypeInformation( BinarySerializer s ) => s.WriteSimpleType( Type, null );

    }
}
