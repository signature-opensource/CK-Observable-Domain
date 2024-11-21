namespace CK.Observable;


/// <summary>
/// Specialized <see cref="ObservableEvent"/> that exposes the apparition of a new property name on an object.
/// It is "new" in the sense that this <see cref="Name"/> has never been exported before and should be associated to
/// its <see cref="PropertyId"/>.
/// </summary>
public class NewPropertyEvent : ObservableEvent
{
    /// <summary>
    /// Gets the name of the new property.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the identifier that will be used to denote this property.
    /// </summary>
    public int PropertyId { get; }

    /// <summary>
    /// Initializes a new <see cref="NewPropertyEvent"/>.
    /// </summary>
    /// <param name="id">The property identifier.</param>
    /// <param name="name">The new property name.</param>
    public NewPropertyEvent( int id, string name )
        : base( ObservableEventType.NewProperty )
    {
        PropertyId = id;
        Name = name;
    }


    /// <summary>
    /// Emits this event data (property name and identifier).
    /// </summary>
    /// <param name="e">The target exporter.</param>
    protected override void ExportEventData( ObjectExporter e )
    {
        e.Target.EmitString( Name );
        e.Target.EmitInt32( PropertyId );
    }

    /// <summary>
    /// Overridden to provide the type and detail about this event.
    /// </summary>
    /// <returns>A readable string.</returns>
    public override string ToString() => $"{EventType} {Name} -> {PropertyId}.";

}
