using CK.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace CK.Observable
{
    /// <summary>
    /// Implements a simple observable <see cref="Dictionary{TKey, TValue}"/>.
    /// </summary>
    /// <typeparam name="TKey">The type of the keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of the values in the dictionary.</typeparam>
    [SerializationVersion( 0 )]
    public class ObservableDictionary<TKey, TValue> : ObservableObject, IDictionary<TKey, TValue>, IObservableReadOnlyDictionary<TKey, TValue> where TKey : notnull
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
        /// Initializes a new empty <see cref="ObservableDictionary{TKey, TValue}"/>.
        /// </summary>
        public ObservableDictionary()
        {
            _map = new Dictionary<TKey, TValue>();
        }

        #region Old Serialization
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        ObservableDictionary( IBinaryDeserializer r, TypeReadInfo? info )
                : base( BinarySerialization.Sliced.Instance )
        {
            _map = (Dictionary<TKey, TValue>)r.ReadObject()!;
            _itemSet = new ObservableEventHandler<CollectionMapSetEvent>( r );
            _itemAdded = new ObservableEventHandler<CollectionMapSetEvent>( r );
            _collectionCleared = new ObservableEventHandler<CollectionClearEvent>( r );
            _itemRemoved = new ObservableEventHandler<CollectionRemoveKeyEvent>( r );
        }
        #endregion

        #region New Serialization
        /// <summary>
        /// Specialized deserialization constructor for specialized classes.
        /// </summary>
        /// <param name="_">Unused parameter.</param>
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        protected ObservableDictionary( BinarySerialization.Sliced _ ) : base( _ ) { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        ObservableDictionary( BinarySerialization.IBinaryDeserializer d, BinarySerialization.ITypeReadInfo info )
            : base( BinarySerialization.Sliced.Instance )
        {
            _map = d.ReadObject<Dictionary<TKey, TValue>>()!;
            _itemSet = new ObservableEventHandler<CollectionMapSetEvent>( d );
            _itemAdded = new ObservableEventHandler<CollectionMapSetEvent>( d );
            _collectionCleared = new ObservableEventHandler<CollectionClearEvent>( d );
            _itemRemoved = new ObservableEventHandler<CollectionRemoveKeyEvent>( d );
        }

        public static void Write( BinarySerialization.IBinarySerializer s, in ObservableDictionary<TKey, TValue> o )
        {
            s.WriteObject( o._map );
            o._itemSet.Write( s );
            o._itemAdded.Write( s );
            o._collectionCleared.Write( s );
            o._itemRemoved.Write( s );
        }
        #endregion

        internal override ObjectExportedKind ExportedKind => ObjectExportedKind.Map;

        /// <summary>
        /// Gets or sets the value associated with the specified key.
        /// </summary>
        /// <param name="key">The key of the value to get or set.</param>
        /// <returns>
        /// The value associated with the specified key. If the specified key is not found,
        /// a get operation throws a <see cref="KeyNotFoundException"/>, and
        /// a set operation creates a new element with the specified key.
        /// </returns>
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
                        CollectionMapSetEvent? e = ActualDomain.OnCollectionMapSet( this, key, value );
                        if( e != null && _itemSet.HasHandlers ) _itemSet.Raise( this, e );
                    }
                }
                else Add( key, value );
           }
        }

        /// <summary>
        /// Gets a collection containing the keys in dictionary.
        /// </summary>
        public Dictionary<TKey, TValue>.KeyCollection Keys => _map.Keys;

        /// <summary>
        /// Gets a collection containing the values in the dictionary.
        /// </summary>
        public Dictionary<TKey, TValue>.ValueCollection Values => _map.Values;

        /// <summary>
        /// Gets the number of key/value pairs contained in the dictionary.
        /// </summary>
        public int Count => _map.Count;

        bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => false;

        IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => _map.Keys;

        IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => _map.Values;

        ICollection<TKey> IDictionary<TKey, TValue>.Keys => _map.Keys;

        ICollection<TValue> IDictionary<TKey, TValue>.Values => _map.Values;

        /// <summary>
        /// Adds the specified key and value to the dictionary.
        /// </summary>
        /// <param name="key">The key of the element to add.</param>
        /// <param name="value">The value of the element to add. The value can be null for reference types.</param>
        public void Add( TKey key, TValue value )
        {
            _map.Add( key, value );
            CollectionMapSetEvent? e = ActualDomain.OnCollectionMapSet( this, key, value );
            if( e != null && _itemAdded.HasHandlers ) _itemAdded.Raise( this, e );
        }

        void ICollection<KeyValuePair<TKey, TValue>>.Add( KeyValuePair<TKey, TValue> item ) => Add( item.Key, item.Value );

        /// <summary>
        /// Removes all keys and values from this dictionary.
        /// </summary>
        public void Clear() => Clear( false );

        /// <summary>
        /// Clears this dictionary of all its entries, optionally destroying all values that are <see cref="IDestroyableObject"/>.
        /// <para>
        /// Note that keys, if they are <see cref="IDestroyableObject"/> are not destroyed, only <see cref="Values"/> can automatically
        /// be destroyed.
        /// </para>
        /// </summary>
        /// <param name="destroyValues">True to call <see cref="IDestroyableObject.Destroy()"/> on destroyable values.</param>
        public void Clear( bool destroyValues )
        {
            if( _map.Count > 0 )
            {
                if( destroyValues )
                {
                    DestroyValues();
                }
                _map.Clear();
                CollectionClearEvent? e = ActualDomain.OnCollectionClear( this );
                if( e != null && _collectionCleared.HasHandlers ) _collectionCleared.Raise( this, e );
            }
        }

        protected void DestroyValues()
        {
            // Take a snapshot so that OnDestroyed reflexes can safely alter the _map.
            foreach( var d in _map.Values.OfType<IDestroyableObject>().ToArray() )
            {
                d.Destroy();
            }
        }

        /// <summary>
        /// Removes the value with the specified key from the dictionary.
        /// </summary>
        /// <param name="key">The key of the element to remove.</param>
        /// <returns>true if the element is successfully found and removed; otherwise, false. This method returns false if key is not found in the dictionary.</returns>
        public bool Remove( TKey key )
        {
            if( _map.Remove( key ) )
            {
                CollectionRemoveKeyEvent? e = ActualDomain.OnCollectionRemoveKey( this, key );
                if( e != null && _itemRemoved.HasHandlers ) _itemRemoved.Raise( this, e );
                return true;
            }
            return false;
        }

        bool ICollection<KeyValuePair<TKey, TValue>>.Remove( KeyValuePair<TKey, TValue> item )
        {
            // Removing a pair from a dictionary also checks the value equality.
            if( ((IDictionary<TKey, TValue>)_map).Remove( item ) )
            {
                CollectionRemoveKeyEvent? e = ActualDomain.OnCollectionRemoveKey( this, item.Key );
                if( e != null && _itemRemoved.HasHandlers ) _itemRemoved.Raise( this, e );
                return true;
            }
            return false;
        }

        bool ICollection<KeyValuePair<TKey, TValue>>.Contains( KeyValuePair<TKey, TValue> item ) => ((IDictionary<TKey, TValue>)_map).Contains( item );

        /// <summary>
        /// Determines whether the dictionary contains the specified key.
        /// </summary>
        /// <param name="key">The key to locate in the dictionary.</param>
        /// <returns>true if the dictionary contains an element with the specified key; otherwise, false.</returns>
        public bool ContainsKey( TKey key ) => _map.ContainsKey( key );

        /// <summary>
        /// Copies the key/value pairs to an array.
        /// </summary>
        /// <param name="array">The target array.</param>
        /// <param name="arrayIndex">The index in the target array.</param>
        public void CopyTo( KeyValuePair<TKey, TValue>[] array, int arrayIndex ) => ((IDictionary<TKey, TValue>)_map).CopyTo( array, arrayIndex );

        /// <summary>
        /// Gets the enumerator.
        /// </summary>
        /// <returns>The enumerator.</returns>
        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _map.GetEnumerator();

        /// <summary>
        /// Gets the value associated with the specified key.
        /// </summary>
        /// <param name="key">The key of the value to get.</param>
        /// <param name="value">
        /// When this method returns, contains the value associated with the specified key,
        /// if the key is found; otherwise, the default value for the type of the value parameter.
        /// This parameter is passed uninitialized.
        /// </param>
        /// <returns>True if the dictionary contains an element with the specified key; otherwise, false.</returns>
        public bool TryGetValue( TKey key, out TValue value ) => _map.TryGetValue( key, out value );

        IEnumerator IEnumerable.GetEnumerator() => _map.GetEnumerator();

    }
}
