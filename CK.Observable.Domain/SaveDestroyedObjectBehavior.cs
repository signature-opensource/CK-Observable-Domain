using CK.Core;

namespace CK.Observable
{
    /// <summary>
    /// Defines what must me done when a <see cref="IDestroyableObject.IsDestroyed"/> object is saved.
    /// </summary>
    public enum SaveDestroyedObjectBehavior
    {
        /// <summary>
        /// Nothing is done: destroyed objects are saved just as other objects and will be restored
        /// in their destroyed state in the object graph.
        /// </summary>
        None,

        /// <summary>
        /// Same as <see cref="None"/>, except that a <see cref="LogLevel.Warn"/> that lists the saved destroyed objects is logged.
        /// </summary>
        LogWarning,

        /// <summary>
        /// Same as <see cref="None"/>, except that a <see cref="LogLevel.Error"/> that lists the saved destroyed objects is logged.
        /// </summary>
        LogError,

        /// <summary>
        /// Saving a destroyed object is forbidden: cascade destroying of any <see cref="IDestroyableObject"/> has to be
        /// fully handled during the transaction (the destroyed object must not be referenced anymore by any other domain's objects), 
        /// </summary>
        Throw
    }
}
