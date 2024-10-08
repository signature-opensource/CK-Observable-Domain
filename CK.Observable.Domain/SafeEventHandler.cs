using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable;

/// <summary>
/// Represents the method that will handle an event that has no event argument: just like
/// the standard <see cref="EventHandler"/> (but without the need to use a <see cref="EventArgs.Empty"/>) except that these
/// events can be bound only to <see cref="IDestroyable"/> object's instance methods and static methods (of any type), that
/// they are serialized and that the instance tracking is done automatically
/// when <see cref="IDisposable.Dispose"/> is called on any target.
/// <para>
/// Use the <see cref="ObservableEventHandler"/> to implement such events.
/// </para>
/// </summary>
/// <param name="sender">The source of the event.</param>
/// <param name="e">An object that contains no event data.</param>
public delegate void SafeEventHandler( object sender );
