using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable
{
    /// <summary>
    /// Represents the method that will handle an event with an event argument: just like
    /// the standard <see cref="EventHandler{T}"/> except that these events can be bound only to <see cref="IDisposableObject"/> object's
    /// instance methods and static methods (of any type), that they are serialized and that the instance tracking is done automatically
    /// when <see cref="IDisposable.Dispose"/> is called on any target.
    /// <para>
    /// Use the <see cref="ObservableEventHandler{TEventArgs}"/> to implement such events.
    /// </para>
    /// </summary>
    /// <typeparam name="TEventArgs">The type of the event data.</typeparam>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">An object that contains the event data.</param>
    public delegate void SafeEventHandler<TEventArgs>( object sender, TEventArgs e );
}
