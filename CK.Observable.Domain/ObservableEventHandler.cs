using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable
{
    /// <summary>
    /// Serializable and checked event handler: only non null and static method or method on a <see cref="ObservableObject"/> can
    /// be added. 
    /// </summary>
    [SerializationVersion(0)]
    public struct ObservableEventHandler
    {
        readonly ObservableDelegate _handler;

        /// <summary>
        /// Deserializes the <see cref="ObservableEvent"/>.
        /// </summary>
        /// <param name="c">The context.</param>
        public ObservableEventHandler( IBinaryDeserializerContext c ) => _handler = new ObservableDelegate( c );

        /// <summary>
        /// Serializes this <see cref="ObservableEvent"/>.
        /// </summary>
        /// <param name="w">The writer.</param>
        public void Write( BinarySerializer w ) => _handler.Write( w );

        /// <summary>
        /// Gets whether at least one handler is registered.
        /// </summary>
        public bool HasHandlers => _handler.D != null;

        /// <summary>
        /// Adds a handler.
        /// </summary>
        /// <param name="h">The handler must be non null and be a static method or a method on a <see cref="ObservableObject"/>.</param>
        /// <param name="eventName">The event name (used for error messages).</param>
        public void Add( EventHandler h, string eventName ) => _handler.Add( h, eventName );

        /// <summary>
        /// Removes a handler and returns true if it has been removed.
        /// </summary>
        /// <param name="h">The handler to remove. Can be null.</param>
        /// <returns>True if the handler has been removed, false otherwise.</returns>
        public bool Remove( EventHandler h ) => _handler.Remove( h );

        /// <summary>
        /// Clears the delegate list.
        /// </summary>
        public void RemoveAll() => _handler.RemoveAll();

        /// <summary>
        /// Raises this event.
        /// </summary>
        /// <param name="monitor">Monitor to use. Cannot be null.</param>
        /// <param name="eventName">Name of the event. Can be null or empty.</param>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="args">The event argument.</param>
        /// <param name="throwException">False to catch exception and log any error as a warning.</param>
        public void Raise( IActivityMonitor monitor, object sender, EventArgs args, string eventName, bool throwException = true )
        {
            var h = _handler.Cleanup();
            for( int i = 0; i < h.Length; ++i )
            {
                try
                {
                    ((EventHandler)h[i]).Invoke( sender, args );
                }
                catch( Exception ex )
                {
                    if( throwException ) throw;
                    monitor.Warn( $"While raising '{eventName}'. Ignoring error.", ex );
                }
            }
        }


    }
}
