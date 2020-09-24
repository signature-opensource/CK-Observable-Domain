using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable
{
    /// <summary>
    /// Exposes <see cref="IsDisposed"/> property and <see cref="Disposed"/> event and not Dispose method.
    /// This interface is used to monitor disposing, not to trigger it: this is why it doesn't extend <see cref="IDisposable"/>.
    /// <para>
    /// Another reason why IDisposable is not welcome here is that there is absolutely no sense to use the using statement/dispose pattern
    /// on <see cref="ObservableObject"/> or any other objects managed by a domain.
    /// </para>
    /// </summary>
    public interface IDisposableObject
    {
        /// <summary>
        /// Raised when this object is disposed and will not be part of its <see cref="ObservableDomain"/> anymore.
        /// </summary>
        event SafeEventHandler<ObservableDomainEventArgs> Disposed;

        /// <summary>
        /// Gets whether this object has been disposed.
        /// </summary>
        bool IsDisposed { get; }
    }

    /// <summary>
    /// Provides <see cref="CheckDisposed"/> extension method.
    /// </summary>
    public static class DisposableObjectExtensions
    {
        /// <summary>
        /// Throws an <see cref="ObjectDisposedException"/> if this <see cref="IDisposableObject.IsDisposed"/> is true.
        /// </summary>
        /// <param name="this">This object.</param>
        public static void CheckDisposed( this IDisposableObject @this )
        {
            if( @this.IsDisposed ) throw new ObjectDisposedException( @this.ToString() );
        }

    }

}
