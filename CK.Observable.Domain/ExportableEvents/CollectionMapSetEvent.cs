namespace CK.Observable
{
    /// <summary>
    /// Specialized <see cref="ObservableEvent"/> that exposes a new association of a key to a value in a map.
    /// Typically applies to <see cref="ObservableDictionary{TKey, TValue}"/>.
    /// </summary>
    public class CollectionMapSetEvent : ObservableEvent, ICollectionEvent
    {
        /// <summary>
        /// Gets the map object identifier.
        /// This can safely be persisted or marshalled to desynchronized processes.
        /// </summary>
        public ObservableObjectId ObjectId { get; }

        /// <summary>
        /// Gets the map object itself.
        /// Must be used in read only during the direct handling of the event.
        /// </summary>
        public ObservableObject Object { get; }

        /// <summary>
        /// Gets the key that is set.
        /// </summary>
        public object Key { get; }

        /// <summary>
        /// Gets the associated value that has been set.
        /// </summary>
        public object? Value { get; }

        /// <summary>
        /// Initializes a new <see cref="CollectionMapSetEvent"/>.
        /// </summary>
        /// <param name="o">The map object.</param>
        /// <param name="key">The key set.</param>
        /// <param name="value">The value set.</param>
        public CollectionMapSetEvent( ObservableObject o, object key, object? value )
            : base( ObservableEventType.CollectionMapSet )
        {
            ObjectId = o.OId;
            Object = o;
            Key = key;
            Value = value;
        }

        /// <summary>
        /// Emits this event data (object index, the key and the value).
        /// </summary>
        /// <param name="e">The target exporter.</param>
        protected override void ExportEventData( ObjectExporter e )
        {
            e.Target.EmitInt32( ObjectId.Index );
            ExportEventObject( e, Key );
            ExportEventObject( e, Value );
        }

        /// <summary>
        /// Overridden to provide the type and detail about this event.
        /// </summary>
        /// <returns>A readable string.</returns>
        public override string ToString() => $"{EventType} {ObjectId}[{Key}] = {Value ?? "null"}";
    }
}
