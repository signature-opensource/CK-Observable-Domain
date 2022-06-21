using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable.League
{
    /// <summary>
    /// Internal interface: this is what a <see cref="OCoordinatorRoot"/> sees.
    /// </summary>
    interface IManagedLeague
    {
        /// <summary>
        /// Creates and adds a new domain: this is called by <see cref="OCoordinatorRoot.CreateDomain(string, IEnumerable{string}?, ManagedDomainOptions?)"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="name">The new domain name.</param>
        /// <param name="rootTypes">The root types.</param>
        /// <returns>The managed domain.</returns>
        IInternalManagedDomain CreateDomain( IActivityMonitor monitor, string name, IReadOnlyList<string> rootTypes );

        /// <summary>
        /// Called when the <see cref="OCoordinatorRoot"/> has been reloaded (from snapshot): this is called
        /// to find the managed domains and ensure that their key information (their root types) are the same
        /// as the ones of the coordinator's domains (otherwise an exception is raised).
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="name">The domain name that must be found or created.</param>
        /// <param name="rootTypes">The root types that, if the managed domain already exist, must match.</param>
        /// <returns>The managed domain.</returns>
        IInternalManagedDomain RebindDomain( IActivityMonitor monitor, string name, IReadOnlyList<string> rootTypes );

        /// <summary>
        /// Called whenever a domain is destroyed: the <see cref="OCoordinatorRoot"/>'s <see cref="ODomain"/> has been disposed.
        /// The managed domain is removed from the domains.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="d">The destroyed domain.</param>
        void OnDestroy( IActivityMonitor monitor, IInternalManagedDomain d );
    }
}
