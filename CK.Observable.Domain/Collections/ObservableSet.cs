using System;
using System.Collections;
using System.Collections.Generic;

namespace CK.Observable
{
    /// <summary>
    /// Implements a simple observable set of items.
    /// </summary>
    /// <remarks>
    /// This doesn't support <see cref="ISet{T}"/> because <see cref="ISet{T}.ExceptWith(IEnumerable{T})"/>, <see cref="ISet{T}.IntersectWith(IEnumerable{T})"/>,
    /// <see cref="ISet{T}.SymmetricExceptWith(IEnumerable{T})"/> and <see cref="ISet{T}.UnionWith(IEnumerable{T})"/> should be re-implemented by this
    /// class in order to be efficient (the added/removed items need to be tracked).
    /// </remarks>
    /// <typeparam name="T">Item type.</typeparam>
    [SerializationVersion( 0 )]
    public class ObservableSet<T> : ObservableObject, IObservableReadOnlySet<T>
    {
        readonly HashSet<T> _set;
        ObservableEventHandler<CollectionAddKeyEvent> _itemAdded;
        ObservableEventHandler<CollectionRemoveKeyEvent> _itemRemoved;
        ObservableEventHandler<CollectionClearEvent> _collectionCleared;

        /// <summary>
        /// Raised when a new item has been added by <see cref="ObservableSet{T}.Add(T)"/>.
        /// </summary>
        public event SafeEventHandler<CollectionAddKeyEvent> ItemAdded
        {
            add => _itemAdded.Add( value, nameof( ItemAdded ) );
            remove => _itemAdded.Remove( value );
        }

