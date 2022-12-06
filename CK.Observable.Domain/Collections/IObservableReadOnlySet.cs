using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable
{
    /// <summary>
    /// Extends <see cref="IReadOnlyCollection{T}"/> but describes a set of items where items are uniquely identified.
    /// As soon as possible, this must extend the new IReadOnlySet{T} interface available in .Net 5.0.
    /// </summary>
    /// <typeparam name="T">Type of the item.</typeparam>
    public interface IObservableReadOnlySet<out T> : IReadOnlyCollection<T>
    {
        /// <summary>
        /// Raised when a new item has been added by <see cref="ObservableSet{T}.Add(T)"/>.
        /// </summary>
        event SafeEventHandler<CollectionAddKeyEvent> ItemAdded;

        /// <summary>
        /// Raised by <see cref="ObservableSet{T}.Remove(T)"/>.
        /// </summary>
        event SafeEventHandler<CollectionRemoveKeyEvent> ItemRemoved;

        /// <summary>
        /// Raised by <see cref="ObservableSet{T}.Clear()"/>.
        /// </summary>
        event SafeEventHandler<CollectionClearEvent> CollectionCleared;

    }
}
