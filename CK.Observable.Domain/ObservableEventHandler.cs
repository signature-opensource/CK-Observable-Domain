using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable
{
    /// <summary>
    /// Serializable and safe event handler: only non null and static method or method on a <see cref="IDisposableObject"/> (that must
    /// be serializable) can be added. 
    /// This is a helper class that implements <see cref="SafeEventHandler"/> events.
    /// </summary>
    [SerializationVersion(0)]
    public struct ObservableEventHandler
    {
        readonly ObservableDelegate _handler;

        /// <summary>
        /// Deserializes the <see cref="ObservableEventHandler"/>.
        /// </summary>
        /// <param name="c">The context.</param>
        public ObservableEventHandler( IBinaryDeserializer r ) => _handler = new ObservableDelegate( r );

        /// <summary>
        /// Serializes this <see cref="ObservableEventHandler"/>.
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
        public void Add( SafeEventHandler h, string eventName ) => _handler.Add( h, eventName );

        /// <summary>
        /// Removes a handler and returns true if it has been removed.
        /// </summary>
        /// <param name="h">The handler to remove. Can be null.</param>
        /// <returns>True if the handler has been removed, false otherwise.</returns>
        public bool Remove( SafeEventHandler h ) => _handler.Remove( h );

        /// <summary>
        /// Clears the delegate list.
        /// </summary>
        public void RemoveAll() => _handler.RemoveAll();

        /// <summary>
        /// Raises this event.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="args">The event argument.</param>
        public void Raise( object sender, EventArgs args )
        {
            var h = _handler.Cleanup();
            for( int i = 0; i < h.Length; ++i )
            {
                ((SafeEventHandler)h[i]).Invoke( sender, args );
            }
        }


    }
}
