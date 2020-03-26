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
        /// Creates and adds a new domain.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="name">The new domain name.</param>
        /// <param name="rootTypes">The root types.</param>
        /// <returns>The managed domain.</returns>
        IManagedDomain CreateDomain( IActivityMonitor monitor, string name, IReadOnlyList<string> rootTypes );

        IManagedDomain RebindDomain( IActivityMonitor monitor, string name, IReadOnlyList<string> rootTypes );
    }
}
