using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable
{
    /// <summary>
    /// This interface enables <see cref="ObservableObject"/> to work with a <see cref="ObservableDomainSidekick"/>.
    /// Object that supports this interface are submitted to the given sidekick (see <see cref="ObservableDomainSidekick.RegisterClientObject(Core.IActivityMonitor, IDestroyableObject)"/>)
    /// so that the sidekick and the object can collaborate.
    /// Note that the <see cref="UseSidekickAttribute"/> just ensures that a sidekick must be up and running.
    /// </summary>
    public interface ISidekickClientObject<TSidekick> where TSidekick : ObservableDomainSidekick
    {
        internal Type SidekickType => typeof( TSidekick );
    }
}
