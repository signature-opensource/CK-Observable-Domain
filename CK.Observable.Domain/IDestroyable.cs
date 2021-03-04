using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable
{
    /// <summary>
    /// Exposes <see cref="IsDestroyed"/> property and <see cref="Destroyed"/> event but not the Destroy method.
    /// This interface is used to monitor destroying, not to trigger it.
    /// <para>
    /// The <see cref="IDestroyableObject"/> extends this interface and adds the <see cref="IDestroyableObject.Destroy()"/> method.
    /// </para>
    /// <para>
    /// The <see cref="IDisposable"/> is not welcome here since there is absolutely no sense to use the using statement/dispose pattern
    /// on <see cref="ObservableObject"/> or any other objects managed by a domain.
    /// </para>
    /// </summary>
    public interface IDestroyable
    {
        /// <summary>
        /// Raised when this object is destroyed and will not be part of its <see cref="ObservableDomain"/> anymore.
        /// </summary>
        event SafeEventHandler<ObservableDomainEventArgs> Destroyed;

        /// <summary>
        /// Gets whether this object has been disposed.
        /// </summary>
        bool IsDestroyed { get; }
    }

    /// <summary>
    /// Provides <see cref="CheckDestroyed"/> extension method.
    /// </summary>
    public static class DestroyableObjectExtensions
    {
        /// <summary>
        /// Throws an <see cref="ObjectDestroyedException"/> if this <see cref="IDestroyable.IsDestroyed"/> is true.
        /// </summary>
        /// <param name="this">This object.</param>
        public static void CheckDestroyed( this IDestroyable @this )
        {
            if( @this.IsDestroyed ) throw new ObjectDestroyedException( @this.ToString() );
        }

    }

}
