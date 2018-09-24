using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    public class DictionarySerializer<TKey,TValue> : ITypeSerializationDriver<Dictionary<TKey, TValue>>
    {
        readonly ITypeSerializationDriver<TKey> _key;
        readonly ITypeSerializationDriver<TValue> _value;

        public DictionarySerializer( ITypeSerializationDriver<TKey> key, ITypeSerializationDriver<TValue> value )
        {
            if( key == null ) throw new ArgumentNullException( nameof( key ) );
            if( value == null ) throw new ArgumentNullException( nameof( value ) );
            _key = key;
            _value = value;
        }

        public Type Type => typeof( Dictionary<TKey, TValue> );

        void ITypeSerializationDriver<Dictionary<TKey, TValue>>.WriteData( BinarySerializer w, Dictionary<TKey, TValue> o )
        {
            if( w == null ) throw new ArgumentNullException( nameof( w ) );
            DoWrite( w, o, _key, _value );
        }

        /// <summary>
        /// Writes a dictionary content.
        /// </summary>
        /// <param name="w">The binary serializer to use. Must not be null.</param>
        /// <param name="count">Number of items. Must be zero or positive.</param>
        /// <param name="items">The items. Can be null (in such case, <paramref name="count"/> must be 0).</param>
        /// <param name="keySerialization">Available key serialization driver if available.</param>
        /// <param name="valueSerialization">Available value serialization driver if available.</param>
        public static void WriteDictionaryContent(
            BinarySerializer w,
            int count,
            IEnumerable<KeyValuePair<TKey, TValue>> items,
            ITypeSerializationDriver<TKey> keySerialization = null,
            ITypeSerializationDriver<TValue> valueSerialization = null )
        {
            if( w == null ) throw new ArgumentNullException( nameof( w ) );
            if( count < 0 ) throw new ArgumentException( "Must be greater or equal to 0.", nameof( count ) );
            if( items == null )
            {
                if( count != 0 ) throw new ArgumentNullException( nameof( items ) );
                w.WriteSmallInt32( -1 );
                return;
            }
            w.WriteSmallInt32( count );
            if( count > 0 )
            {
                var tKey = typeof(TKey);
                var tVal = typeof(TValue);
                bool monoTypeKey = tKey.IsSealed || tKey.IsValueType;
                if( monoTypeKey && keySerialization == null ) keySerialization = w.Drivers.FindDriver<TKey>();
                bool monoTypeVal = tVal.IsSealed || tVal.IsValueType;
                if( monoTypeVal && valueSerialization == null ) valueSerialization = w.Drivers.FindDriver<TValue>();

                int dicType = monoTypeKey ? 1 : 0;
                dicType |= monoTypeVal ? 2 : 0;
                w.Write( (byte)dicType );

                foreach( var kv in items )
                {
                    if( monoTypeKey ) w.Write( kv.Key, keySerialization );
                    else w.WriteObject( kv.Key );
                    if( monoTypeVal ) w.Write( kv.Value, valueSerialization );
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
