using CK.BinarySerialization;
using CK.Core;
using System;
using System.Collections.Generic;

namespace CK.Observable;

/// <summary>
/// A simple event channel or bus that acts as an always empty <see cref="ObservableList{T}"/>: items
/// are inserted via <see cref="Send(T)"/>, <see cref="ItemSent"/> is raised, but items are not kept.
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
[SerializationVersion(0)]
public class ObservableChannel<T> : ObservableObject
{
    ObservableEventHandler<ListInsertEvent> _itemSent;

    /// <summary>
    /// Raised by <see cref="Send(T)"/>. Note that <see cref="ListInsertEvent.Index"/> is always 0.
    /// </summary>
    public event SafeEventHandler<ListInsertEvent> ItemSent
    {
        add => _itemSent.Add( value, nameof( ItemSent ) );
        remove => _itemSent.Remove( value );
    }

    /// <summary>
    /// Initializes a new <see cref="ObservableChannel{T}"/>.
    /// </summary>
    public ObservableChannel()
    {
    }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    protected ObservableChannel( BinarySerialization.Sliced _ ) : base( _ ) { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    public ObservableChannel( IBinaryDeserializer d, ITypeReadInfo info )
            : base( Sliced.Instance )
    {
        _itemSent = new ObservableEventHandler<ListInsertEvent>( d );
    }

    public static void Write( IBinarySerializer s, in ObservableChannel<T> o )
    {
        o._itemSent.Write( s );
    }

    /// <summary>
    /// Exports this channel as an empty list of items.
    /// </summary>
    /// <param name="num">The object number.</param>
    /// <param name="exporter">The target exporter.</param>
    public void Export( int num, ObjectExporter exporter )
    {
        exporter.ExportList( num, Array.Empty<T>() );
    }

    internal override ObjectExportedKind ExportedKind => ObjectExportedKind.List;

    /// <summary>
    /// Sends an item into this channel.
    /// </summary>
    /// <param name="item">The item to send. May be null for reference type.</param>
    public void Send( T item )
    {
        var e = ActualDomain.OnListInsert( this, 0, item );
        if( e != null && _itemSent.HasHandlers ) _itemSent.Raise( this, e );
    }

    /// <summary>
    /// Simple helper that calls <see cref="Send(T)"/> for each item.
    /// </summary>
    /// <param name="items">The items to send.</param>
    public void Send( IEnumerable<T> items )
    {
        foreach( var i in items ) Send( i );
    }
    
}
