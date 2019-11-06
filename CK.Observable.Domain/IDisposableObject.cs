using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable
{
    /// <summary>
    /// Extends <see cref="IDisposable"/> to expose <see cref="IsDisposed"/> property
    /// and <see cref="Disposed"/> event.
    /// </summary>
    public interface IDisposableObject : IDisposable
    {
        /// <summary>
        /// Raised when this object is disposed and will not be part of its <see cref="ObservableDomain"/> anymore.
        /// </summary>
        event SafeEventHandler<EventMonitoredArgs> Disposed;

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
