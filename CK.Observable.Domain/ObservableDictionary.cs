using System;
using System.Collections;
using System.Collections.Generic;

namespace CK.Observable
{
    [SerializationVersionAttribute( 0 )]
    public class ObservableDictionary<TKey, TValue> : ObservableObject, IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>
    {
        readonly Dictionary<TKey, TValue> _map;

        /// <summary>
        /// Raised when an item has been set by <see cref="this[TKey]"/> or <see cref="Add(TKey, TValue)"/>.
        /// </summary>
        public event EventHandler<CollectionMapSetEvent> ItemSet;

        /// <summary>
        /// Raised by <see cref="Clear"/>.
        /// </summary>
        public event EventHandler<CollectionClearEvent> CollectionCleared;

        /// <summary>
        /// Raised by <see cref="Remove(TKey)"/>.
        /// </summary>
        public event EventHandler<CollectionRemoveKeyEvent> ItemRemoved;

        /// <summary>
        /// Initializes a new <see cref="ObservableDictionary{TKey, TValue}"/>.
        /// </summary>
        public ObservableDictionary()
        {
            _map = new Dictionary<TKey, TValue>();
        }

        protected ObservableDictionary( IBinaryDeserializerContext d ) : base( d )
        {
            var r = d.StartReading();
            _map = (Dictionary<TKey, TValue>)r.ReadObject();
        }

        void Write( BinarySerializer s )
        {
            s.WriteObject( _map );
        }

        internal override ObjectExportedKind ExportedKind => ObjectExportedKind.Map;

        public TValue this[TKey key]
        {
            get => _map[key];
            set
            {
                if( _map.TryGetValue( key, out var exists ) )
                {
                    if( !EqualityComparer<TValue>.Default.Equals( value, exists ) )
                    {
                        _map[key] = value;
                        CollectionMapSetEvent e = Domain.OnCollectionMapSet( this, key, value );
                        if( e != null && ItemSet != null ) ItemSet( this, e );
                    }
                }
                else Add( key, value );
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
            CollectionMapSetEvent e = Domain.OnCollectionMapSet( this, key, value );
            if( e != null && ItemSet != null ) ItemSet( this, e );
        }

        void ICollection<KeyValuePair<TKey, TValue>>.Add( KeyValuePair<TKey, TValue> item ) => Add( item.Key, item.Value );

        public void Clear()
        {
            if( _map.Count > 0 )
            {
                _map.Clear();
                CollectionClearEvent e = Domain.OnCollectionClear( this );
                if( e != null && CollectionCleared != null ) CollectionCleared( this, e );
            }
        }

        public bool Remove( TKey key )
        {
            if( _map.Remove( key ) )
            {
                CollectionRemoveKeyEvent e = Domain.OnCollectionRemoveKey( this, key );
                if( e != null && ItemRemoved != null ) ItemRemoved( this, e );
                return true;
            }
            return false;
        }

        bool ICollection<KeyValuePair<TKey, TValue>>.Remove( KeyValuePair<TKey, TValue> item )
        {
            // Removing a pair from a dictionary also checks the value equality.
            if( ((IDictionary<TKey, TValue>)_map).Remove( item ) )
            {
                CollectionRemoveKeyEvent e = Domain.OnCollectionRemoveKey( this, item.Key );
                if( e != null && ItemRemoved != null ) ItemRemoved( this, e );
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
