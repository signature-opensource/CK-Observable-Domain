using CK.BinarySerialization;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace CK.Observable
{
    /// <summary>
    /// Serializable and safe event handler: only static methods or methods on a <see cref="IDestroyable"/> (that must
    /// be serializable) can be registered.
    /// <para>
    /// When suppressing the event, <see cref="ObservableEventHandler.Skip(BinarySerialization.IBinaryDeserializer)"/>
    /// must be used when deserializing from a previous version that had the event.
    /// </para>
    /// <para>
    /// This is a helper class that implements <see cref="SafeEventHandler{TEventArgs}"/> events.
    /// This field MUST not be readonly. The pattern is the following one:
    /// <code>
    /// // Declare a private non readonly field:
    /// ObservableEventHandler&lt;MyEventArgs&gt; _myEvent;
    /// 
    /// // In the Write method, saves it:
    /// o._myEvent.Write( s );
    /// 
    /// // In the Deserialization constructor, reads it back:
    /// _myEvent = new ObservableEventHandler&lt;MyEventArgs&gt;( d );
    /// 
    /// // Exposes the event:
    /// public event SafeEventHandler&lt;MyEventArgs&gt; MyEvent
    /// {
    ///    add => _myEvent.Add( value );
    ///    remove => _myEvent.Remove( value );
    /// }
    /// </code>
    /// </para>
    /// </summary>
    /// <typeparam name="TEventArgs">The type of the event argument.</typeparam>
    public struct ObservableEventHandler<TEventArgs> where TEventArgs : EventArgs
    {
        ObservableDelegate _handler;

        /// <summary>
        /// Deserializes the <see cref="ObservableEventHandler{TEventArgs}"/>.
        /// If the method has been suppressed, use the static helper <see cref="ObservableEventHandler.Skip(IBinaryDeserializer)"/>.
        /// </summary>
        /// <param name="r">The deserializer.</param>
        public ObservableEventHandler( IBinaryDeserializer d ) => _handler = new ObservableDelegate( d );

        /// <summary>
        /// Serializes this <see cref="ObservableEventHandler{TEventArgs}"/>.
        /// </summary>
        /// <param name="w">The writer.</param>
        public void Write( IBinarySerializer s ) => _handler.Write( s );

        /// <summary>
        /// Gets whether at least one handler is registered.
        /// </summary>
        public bool HasHandlers => _handler.HasHandlers;

        /// <summary>
        /// Adds a handler.
        /// </summary>
        /// <param name="h">The handler must be non null and be a static method or a method on a <see cref="ObservableObject"/>.</param>
        /// <param name="eventName">The event name (used for error messages).</param>
        public void Add( SafeEventHandler<TEventArgs> h, [CallerMemberName] string? eventName = null ) => _handler.Add( h, eventName );

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
            Throw.CheckNotNullArgument( monitor );
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
