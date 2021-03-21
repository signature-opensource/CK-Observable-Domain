namespace CK.Observable
{
    /// <summary>
    /// Specialized <see cref="ObservableEvent"/> that exposes the disposal of a <see cref="ObservableObject"/>.
    /// </summary>
    public class DisposedObjectEvent : ObservableEvent
    {
        /// <summary>
        /// Gets the object identifier.
        /// This can safely be persisted or marshalled to desynchronized processes.
        /// </summary>
        public ObservableObjectId ObjectId { get; }

        /// <summary>
        /// Gets the object itself.
        /// Must be used in read only during the direct handling of the event.
        /// </summary>
        public ObservableObject Object { get; }

        /// <summary>
        /// Initializes a new <see cref="DisposedObjectEvent"/>.
        /// </summary>
        /// <param name="o">The disposed object.</param>
        public DisposedObjectEvent( ObservableObject o )
            : base( ObservableEventType.DisposedObject )
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
        public override string ToString() => $"{EventType} {ObjectId} ({Object.GetType().Name}).";

    }
}
