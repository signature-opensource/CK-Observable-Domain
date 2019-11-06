using System;

namespace CK.Observable
{
    /// <summary>
    /// Very simple command encapsulation: there is no constraint on the command type.
    /// </summary>
    public readonly struct ObservableCommand
    {
        /// <summary>
        /// The command sender (the one who emitted the command via <see cref="ObservableObject.SendCommand(object)"/>
        /// or <see cref="InternalObject.SendCommand(object)"/>.
        /// </summary>
        public readonly IDisposableObject Sender;

        /// <summary>
        /// The command object payload.
        /// </summary>
        public readonly object Payload;

        /// <summary>
        /// Initializes a new <see cref="ObservableCommand"/>.
        /// </summary>
        /// <param name="sender">The sender. Must not be null.</param>
        /// <param name="payload">The command payload. Must not be null.</param>
        public ObservableCommand( IDisposableObject sender, object payload )
        {
            Sender = sender ?? throw new ArgumentNullException( nameof( sender ) );
            Payload = payload ?? throw new ArgumentNullException( nameof( payload ) );
        }
    }
}
