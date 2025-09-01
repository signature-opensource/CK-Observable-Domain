using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable;

/// <summary>
/// Extends <see cref="IDestroyable"/> to expose the <see cref="IDestroyableObject.Destroy()"/> method.
/// <para>
/// This interface generalizes <see cref="InternalObject"/>, <see cref="ObservableObject"/> and <see cref="ObservableTimedEventBase"/>
/// and ony them (as it can only be implemented in this assembly).
/// </para>
/// </summary>
public interface IDestroyableObject : IDestroyable
{
    /// <summary>
    /// Destroys this object.
    /// </summary>
    void Destroy();

    internal void LocalImplementationOnly();
}
