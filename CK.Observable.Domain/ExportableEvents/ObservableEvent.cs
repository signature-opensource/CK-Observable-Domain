using System;

namespace CK.Observable;

/// <summary>
/// Base class for all observable events. Such events are emitted by ModifyAsync methods
/// and are enough to fully synchronize a remote associated domain view.
/// These events must be handled by <see cref="IObservableDomainClient.OnTransactionCommit(TransactionDoneEventArgs)"/> (write lock held),
/// or by <see cref="ObservableDomainSidekick.OnTransactionResult(TransactionDoneEventArgs)"/> (read lock held), directly after the
/// modifications because direct domain objects are exposed by these events (for example the <see cref="PropertyChangedEvent.Value"/>): these
/// events CANNOT be correctly handled outside of these 2 extension points.
/// </summary>
public abstract class ObservableEvent : EventArgs
{
    static readonly string[] _exportCodes =
        {
            null!,
            "N",  // NewObject
            "D",  // DisposedObject
            "P",  // NewProperty
            "C",  // PropertyChanged
            "I",  // ListInsert
            "CL", // CollectionClear
            "R",  // ListRemoveAt
            "S",  // ListSetAt
            "K",  // CollectionRemoveKey
            "M",  // CollectionMapSet,
            "A"   // CollectionAddKey
        };

    /// <summary>
    /// Initializes a new <see cref="ObservableEvent"/>.
    /// </summary>
    /// <param name="type">The event type.</param>
    protected ObservableEvent( ObservableEventType type )
    {
        EventType = type;
    }

    /// <summary>
    /// Gets the event type.
    /// </summary>
    public ObservableEventType EventType { get; }

    /// <summary>
    /// Exports this event: this is a <see cref="ObjectExportedKind.List"/> with
    /// the <see cref="EventType"/> as a string and then the result of
    /// the <see cref="ExportEventData"/> call.
    /// </summary>
    /// <param name="e">The target exporter.</param>
    public void Export( ObjectExporter e )
    {
        e.Target.EmitStartObject( -1, ObjectExportedKind.List );
        e.Target.EmitString( _exportCodes[(int)EventType] );
        ExportEventData( e );
        e.Target.EmitEndObject( -1, ObjectExportedKind.List );
    }

    /// <summary>
    /// Helper method to export an object.
    /// </summary>
    /// <param name="e">The target exporter.</param>
    /// <param name="o">The object to export.</param>
    protected void ExportEventObject( ObjectExporter e, object? o )
    {
        if( o is ObservableObject obs )
        {
            e.Target.EmitStartObject( -1, ObjectExportedKind.Object );
            e.Target.EmitPropertyName( "=" );
            e.Target.EmitInt32( obs.OId.Index );
            e.Target.EmitEndObject( -1, ObjectExportedKind.Object );
        }
        else
        {
            e.ExportObject( o );
        }
    }

    /// <summary>
    /// Abstract method called by <see cref="Export(ObjectExporter)"/> that must export the
    /// data specific the concrete event.
    /// </summary>
    /// <param name="e">The target exporter.</param>
    protected abstract void ExportEventData( ObjectExporter e );
}
