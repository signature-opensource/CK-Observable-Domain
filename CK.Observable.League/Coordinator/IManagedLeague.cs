using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable.League
{
    /// <summary>
    /// Internal interface: this is what a <see cref="Coordinator"/> sees.
    /// </summary>
    interface IManagedLeague
    {
        /// <summary>
        /// Creates and adds a new domain: this is called by <see cref="Coordinator.CreateDomain(string, IEnumerable{string})"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="name">The new domain name.</param>
        /// <param name="rootTypes">The root types.</param>
        /// <returns>The managed domain.</returns>
        IManagedDomain CreateDomain( IActivityMonitor monitor, string name, IReadOnlyList<string> rootTypes );

        /// <summary>
        /// Called when the <see cref="Coordinator"/> has been reloaded (from snapshot): this is called
        /// to find the managed domain and ensure that its key information (the root types) are the same
        /// (otherwise an exception is raised).
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="name">The domain name that must be found or created.</param>
        /// <param name="rootTypes">The root types that, if the managed domain already exist, must match.</param>
        /// <returns>The managed domain.</returns>
        IManagedDomain RebindDomain( IActivityMonitor monitor, string name, IReadOnlyList<string> rootTypes );

        /// <summary>
        /// Called whenever a domain is destroyed: the <see cref="Coordinator"/>'s <see cref="Domain"/> has been disposed.
        /// The managed domain is removed from the domains.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="d">The destroyed domain.</param>
        void OnDestroy( IActivityMonitor monitor, IManagedDomain d );
    }
}
