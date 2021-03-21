namespace CK.Observable
{
    /// <summary>
    /// Specialized <see cref="ObservableEvent"/> that exposes the insertion of an item in an indexed list.
    /// Typically applies to <see cref="ObservableList{T}"/>.
    /// </summary>
    public class ListInsertEvent : ObservableEvent, ICollectionEvent
    {
        /// <summary>
        /// Gets the list object identifier.
        /// This can safely be persisted or marshalled to desynchronized processes.
        /// </summary>
        public ObservableObjectId ObjectId { get; }

        /// <summary>
        /// Gets the list object itself.
        /// Must be used in read only during the direct handling of the event.
        /// </summary>
        public ObservableObject Object { get; }

        /// <summary>
        /// Gets the index of the insertion.
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// Gets the inserted value.
        /// </summary>
        public object? Item { get; }

        /// <summary>
        /// Initializes a new <see cref="ListInsertEvent"/>.
        /// </summary>
        /// <param name="o">The list object.</param>
        /// <param name="index">The inserted index.</param>
        /// <param name="item">The inserted value.</param>
        public ListInsertEvent( ObservableObject o, int index, object? item )
            : base( ObservableEventType.ListInsert )
        {
            ObjectId = o.OId;
            Object = o;
            Index = index;
            Item = item;
        }

        /// <summary>
        /// Emits this event data (object index, the index and the value).
        /// </summary>
        /// <param name="e">The target exporter.</param>
        protected override void ExportEventData( ObjectExporter e )
        {
            e.Target.EmitInt32( ObjectId.Index );
            e.Target.EmitInt32( Index );
            ExportEventObject( e, Item );
        }

        /// <summary>
        /// Overridden to provide the type and detail about this event.
        /// </summary>
        /// <returns>A readable string.</returns>
        public override string ToString() => $"{EventType} {ObjectId}[{Index}] = {Item}.";

    }


}
