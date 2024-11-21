using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable;

/// <summary>
/// Exposes <see cref="BinarySerialization.IDestroyable"/> property and <see cref="Destroyed"/> event but not the Destroy method.
/// This interface is used to monitor destroying, not to trigger it.
/// <para>
/// The <see cref="IDestroyableObject"/> extends this interface and adds the <see cref="IDestroyableObject.Destroy()"/> method.
/// </para>
/// <para>
/// The <see cref="IDisposable"/> is not welcome here since there is absolutely no sense to use the using statement/dispose pattern
/// on <see cref="ObservableObject"/> or any other objects managed by a domain.
/// </para>
/// </summary>
public interface IDestroyable : BinarySerialization.IDestroyable
{
    /// <summary>
    /// Raised when this object is destroyed and will not be part of its <see cref="ObservableDomain"/> anymore.
    /// </summary>
    event SafeEventHandler<ObservableDomainEventArgs> Destroyed;
}

/// <summary>
/// Provides <see cref="CheckDestroyed"/> extension method.
/// </summary>
public static class DestroyableObjectExtensions
{
    /// <summary>
    /// Throws an <see cref="ObjectDestroyedException"/> if this <see cref="BinarySerialization.IDestroyable.IsDestroyed"/> is true.
    /// </summary>
    /// <param name="this">This object.</param>
    public static void CheckDestroyed( this IDestroyable @this )
    {
        if( @this.IsDestroyed ) ThrowObjectDestroyedException( @this.ToString()! );
    }

    /// <summary>
    /// Helper that throws the <see cref="ObjectDestroyedException"/>.
    /// </summary>
    /// <param name="message">The exception message (usually the destroyed object's type name).</param>
    /// <exception cref="ObjectDestroyedException">Alway throw.</exception>
    public static void ThrowObjectDestroyedException( string message )
    {
        throw new ObjectDestroyedException( message );
    }

}
