using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable
{
    /// <summary>
    /// Extends <see cref="IReadOnlyDictionary{TKey, TValue}"/> to expose events.
    /// </summary>
    /// <typeparam name="TKey">Type of the key.</typeparam>
    /// <typeparam name="TValue">Type of the value.</typeparam>
    public interface IObservableReadOnlyDictionary<TKey, TValue> : IReadOnlyDictionary<TKey, TValue>
    {
        /// <summary>
        /// Raised when an existing item has been updated by <see cref="IDictionary{TKey, TValue}[TKey]"/> to a different value.
        /// </summary>
        event SafeEventHandler<CollectionMapSetEvent> ItemSet;

        /// <summary>
        /// Raised when a new item has been added by <see cref="IDictionary{TKey, TValue}[TKey]"/> or <see cref="IDictionary{TKey, TValue}.Add(TKey, TValue)"/>.
        /// </summary>
        event SafeEventHandler<CollectionMapSetEvent> ItemAdded;

        /// <summary>
        /// Raised by <see cref="IDictionary{TKey, TValue}.Clear"/>.
        /// </summary>
        event SafeEventHandler<CollectionClearEvent> CollectionCleared;

        /// <summary>
        /// Raised by <see cref="IDictionary{TKey, TValue}.Remove(TKey)"/>.
        /// </summary>
        event SafeEventHandler<CollectionRemoveKeyEvent> ItemRemoved;

    }
}
