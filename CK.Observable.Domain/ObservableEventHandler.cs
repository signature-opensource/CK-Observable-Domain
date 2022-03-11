using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable
{
    /// <summary>
    /// Serializable and safe event handler with no argument: only static methods or methods on a <see cref="IDestroyable"/> (that must
    /// be serializable) can be registered. 
    /// <para>
    /// This is a helper class that implements <see cref="SafeEventHandler"/> events.
    /// This field MUST not be readonly. See <see cref="ObservableEventHandler{TEventArgs}"/>
    /// </para>
    /// </summary>
    public struct ObservableEventHandler
    {
        ObservableDelegate _handler;

        /// <summary>
        /// Deserializes the <see cref="ObservableEventHandler"/>.
        /// If the method has been suppressed, use the static helper <see cref="Skip(IBinaryDeserializer)"/>.
        /// </summary>
        /// <param name="r">The deserializer.</param>
        public ObservableEventHandler( IBinaryDeserializer r ) => _handler = new ObservableDelegate( r );

        /// <summary>
        /// Helper that skips a serialized event to be used when an event is removed.
        /// </summary>
        /// <param name="r">The deserializer.</param>
        public static void Skip( IBinaryDeserializer r ) => ObservableDelegate.Skip( r );

        /// <summary>
        /// Deserializes the <see cref="ObservableEventHandler"/>.
        /// If the method has been suppressed, use the static helper <see cref="Skip(IBinaryDeserializer)"/>.
        /// </summary>
        /// <param name="d">The deserializer.</param>
        public ObservableEventHandler( BinarySerialization.IBinaryDeserializer d ) => _handler = new ObservableDelegate( d );

        /// <summary>
        /// Helper that skips a serialized event to be used when an event is removed.
        /// </summary>
        /// <param name="r">The deserializer.</param>
        public static void Skip( BinarySerialization.IBinaryDeserializer d ) => ObservableDelegate.Skip( d );

        /// <summary>
        /// Serializes this <see cref="ObservableEventHandler"/>.
        /// </summary>
        /// <param name="w">The writer.</param>
        public void Write( BinarySerialization.IBinarySerializer s ) => _handler.Write( s );

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
        public void Raise( object sender )
        {
            var h = _handler.Cleanup();
            for( int i = 0; i < h.Length; ++i )
            {
                ((SafeEventHandler)h[i]).Invoke( sender );
            }
        }

        /// <summary>
        /// Raises this event, logging any exception that could be thrown by the registered handlers.
        /// This should be used with care: failing fast should be the rule in an Observable domain transaction.
        /// </summary>
        /// <param name="monitor">The monitor that will log any exception.</param>
        /// <param name="sender">The sender of the event.</param>
        /// <returns>True on success, false if at least one handler has thrown.</returns>
        public bool SafeRaise( IActivityMonitor monitor, object sender )
        {
            if( monitor == null ) throw new ArgumentNullException( nameof( monitor ) );
            bool success = true;
            var h = _handler.Cleanup();
            for( int i = 0; i < h.Length; ++i )
            {
                try
                {
                    ((SafeEventHandler)h[i]).Invoke( sender );
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
