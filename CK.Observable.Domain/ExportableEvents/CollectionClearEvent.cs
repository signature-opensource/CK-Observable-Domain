namespace CK.Observable
{
    /// <summary>
    /// Specialized <see cref="ObservableEvent"/> that exposes the fact tha a collection object has been
    /// cleared (<see cref="ObservableList{T}.Clear"/> or <see cref="ObservableDictionary{TKey, TValue}.Clear"/> for instance).
    /// </summary>
    public class CollectionClearEvent : ObservableEvent, ICollectionEvent
    {
        /// <summary>
        /// Gets the collection object identifier.
        /// This can safely be persisted or marshalled to desynchronized processes.
        /// </summary>
        public ObservableObjectId ObjectId { get; }

        /// <summary>
        /// Gets the collection object itself.
        /// Must be used in read only during the direct handling of the event.
        /// </summary>
        public ObservableObject Object { get; }

        /// <summary>
        /// Initializes a new <see cref="CollectionClearEvent"/>.
        /// </summary>
        /// <param name="o">The collection object.</param>
        public CollectionClearEvent( ObservableObject o )
            : base( ObservableEventType.CollectionClear )
        {
            ObjectId = o.OId;
            Object = o;
        }

        /// <summary>
        /// Emits this event data (object index only).
        /// </summary>
        /// <param name="e">The target exporter.</param>
        protected override void ExportEventData( ObjectExporter e )
        {
            e.Target.EmitInt32( ObjectId.Index );
        }

        /// <summary>
        /// Overridden to provide the type and detail about this event.
        /// </summary>
        /// <returns>A readable string.</returns>
        public override string ToString() => $"{EventType} {ObjectId}.";
    }
}
