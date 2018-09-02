
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System;
using System.Diagnostics;

namespace CK.Observable
{
    /// <summary>
    /// A <see cref="CKSortedArrayKeyList{T,TKey}"/> that implements <see cref="CollectionChanged"/> and <see cref="PropertyChanged"/> events
    /// in order to be an observable list.
    /// </summary>
    public class CKObservableSortedArrayKeyList<T, TKey> : CKSortedArrayKeyList<T, TKey>, ICKObservableReadOnlyList<T>
    {
        /// <summary>
        /// Initializes a new <see cref="CKObservableSortedArrayKeyList{T,TKey}"/> with a default comparison function.
        /// </summary>
        /// <param name="keySelector">The function that select the key from an item.</param>
        /// <param name="allowDuplicates">True to allow duplicate items.</param>
        public CKObservableSortedArrayKeyList( Func<T, TKey> keySelector, bool allowDuplicates = false )
            : base( keySelector, allowDuplicates )
        {
        }

        /// <summary>
        /// Initializes a new <see cref="CKObservableSortedArrayKeyList{T,TKey}"/> with a specific comparison function.
        /// </summary>
        /// <param name="keySelector">The function that select the key from an item.</param>
        /// <param name="keyComparison">Comparison function for keys.</param>
        /// <param name="allowDuplicates">True to allow duplicate items.</param>
        public CKObservableSortedArrayKeyList( Func<T, TKey> keySelector, Comparison<TKey> keyComparison, bool allowDuplicates = false )
            : base( keySelector, keyComparison, allowDuplicates )
        {
        }

        /// <summary>
        /// Standard <see cref="INotifyCollectionChanged"/> event.
        /// </summary>
        public event NotifyCollectionChangedEventHandler CollectionChanged;

        /// <summary>
        /// Standard <see cref="INotifyPropertyChanged"/> event.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Raises the <see cref="CollectionChanged"/> event.
        /// </summary>
        /// <param name="e">Event argument.</param>
        protected virtual void OnCollectionChanged( NotifyCollectionChangedEventArgs e )
        {
            var h = CollectionChanged;
            if( h != null ) h( this, e );
        }

        /// <summary>
        /// Raises the <see cref="PropertyChanged"/> event (for "Count" and "Item[]" property).
        /// </summary>
        /// <param name="e">Event argument.</param>
        protected virtual void OnPropertyChanged( PropertyChangedEventArgs e )
        {
            var h = PropertyChanged;
            if( h != null ) h( this, e );
        }

        /// <summary>
        /// Overridden to trigger the necessary events.
        /// </summary>
        /// <param name="index">Index to insert.</param>
        /// <param name="value">Item to insert.</param>
        protected override void DoInsert( int index, T value )
        {
            base.DoInsert( index, value );
            RaiseAdd( index, value );
        }

        /// <summary>
        /// Raises the event corresponding to <see cref="DoInsert"/> (<see cref="NotifyCollectionChangedAction.Add"/>).
        /// </summary>
        /// <param name="index">Inserted index.</param>
        /// <param name="value">Inserted item.</param>
        protected virtual void RaiseAdd( int index, T value )
        {
            OnPropertyChanged( CollectionExtension.CountChangedEventArgs );
            OnPropertyChanged( CollectionExtension.ItemArrayChangedEventArgs );
            OnCollectionChanged( new NotifyCollectionChangedEventArgs( NotifyCollectionChangedAction.Add, value, index ) );
        }

        /// <summary>
        /// Overridden to trigger the necessary events.
        /// </summary>
        /// <param name="index">Index to remove.</param>
        protected override void DoRemoveAt( int index )
        {
            var item = this[index];
            base.DoRemoveAt( index );
            RaiseRemove( index, item );
        }

        /// <summary>
        /// Raises the event corresponding to <see cref="DoRemoveAt"/> (<see cref="NotifyCollectionChangedAction.Remove"/>).
        /// </summary>
        /// <param name="index">Removed index.</param>
        /// <param name="value">Removed item.</param>
        protected virtual void RaiseRemove( int index, T value )
        {
            OnPropertyChanged( CollectionExtension.CountChangedEventArgs );
            OnPropertyChanged( CollectionExtension.ItemArrayChangedEventArgs );
            OnCollectionChanged( new NotifyCollectionChangedEventArgs( NotifyCollectionChangedAction.Remove, value, index ) );
        }

        /// <summary>
        /// Overridden to trigger the necessary events.
        /// </summary>
        /// <param name="index">The position to set.</param>
        /// <param name="newValue">The new item to inject.</param>
        /// <returns>The previous item at the position.</returns>
        protected override T DoSet( int index, T newValue )
        {
            T oldValue = base.DoSet( index, newValue );
            RaiseReplace( index, newValue, oldValue );
            return oldValue;
        }

        /// <summary>
        /// Raises the event corresponding to <see cref="DoSet"/> (<see cref="NotifyCollectionChangedAction.Replace"/>).
        /// </summary>
        /// <param name="index">Replaced index.</param>
        /// <param name="newValue">New item.</param>
        /// <param name="oldValue">Replaced item.</param>
        protected virtual void RaiseReplace( int index, T newValue, T oldValue )
        {
            OnPropertyChanged( CollectionExtension.ItemArrayChangedEventArgs );
            OnCollectionChanged( new NotifyCollectionChangedEventArgs( NotifyCollectionChangedAction.Replace, oldValue, newValue, index ) );
        }

        /// <summary>
        /// Overridden to trigger the necessary events.
        /// </summary>
        /// <param name="from">Old index of the item.</param>
        /// <param name="newIndex">New index.</param>
        /// <returns>The new index of the element.</returns>
        protected override int DoMove( int from, int newIndex )
        {
            newIndex = base.DoMove( from, newIndex );
            if( newIndex != from ) RaiseMove( from, newIndex );
            return newIndex;
        }

        /// <summary>
        /// Raises the event corresponding to <see cref="DoMove"/> (<see cref="NotifyCollectionChangedAction.Move"/>).
        /// </summary>
        /// <param name="from">Original index.</param>
        /// <param name="newIndex">Target index.</param>
        protected virtual void RaiseMove( int from, int newIndex )
        {
            OnPropertyChanged( CollectionExtension.ItemArrayChangedEventArgs );
            OnCollectionChanged( new NotifyCollectionChangedEventArgs( NotifyCollectionChangedAction.Move, Store[newIndex], newIndex, from ) );
        }

        /// <summary>
        /// Overridden to trigger the necessary events.
        /// </summary>
        protected override void DoClear()
        {
            base.DoClear();
            RaiseReset();
        }

        /// <summary>
        /// Raises the event corresponding to <see cref="DoClear"/> (<see cref="NotifyCollectionChangedAction.Reset"/>).
        /// </summary>
        protected virtual void RaiseReset()
        {
            OnPropertyChanged( CollectionExtension.CountChangedEventArgs );
            OnPropertyChanged( CollectionExtension.ItemArrayChangedEventArgs );
            OnCollectionChanged( new NotifyCollectionChangedEventArgs( NotifyCollectionChangedAction.Reset ) );
        }
    }
}
