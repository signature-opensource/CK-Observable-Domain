namespace CK.Observable;

/// <summary>
/// Specialized <see cref="ObservableEvent"/> that exposes the removal of an item in an indexed list.
/// Typically applies to <see cref="ObservableList{T}"/>.
/// </summary>
public class ListRemoveAtEvent : ObservableEvent, ICollectionEvent
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
    /// Gets the index of the removal.
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// Initializes a new <see cref="ListRemoveAtEvent"/>.
    /// </summary>
    /// <param name="o">The list object.</param>
    /// <param name="index">The removed index.</param>
    public ListRemoveAtEvent( ObservableObject o, int index )
        : base( ObservableEventType.ListRemoveAt )
    {
        ObjectId = o.OId;
        Object = o;
        Index = index;
    }

    /// <summary>
    /// Emits this event data (object index and the index).
    /// </summary>
    /// <param name="e">The target exporter.</param>
    protected override void ExportEventData( ObjectExporter e )
    {
        e.Target.EmitInt32( ObjectId.Index );
        e.Target.EmitInt32( Index );
    }

    /// <summary>
    /// Overridden to provide the type and detail about this event.
    /// </summary>
    /// <returns>A readable string.</returns>
    public override string ToString() => $"{EventType} {ObjectId}[{Index}].";

}
