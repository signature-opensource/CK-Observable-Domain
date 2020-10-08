namespace CK.Observable
{
    /// <summary>
    /// Specialized <see cref="ObservableEvent"/> that exposes the apparition of a key in a set of items
    /// (or any set-like collections). Typically applies to <see cref="ObservableSet{T}"/>.
    /// </summary>
    public class CollectionAddKeyEvent : ObservableEvent, ICollectionEvent
    {
        /// <summary>
        /// Gets the set object identifier.
        /// This can safely be persisted or marshalled to desynchronized processes.
        /// </summary>
        public ObservableObjectId ObjectId { get; }

        /// <summary>
        /// Gets the set-like object itself.
        /// Must be used in read only during the direct handling of the event.
        /// </summary>
        public ObservableObject Object { get; }

        /// <summary>
        /// Gets the object that has been added.
        /// </summary>
        public object Key { get; }

        /// <summary>
        /// Initializes a new <see cref="CollectionAddKeyEvent"/>.
        /// </summary>
        /// <param name="o">The set-like object.</param>
        /// <param name="key">The added object.</param>
        public CollectionAddKeyEvent( ObservableObject o, object key )
            : base( ObservableEventType.CollectionAddKey )
        {
            ObjectId = o.OId;
            Object = o;
            Key = key;
        }

        /// <summary>
        /// Emits this event data (object index and the added key).
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
