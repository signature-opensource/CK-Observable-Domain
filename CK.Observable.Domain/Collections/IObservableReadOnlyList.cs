using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable
{
    /// <summary>
    /// Extends <see cref="IReadOnlyList{T}"/> to expose events.
    /// </summary>
    /// <typeparam name="T">Type of the item.</typeparam>
    public interface IObservableReadOnlyList<out T> : IReadOnlyList<T>
    {
        /// <summary>
        /// Raised when an existing item has been updated by <see cref="ObservableList{T}.this[int]"/> to a different value.
        /// </summary>
        event SafeEventHandler<ListSetAtEvent> ItemSet;

        /// <summary>
        /// Raised when a new item has been added by <see cref="ObservableList{T}.Add(T)"/> or <see cref="ObservableList{T}.Insert(int, T)"/>.
        /// </summary>
        event SafeEventHandler<ListInsertEvent> ItemInserted;

        /// <summary>
        /// Raised by <see cref="ObservableList{T}.Remove(T)"/> or <see cref="ObservableList{T}.RemoveAt(int)"/>.
        /// </summary>
        event SafeEventHandler<ListRemoveAtEvent> ItemRemovedAt;

        /// <summary>
        /// Raised by <see cref="ObservableList{T}.Clear()"/>.
        /// </summary>
        event SafeEventHandler<CollectionClearEvent> CollectionCleared;

    }
}
