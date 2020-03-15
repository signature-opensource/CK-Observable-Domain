namespace CK.Observable
{
    /// <summary>
    /// Specialized <see cref="ObservableEvent"/> that exposes the removal of a key in a map or a collection of items.
    /// Typically applies to <see cref="ObservableDictionary{TKey, TValue}"/>.
    /// </summary>
    public class CollectionRemoveKeyEvent : ObservableEvent, ICollectionEvent
    {
        /// <summary>
        /// Gets the map or collection object identifier.
        /// This can safely be persisted or marshalled to desynchronized processes.
        /// </summary>
        public ObservableObjectId ObjectId { get; }

        /// <summary>
        /// Gets the map or collection object itself.
        /// Must be used in read only during the direct handling of the event.
        /// </summary>
        public ObservableObject Object { get; }

        /// <summary>
        /// Gets the key that has been removed.
        /// </summary>
        public object Key { get; }

        /// <summary>
        /// Initializes a new <see cref="CollectionRemoveKeyEvent"/>.
        /// </summary>
        /// <param name="o">The map or collection object.</param>
        /// <param name="key">The removed key.</param>
        public CollectionRemoveKeyEvent( ObservableObject o, object key )
            : base( ObservableEventType.CollectionRemoveKey )
        {
            ObjectId = o.OId;
            Object = o;
            Key = key;
        }

        /// <summary>
        /// Emits this event data (object index and the key).
        /// </summary>
        /// <param name="e">The target exporter.</param>
        protected override void ExportEventData( ObjectExporter e )
        {
            e.Target.EmitInt32( ObjectId.Index );
            ExportEventObject( e, Key );
        }

        /// <summary>
        /// Overridden to provide the type and detail about this event.
        /// </summary>
        /// <returns>A readable string.</returns>
        public override string ToString() => $"{EventType} {ObjectId}[{Key}]";
    }
}
