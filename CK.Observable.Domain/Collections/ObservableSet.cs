using CK.BinarySerialization;
using CK.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace CK.Observable;

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
public class ObservableSet<T> : ObservableObject, IObservableReadOnlySet<T> where T : notnull
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
    /// Specialized deserialization constructor for specialized classes.
    /// </summary>
    /// <param name="_">Unused parameter.</param>
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    protected ObservableSet( Sliced _ ) : base( _ ) { }
#pragma warning restore CS8618

    ObservableSet( IBinaryDeserializer d, ITypeReadInfo info )
        : base( Sliced.Instance )
    {
        _set = d.ReadObject<HashSet<T>>()!;
        _itemAdded = new ObservableEventHandler<CollectionAddKeyEvent>( d );
        _itemRemoved = new ObservableEventHandler<CollectionRemoveKeyEvent>( d );
        _collectionCleared = new ObservableEventHandler<CollectionClearEvent>( d );
    }

    /// <summary>
    /// The serialization method.
    /// </summary>
    /// <param name="s">The target binary serializer.</param>
    public static void Write( IBinarySerializer s, in ObservableSet<T> o )
    {
        s.WriteObject( o._set );
        o._itemAdded.Write( s );
        o._itemRemoved.Write( s );
        o._collectionCleared.Write( s );
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
    /// <returns>True if the item has actually been added, false if it was already in this set.</returns>
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
    /// Clears this set of all its items.
    /// </summary>
    public void Clear() => Clear( false );

    /// <summary>
    /// Clears this set of all its items, optionally destroying all items that are <see cref="IDestroyableObject"/>.
    /// </summary>
    /// <param name="destroyItems">True to call <see cref="IDestroyableObject.Destroy()"/> on destroyable items.</param>
    public void Clear( bool destroyItems )
    {
        if( _set.Count > 0 )
        {
            if( destroyItems )
            {
                DestroyItems();
            }
            _set.Clear();
            var e = ActualDomain.OnCollectionClear( this );
            if( e != null && _collectionCleared.HasHandlers ) _collectionCleared.Raise( this, e );
        }
    }

    /// <summary>
    /// Destroys any <see cref="IDestroyableObject"/> items that may appear in this set.
    /// </summary>
    protected void DestroyItems()
    {
        // Take a snapshot so that OnDestroyed reflexes can safely alter the _set.
        foreach( var d in _set.OfType<IDestroyableObject>().ToArray() )
        {
            d.Destroy();
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
