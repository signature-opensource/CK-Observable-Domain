using System;
using CK.Core;

namespace CK.Observable;

/// <summary>
/// Specialized <see cref="ObservableEvent"/> that exposes the apparition of a new <see cref="ObservableObject"/>.
/// </summary>
public class NewObjectEvent : ObservableEvent
{
    /// <summary>
    /// Gets the new object identifier.
    /// This can safely be persisted or marshalled to desynchronized processes.
    /// </summary>
    public ObservableObjectId ObjectId { get; }

    /// <summary>
    /// Gets the new object itself.
    /// Must be used in read only during the direct handling of the event.
    /// </summary>
    public ObservableObject Object { get; }

    /// <summary>
    /// Gets the kind of the new object.
    /// </summary>
    public ObjectExportedKind ExportedKind { get;}

    /// <summary>
    /// Initializes a new <see cref="NewObjectEvent"/>.
    /// When this constructor is called the new object identifier that has been computed is not yet
    /// made available on the object (this occurs during the construction of the <see cref="ObservableObject"/>).
    /// </summary>
    /// <param name="o">The new object.</param>
    /// <param name="oid">The new object identifier (will be the <see cref="ObservableObject.OId"/>).</param>
    public NewObjectEvent( ObservableObject o, ObservableObjectId oid )
        : base( ObservableEventType.NewObject )
    {
        ObjectId = oid;
        Object = o;
        ExportedKind = o.ExportedKind;
    }

    /// <summary>
    /// Emits this event data (the new object index and the <see cref="ExportedKind"/> as a
    /// string: A(rray), M(ap), S(et) or the empty string for <see cref="ObjectExportedKind.Object"/>).
    /// </summary>
    /// <param name="e">The target exporter.</param>
    protected override void ExportEventData( ObjectExporter e )
    {
        e.Target.EmitInt32( ObjectId.Index );
        switch( ExportedKind )
        {
            case ObjectExportedKind.Object: e.Target.EmitString( "" ); break;
            case ObjectExportedKind.List: e.Target.EmitString( "A" ); break;
            case ObjectExportedKind.Map: e.Target.EmitString( "M" ); break;
            case ObjectExportedKind.Set: e.Target.EmitString( "S" ); break;
            default: Throw.NotSupportedException(); break;
        }
    }

    /// <summary>
    /// Overridden to provide the type and detail about this event.
    /// </summary>
    /// <returns>A readable string.</returns>
    public override string ToString() => $"{EventType} {ObjectId} ({Object.GetType().ToCSharpName( withNamespace:false )}).";
}
