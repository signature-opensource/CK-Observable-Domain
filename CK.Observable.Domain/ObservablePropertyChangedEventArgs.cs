using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace CK.Observable;

/// <summary>
/// Extends the standard <see cref="PropertyChangedEventArgs"/> with the <see cref="PropertyId"/>
/// associated to the <see cref="PropertyChangedEventArgs.PropertyName"/> (in the context of an ObservableDomain).
/// </summary>
public class ObservablePropertyChangedEventArgs : PropertyChangedEventArgs
{
    /// <summary>
    /// Gets the unique property indentifier.
    /// </summary>
    public int PropertyId { get; }

    /// <summary>
    /// Initializes a new <see cref="ObservablePropertyChangedEventArgs"/>.
    /// </summary>
    /// <param name="propertyId">The property identifier.</param>
    /// <param name="name">The property name.</param>
    public ObservablePropertyChangedEventArgs( int propertyId, string name )
        : base( name )
    {
        PropertyId = propertyId;
    }

    /// <summary>
    /// Builds a long value based on the <see cref="ObservableObjectId.Index"/> and <see cref="PropertyId"/>
    /// that can be used to identify property instance. This is used to track/dedup property changed event.
    /// </summary>
    /// <param name="o">The owning object.</param>
    /// <returns>The key to use for this property of the specified object.</returns>
    public long GetObjectPropertyId( ObservableObject o )
    {
        Debug.Assert( o.OId.IsValid );
        long r = o.OId.Index;
        return (r << 24) | (uint)PropertyId;
    }
}
