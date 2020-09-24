using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable
{
    /// <summary>
    /// This event argument is exposed by <see cref="DomainView.SidekickActivated"/>. It fully hides
    /// the actual <see cref="ObservableDomainSidekick"/> by enabling only to challenge the type of
    /// the activated sidekick and to submit a <see cref="ObservableObject"/> or <see cref="InternalObject"/>
    /// to it.
    /// </summary>
    public class SidekickActivatedEventArgs : EventMonitoredArgs 
    {
        readonly ObservableDomainSidekick _sidekick;

        internal SidekickActivatedEventArgs( IActivityMonitor monitor, ObservableDomainSidekick sidekick )
            : base( monitor )
        {
            _sidekick = sidekick;
        }

        /// <summary>
        /// Gets whether the activated <see cref="ObservableDomainSidekick"/> is compatible with the given type.
        /// </summary>
        /// <param name="t">The type to challenge.</param>
        /// <returns>True if the activated sidekick is compatible with the type to challenge, false otherwise.</returns>
        public bool IsOfType( Type t ) => t.IsAssignableFrom( _sidekick.GetType() );

        /// <summary>
        /// Registers the <see cref="ObservableObject"/> on the sidekick. It is up to the sidekick to handle
        /// this "client" object the way it wants.
        /// The semantics of this registration is specific to each sidekick: this Activation/Registration protocol
        /// is a framework that is as neutral as possible.
        /// </summary>
        /// <param name="o">The object to register.</param>
        public void RegisterObject( ObservableObject o ) => _sidekick.RegisterClientObject( Monitor, o );

        /// <summary>
        /// Registers the <see cref="InternalObject"/> on the sidekick. It is up to the sidekick to handle
        /// this "client" object the way it wants.
        /// The semantics of this registration is specific to each sidekick: this Activation/Registration protocol
        /// is a framework that is as neutral as possible.
        /// </summary>
        /// <param name="o">The object to register.</param>
        public void RegisterObject( InternalObject o ) => _sidekick.RegisterClientObject( Monitor, o );

    }
}
