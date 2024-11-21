namespace CK.Observable;

/// <summary>
/// Specialized <see cref="ObservableEvent"/> that exposes an <see cref="ObservableObject"/>'s property change.
/// </summary>
public class PropertyChangedEvent : ObservableEvent
{
    /// <summary>
    /// Gets the object identifier.
    /// This can safely be persisted or marshaled to desynchronized processes.
    /// </summary>
    public ObservableObjectId ObjectId { get; }

    /// <summary>
    /// Gets the object itself. Must be used in read only during the direct handling of the event.
    /// </summary>
    public ObservableObject Object { get; }

    /// <summary>
    /// Gets the property identifier used instead of <see cref="PropertyName"/>.
    /// </summary>
    public int PropertyId { get; }

    /// <summary>
    /// Gets the property name.
    /// </summary>
    public string PropertyName { get; }

    /// <summary>
    /// Gets the value that changed.
    /// </summary>
    public object? Value { get; }

    /// <summary>
    /// INitializes a new <see cref="PropertyChangedEvent"/>.
    /// </summary>
    /// <param name="o">The object.</param>
    /// <param name="propertyId">The property identifier.</param>
    /// <param name="propertyName">The property name.</param>
    /// <param name="value">The new value.</param>
    public PropertyChangedEvent( ObservableObject o, int propertyId, string propertyName, object? value )
        : base( ObservableEventType.PropertyChanged )
    {
        ObjectId = o.OId;
        Object = o;
        PropertyId = propertyId;
        PropertyName = propertyName;
        Value = value;
    }

    /// <summary>
    /// Emits this event data (object index, property identifier and the new <see cref="Value"/>).
    /// </summary>
    /// <param name="e">The target exporter.</param>
    protected override void ExportEventData( ObjectExporter e )
    {
        e.Target.EmitInt32( ObjectId.Index );
        e.Target.EmitInt32( PropertyId );
        ExportEventObject( e, Value );
    }

    /// <summary>
    /// Overridden to provide the type and detail about this event.
    /// </summary>
    /// <returns>A readable string.</returns>
    public override string ToString() => $"{EventType} {ObjectId}.{PropertyName} = {Value ?? "null"}.";

}
