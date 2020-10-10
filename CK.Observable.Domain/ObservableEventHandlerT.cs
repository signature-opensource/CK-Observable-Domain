using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable
{
    /// <summary>
    /// Serializable and safe event handler: only non null and static method or method on a <see cref="IDisposableObject"/> (that must
    /// be serializable) can be added.
    /// This is a helper class that implements <see cref="SafeEventHandler{TEventArgs}"/> events.
    /// </summary>
    /// <typeparam name="TEventArgs">The type of the event argument.</typeparam>
    public struct ObservableEventHandler<TEventArgs> where TEventArgs : EventArgs
    {
        ObservableDelegate _handler;

        /// <summary>
        /// Deserializes the <see cref="ObservableEventHandler{TEventArgs}"/>.
        /// </summary>
        /// <param name="r">The deserializer.</param>
        public ObservableEventHandler( IBinaryDeserializer r ) => _handler = new ObservableDelegate( r );

        /// <summary>
        /// Serializes this <see cref="ObservableEventHandler{TEventArgs}"/>.
        /// </summary>
        /// <param name="w">The writer.</param>
        public void Write( BinarySerializer w ) => _handler.Write( w );

        /// <summary>
        /// Gets whether at least one handler is registered.
        /// </summary>
        public bool HasHandlers => _handler.HasHandlers;

        /// <summary>
        /// Adds a handler.
        /// </summary>
        /// <param name="h">The handler must be non null and be a static method or a method on a <see cref="ObservableObject"/>.</param>
        /// <param name="eventName">The event name (used for error messages).</param>
        public void Add( SafeEventHandler<TEventArgs> h, string eventName ) => _handler.Add( h, eventName );

        /// <summary>
        /// Removes a handler and returns true if it has been removed.
        /// </summary>
        /// <param name="h">The handler to remove. Can be null.</param>
        /// <returns>True if the handler has been removed, false otherwise.</returns>
        public bool Remove( SafeEventHandler<TEventArgs> h ) => _handler.Remove( h );

        /// <summary>
        /// Clears the delegate list.
        /// </summary>
        public void RemoveAll() => _handler.RemoveAll();

        /// <summary>
        /// Raises this event.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="args">The event argument.</param>
        public void Raise( object sender, TEventArgs args )
        {
            var h = _handler.Cleanup();
            for( int i = 0; i < h.Length; ++i )
            {
                ((SafeEventHandler<TEventArgs>)h[i]).Invoke( sender, args );
            }
        }

        /// <summary>
        /// Raises this event, logging any exception that could be thrown by the registered handlers.
        /// This should be used with care: failing fast should be the rule in an Observable domain transaction.
        /// </summary>
        /// <param name="monitor">The monitor that will log any exception.</param>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="args">The event argument.</param>
        /// <returns>True on success, false if at least one handler has thrown.</returns>
        public bool SafeRaise( IActivityMonitor monitor, object sender, TEventArgs args )
        {
            if( monitor == null ) throw new ArgumentNullException( nameof( monitor ) );
            bool success = true;
            var h = _handler.Cleanup();
            for( int i = 0; i < h.Length; ++i )
            {
                try
                {
                    ((SafeEventHandler<TEventArgs>)h[i]).Invoke( sender, args );
                }
                catch( Exception ex )
                {
                    monitor.Error( "While raising event.", ex );
                    success = false;
                }
            }
            return success;
        }

    }
}
