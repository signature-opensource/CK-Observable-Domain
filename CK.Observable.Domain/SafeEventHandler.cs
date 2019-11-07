using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable
{
    /// <summary>
    /// Represents the method that will handle an event that has no data: just like
    /// the standard <see cref="EventHandler"/> except that these events can be bound only to <see cref="IDisposableObject"/> object's
    /// instance methods and static methods (of any type), that they are serialized and that the instance tracking is done automatically
    /// when <see cref="IDisposable.Dispose"/> is called on any target.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">An object that contains no event data (<see cref="EventArgs.Empty"/> should be used).</param>
    public delegate void SafeEventHandler( object sender, EventArgs e );

}
