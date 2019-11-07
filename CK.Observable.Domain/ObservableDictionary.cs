using System;
using System.Collections;
using System.Collections.Generic;

namespace CK.Observable
{
    [SerializationVersionAttribute( 0 )]
    public class ObservableDictionary<TKey, TValue> : ObservableObject, IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>
    {
        readonly Dictionary<TKey, TValue> _map;
        ObservableEventHandler<CollectionMapSetEvent> _itemSet;
        ObservableEventHandler<CollectionMapSetEvent> _itemAdded;
        ObservableEventHandler<CollectionClearEvent> _collectionCleared;
        ObservableEventHandler<CollectionRemoveKeyEvent> _itemRemoved;

        /// <summary>
        /// Raised when an existing item has been updated by <see cref="this[TKey]"/> to a different value.
        /// </summary>
        public event SafeEventHandler<CollectionMapSetEvent> ItemSet
        {
            add => _itemSet.Add( value, nameof( ItemSet ) );
            remove => _itemSet.Remove( value );
        }

        /// <summary>
        /// Raised when a new item has been added by <see cref="this[TKey]"/> or <see cref="Add(TKey, TValue)"/>.
        /// </summary>
        public event SafeEventHandler<CollectionMapSetEvent> ItemAdded
        {
            add => _itemAdded.Add( value, nameof( ItemAdded ) );
            remove => _itemAdded.Remove( value );
        }


        /// <summary>
        /// Raised by <see cref="Clear"/>.
        /// </summary>
        public event SafeEventHandler<CollectionClearEvent> CollectionCleared
        {
            add => _collectionCleared.Add( value, nameof( CollectionCleared ) );
            remove => _collectionCleared.Remove( value );
        }

        /// <summary>
        /// Raised by <see cref="Remove(TKey)"/>.
        /// </summary>
        public event SafeEventHandler<CollectionRemoveKeyEvent> ItemRemoved
        {
            add => _itemRemoved.Add( value, nameof( CollectionCleared ) );
            remove => _itemRemoved.Remove( value );
        }


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
            _itemSet = new ObservableEventHandler<CollectionMapSetEvent>( r );
            _itemAdded = new ObservableEventHandler<CollectionMapSetEvent>( r );
            _collectionCleared = new ObservableEventHandler<CollectionClearEvent>( r );
            _itemRemoved = new ObservableEventHandler<CollectionRemoveKeyEvent>( r );
        }

        void Write( BinarySerializer s )
        {
            s.WriteObject( _map );
            _itemSet.Write( s );
            _itemAdded.Write( s );
            _collectionCleared.Write( s );
            _itemRemoved.Write( s );
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
                        if( e != null && _itemSet.HasHandlers ) _itemSet.Raise( Monitor, this, e, nameof(ItemSet) );
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
            if( e != null && _itemAdded.HasHandlers ) _itemAdded.Raise( Monitor, this, e, nameof( ItemAdded ) );
        }

        void ICollection<KeyValuePair<TKey, TValue>>.Add( KeyValuePair<TKey, TValue> item ) => Add( item.Key, item.Value );

        public void Clear()
        {
            if( _map.Count > 0 )
            {
                _map.Clear();
                CollectionClearEvent e = Domain.OnCollectionClear( this );
                if( e != null && _collectionCleared.HasHandlers ) _collectionCleared.Raise( Monitor, this, e, nameof( CollectionCleared ) );
            }
        }

        public bool Remove( TKey key )
        {
            if( _map.Remove( key ) )
            {
                CollectionRemoveKeyEvent e = Domain.OnCollectionRemoveKey( this, key );
                if( e != null && _itemRemoved.HasHandlers ) _itemRemoved.Raise( Monitor, this, e, nameof( ItemRemoved ) );
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
                if( e != null && _itemRemoved.HasHandlers ) _itemRemoved.Raise( Monitor, this, e, nameof( ItemRemoved ) );
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