        /// <summary>
        /// Raised by <see cref="ObservableSet{T}.Remove(T)"/>.
        /// </summary>
        public event SafeEventHandler<CollectionRemoveKeyEvent> ItemRemoved
        {
            add => _itemRemoved.Add( value, nameof( ItemRemoved ) );
            remove => _itemRemoved.Remove( value );
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
        /// Initializes a new empty observable set.
        /// </summary>
        public ObservableSet()
        {
            _set = new HashSet<T>();
        }

        /// <summary>
        /// Deserialization constructor.
        /// </summary>
        /// <param name="d">The deserialization context.</param>
        protected ObservableSet( IBinaryDeserializerContext d )
            : base( d )
        {
            var r = d.StartReading().Reader;
            _set = (HashSet<T>)r.ReadObject();
            _itemAdded = new ObservableEventHandler<CollectionAddKeyEvent>( r );
            _itemRemoved = new ObservableEventHandler<CollectionRemoveKeyEvent>( r );
            _collectionCleared = new ObservableEventHandler<CollectionClearEvent>( r );
        }

        /// <summary>
        /// The serialization method.
        /// </summary>
        /// <param name="s">The target binary serializer.</param>
        void Write( BinarySerializer s )
        {
            s.WriteObject( _set );
            _itemAdded.Write( s );
            _itemRemoved.Write( s );
            _collectionCleared.Write( s );
        }

        /// <summary>
        /// Gets the number of items contained in this set.
        /// </summary>
        public int Count => _set.Count;

        internal override ObjectExportedKind ExportedKind => ObjectExportedKind.List;

        public bool IsReadOnly => ((ICollection<T>)_set).IsReadOnly;

        /// <summary>
        /// Adds a new item.
        /// </summary>
        /// <param name="item">Item to add.</param>
        /// <returns>True if the item has actually been added, false it ot was already in this set.</returns>
        public bool Add( T item )
        {
            if( _set.Add( item ) )
            {
                var e = ActualDomain.OnCollectionAddKey( this, item );
                if( e != null && _itemAdded.HasHandlers ) _itemAdded.Raise( this, e );
                return true;
            }
            return false;
        }

        /// <summary>
        /// Adds multiple items at once (simple helper that calls <see cref="Add(T)"/> for each of them).
        /// </summary>
        /// <param name="items">Set of items to append.</param>
        public void AddRange( IEnumerable<T> items )
        {
            foreach( var i in items ) Add( i );
        }

        /// <summary>
        /// Clears this list of all its items.
        /// </summary>
        public void Clear()
        {
            if( _set.Count > 0 )
            {
                var e = ActualDomain.OnCollectionClear( this );
                _set.Clear();
                if( e != null && _collectionCleared.HasHandlers ) _collectionCleared.Raise( this, e );
            }
        }

        /// <summary>
        /// Removes an item.
        /// </summary>
        /// <param name="item">The item to remove.</param>
        /// <returns>True if item has been successfully removed.</returns>
        public bool Remove( T item )
        {
            if( _set.Remove( item ) )
            {
                var e = ActualDomain.OnCollectionRemoveKey( this, item );
                if( e != null && _itemRemoved.HasHandlers ) _itemRemoved.Raise( this, e );
                return true;
            }
            return false;
        }

        /// <summary>
        /// Gets whether the given item can be found in this set.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns>True if the item can be found in this set.</returns>
        public bool Contains( T item ) => _set.Contains( item );

        /// <summary>
        /// Copies the entire list to a compatible one-dimensional
        /// array, starting at the specified index of the target array.
        /// </summary>
        /// <param name="array">
        /// The one-dimensional System.Array that is the destination of the elements copied
        /// from this list. The System.Array must have zero-based indexing.
        /// </param>
        /// <param name="arrayIndex">The zero-based index in array at which copying begins. Must not be negative.</param>
        public void CopyTo( T[] array, int arrayIndex ) => _set.CopyTo( array, arrayIndex );

        /// <summary>
        /// Returns an enumerator that iterates through this list.
        /// </summary>
        /// <returns>The set of items.</returns>
        public IEnumerator<T> GetEnumerator() => _set.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _set.GetEnumerator();

        /// <summary>
        /// Determines whether this set is a proper subset of the specified collection.
        /// </summary>
        /// <param name="other">The collection to compare to this set.</param>
        /// <returns>true if this set is a proper subset of <paramref name="other"/>; otherwise, false.</returns>
        public bool IsProperSubsetOf( IEnumerable<T> other )
        {
            return _set.IsProperSubsetOf( other );
        }

        /// <summary>
        /// Determines whether this set is a proper superset of the specified collection.
        /// </summary>
        /// <param name="other">The collection to compare to this set.</param>
        /// <returns>true if this set is a proper superset of <paramref name="other"/>; otherwise, false.</returns>
        public bool IsProperSupersetOf( IEnumerable<T> other )
        {
            return _set.IsProperSupersetOf( other );
        }

        /// <summary>Determines whether this set is a subset of the specified collection.</summary>
        /// <param name="other">The collection to compare to this set.</param>
        /// <returns>true if this set is a subset of <paramref name="other"/>; otherwise, false.</returns>
        public bool IsSubsetOf( IEnumerable<T> other )
        {
            return _set.IsSubsetOf( other );
        }

        /// <summary>
        /// Determines whether this set is a proper superset of the specified collection.
        /// </summary>
        /// <param name="other">The collection to compare to this set.</param>
        /// <returns>true if this set is a superset of <paramref name="other"/>; otherwise, false.</returns>
        public bool IsSupersetOf( IEnumerable<T> other )
        {
            return _set.IsSupersetOf( other );
        }

        /// <summary>
        /// Determines whether this set and a specified collection share common elements.</summary>
        /// <param name="other">The collection to compare to this set.</param>
        /// <returns>true if this set and <paramref name="other"/> share at least one common element; otherwise, false.</returns>
        public bool Overlaps( IEnumerable<T> other )
        {
            return _set.Overlaps( other );
        }

        /// <summary>
        /// Determines whether this set and the specified collection contain the same elements.
        /// </summary>
        /// <param name="other">The collection to compare to this set.</param>
        /// <returns>true if this set is equal to <paramref name="other"/>; otherwise, false.</returns>
        public bool SetEquals( IEnumerable<T> other )
        {
            return _set.SetEquals( other );
        }

    }
}