using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    [SerializationVersionAttribute(0)]
    public class ObservableDictionary<TKey, TValue> : ObservableObject, IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>
    {
        readonly Dictionary<TKey, TValue> _map;

        public ObservableDictionary()
        {
            _map = new Dictionary<TKey, TValue>();
        }

        protected ObservableDictionary( IBinaryDeserializerContext d ) : base( d )
        {
            var r = d.StartReading();
            int count = r.ReadNonNegativeSmallInt32();
            _map = new Dictionary<TKey, TValue>( count );
            while( --count >= 0 )
            {
                _map.Add( (TKey)r.ReadObject(), (TValue)r.ReadObject() );
            }
        }

        void Write( BinarySerializer s )
        {
            s.WriteNonNegativeSmallInt32( _map.Count );
            foreach( var kv in _map )
            {
                s.WriteObject( kv.Key );
                s.WriteObject( kv.Value );
            }
        }

        void Export( int num, ObjectExporter e )
        {
            //e.Target.EmitStartObject( -1, ObjectExportedKind.Object );
            //e.ExportNamedProperty( ExportContentOIdName, OId );
            //e.Target.EmitPropertyName( ExportContentPropName );
            e.ExportMap( num, _map );
            //e.Target.EmitEndObject( -1, ObjectExportedKind.Object );
        }

        internal override ObjectExportedKind ExportedKind => ObjectExportedKind.Map;

        public TValue this[TKey key]
        {
            get => _map[key];
            set
            {
                _map[key] = value;
                Domain.OnCollectionMapSet( this, key, value );
            }
        }

        public Dictionary<TKey, TValue>.KeyCollection Keys => _map.Keys;

        public Dictionary<TKey, TValue>.ValueCollection Values => _map.Values;

        public int Count => _map.Count;

        bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => false;

        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => _map.Keys;

        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => _map.Values;

        ICollection<TKey> IDictionary<TKey, TValue>.Keys => _map.Keys;

        ICollection<TValue> IDictionary<TKey, TValue>.Values => _map.Values;

        public void Add( TKey key, TValue value )
        {
            _map.Add( key, value );
            Domain.OnCollectionMapSet( this, key, value );
        }

        void ICollection<KeyValuePair<TKey, TValue>>.Add( KeyValuePair<TKey, TValue> item ) => Add( item.Key, item.Value );

        public void Clear()
        {
            _map.Clear();
            Domain.OnCollectionClear( this );
        }

        public bool Remove( TKey key )
        {
            if( _map.Remove( key ) )
            {
                Domain.OnCollectionRemoveKey( this, key );
                return true;
            }
            return false;
        }

        bool ICollection<KeyValuePair<TKey, TValue>>.Remove( KeyValuePair<TKey, TValue> item )
        {
            // Removing a pair from a dictionary also checks the value equality.
            if( ((IDictionary<TKey, TValue>)_map).Remove( item ) )
            {
                Domain.OnCollectionRemoveKey( this, item.Key );
                return true;
            }
            return false;
        }

        bool ICollection<KeyValuePair<TKey, TValue>>.Contains( KeyValuePair<TKey, TValue> item ) => ((IDictionary<TKey, TValue>)_map).Contains( item );

        public bool ContainsKey( TKey key ) => _map.ContainsKey( key );

        public void CopyTo( KeyValuePair<TKey, TValue>[] array, int arrayIndex ) => ((IDictionary<TKey, TValue>)_map).CopyTo( array, arrayIndex );

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _map.GetEnumerator();


        public bool TryGetValue( TKey key, out TValue value ) => _map.TryGetValue( key, out value );


        IEnumerator IEnumerable.GetEnumerator() => _map.GetEnumerator();
        
    }
}
