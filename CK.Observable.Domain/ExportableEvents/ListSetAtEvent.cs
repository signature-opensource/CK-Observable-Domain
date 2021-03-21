namespace CK.Observable
{
    /// <summary>
    /// Specialized <see cref="ObservableEvent"/> that exposes the replacement of an item in an indexed list.
    /// Typically applies to <see cref="ObservableList{T}"/>.
    /// </summary>
    public class ListSetAtEvent : ObservableEvent, ICollectionEvent
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
        /// Gets the index where the value has been set.
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// Gets the value set.
        /// </summary>
        public object Value { get; }

        /// <summary>
        /// Initializes a new <see cref="ListSetAtEvent"/>.
        /// </summary>
        /// <param name="o">The list object.</param>
        /// <param name="index">The index.</param>
        /// <param name="value">The value at the index.</param>
        public ListSetAtEvent( ObservableObject o, int index, object value )
            : base( ObservableEventType.ListSetAt )
        {
            ObjectId = o.OId;
            Object = o;
            Index = index;
            Value = value;
        }

        /// <summary>
        /// Emits this event data (object index, index and value).
        /// </summary>
        /// <param name="e">The target exporter.</param>
        protected override void ExportEventData( ObjectExporter e )
        {
            e.Target.EmitInt32( ObjectId.Index );
            e.Target.EmitInt32( Index );
            ExportEventObject( e, Value );
        }

        /// <summary>
        /// Overridden to provide the type and detail about this event.
        /// </summary>
        /// <returns>A readable string.</returns>
        public override string ToString() => $"{EventType} {ObjectId}[{Index}] = {Value ?? "null"}.";

    }


}
