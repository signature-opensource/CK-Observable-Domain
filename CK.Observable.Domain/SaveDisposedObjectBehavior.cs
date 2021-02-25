using CK.Core;

namespace CK.Observable
{
    /// <summary>
    /// Defines what must me done when a <see cref="IDestroyableObject.IsDisposed"/> object is saved.
    /// </summary>
    public enum SaveDisposedObjectBehavior
    {
        /// <summary>
        /// Nothing is done: disposed objects are saved just as other objects and will be restored
        /// in their disposed state in the object graph.
        /// </summary>
        None,

        /// <summary>
        /// Same as <see cref="None"/>, except that a <see cref="LogLevel.Warn"/> that lists the saved disposed objects is logged.
        /// </summary>
        LogWarning,

        /// <summary>
        /// Same as <see cref="None"/>, except that a <see cref="LogLevel.Error"/> that lists the saved disposed objects is logged.
        /// </summary>
        LogError,

        /// <summary>
        /// Saving a disposed object is forbidden: disposal of any <see cref="IDestroyableObject"/> has to be fully handled during the
        /// transaction (the disposed object must not be referenced anymore by any other domain's objects), 
        /// </summary>
        Throw
    }
}
