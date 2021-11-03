using System;
using System.Collections.Generic;
using System.Diagnostics;
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace CK.Observable
{
    public class DictionarySerializer<TKey,TValue> : ITypeSerializationDriver<Dictionary<TKey, TValue>>
    {
        readonly ITypeSerializationDriver<TKey> _key;
        readonly ITypeSerializationDriver<TValue> _value;

        public DictionarySerializer( ITypeSerializationDriver<TKey> key, ITypeSerializationDriver<TValue> value )
        {
            _key = key;
            _value = value;
        }

        bool ITypeSerializationDriver.IsFinalType => false;

        public Type Type => typeof( Dictionary<TKey, TValue> );

        void ITypeSerializationDriver<Dictionary<TKey, TValue>>.WriteData( BinarySerializer w, Dictionary<TKey, TValue> o )
        {
            DoWrite( w, o, _key, _value );
        }

        /// <summary>
        /// Writes a dictionary content.
        /// </summary>
        /// <param name="w">The binary serializer to use. Must not be null.</param>
        /// <param name="count">Number of items. Must be zero or positive.</param>
        /// <param name="items">The items. Cannot be null.</param>
        /// <param name="keySerialization">Available key serialization driver if available.</param>
        /// <param name="valueSerialization">Available value serialization driver if available.</param>
        public static void WriteDictionaryContent( BinarySerializer w,
                                                   int count,
                                                   IEnumerable<KeyValuePair<TKey, TValue>> items,
                                                   ITypeSerializationDriver<TKey>? keySerialization = null,
                                                   ITypeSerializationDriver<TValue>? valueSerialization = null )
        {
            if( w == null ) throw new ArgumentNullException( nameof( w ) );
            if( count < 0 ) throw new ArgumentOutOfRangeException( nameof( count ) );
            if( items == null ) throw new ArgumentNullException( nameof( items ) );

            w.WriteSmallInt32( count );
            if( count > 0 )
            {
                var tKey = typeof(TKey);
                var tVal = typeof(TValue);
                bool monoTypeKey = tKey.IsValueType || (keySerialization != null && keySerialization.IsFinalType);
                if( monoTypeKey && keySerialization == null )
                {
                    keySerialization = w.ImplementationServices.Drivers.FindDriver<TKey>();
                    if( keySerialization == null ) throw new InvalidOperationException( $"Cannot find an {nameof( ITypeSerializationDriver )} for Dictionary Key type: {tKey.FullName}." );
                }
                bool monoTypeVal = tVal.IsValueType || (valueSerialization != null && valueSerialization.IsFinalType);
                if( monoTypeVal && valueSerialization == null )
                {
                    valueSerialization = w.ImplementationServices.Drivers.FindDriver<TValue>();
                    if( valueSerialization == null ) throw new InvalidOperationException( $"Cannot find an {nameof( ITypeSerializationDriver )} for Dictionary Value type: {tVal.FullName}." );
                }
                int dicType = monoTypeKey ? 1 : 0;
                dicType |= monoTypeVal ? 2 : 0;
                w.Write( (byte)dicType );

                foreach( var kv in items )
                {
                    if( monoTypeKey )
                    {
                        Debug.Assert( keySerialization != null );
                        keySerialization.WriteData( w, kv.Key );
                    }
                    else w.WriteObject( kv.Key );

                    if( monoTypeVal )
                    {
                        Debug.Assert( valueSerialization != null );
                        valueSerialization.WriteData( w, kv.Value );
                    }
                    else w.WriteObject( kv.Value );
                    if( --count == 0 ) break;
                }
                if( count > 0 ) throw new ArgumentException( $"Not enough items: missing {count} items.", nameof( count ) );
            }
        }

        static void DoWrite( BinarySerializer w, Dictionary<TKey, TValue> o, ITypeSerializationDriver<TKey> key, ITypeSerializationDriver<TValue> value )
        {
            if( !w.ImplementationServices.WriteNewObject( o ) ) return;
            // Version
            w.WriteSmallInt32( 0 );
            w.WriteObject( o.Comparer );
            WriteDictionaryContent( w, o.Count, o, key, value );
        }

        public void WriteData( BinarySerializer w, object o ) => DoWrite( w, (Dictionary<TKey,TValue>)o, _key, _value );

        public void WriteTypeInformation( BinarySerializer s ) => s.WriteSimpleType( Type );

    }
}
