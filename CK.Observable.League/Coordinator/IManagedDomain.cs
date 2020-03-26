using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable.League
{
    interface IManagedDomain
    {
        /// <summary>
        /// Gets whether the domain can be loaded, at least because the domain type can be resolved.
        /// </summary>
        bool IsLoadable { get; }

        /// <summary>
        /// Gets whether the domain is currently loaded.
        /// </summary>
        bool IsLoaded { get; }

        /// <summary>
        /// Gets the domain name.
        /// </summary>
        string DomainName { get; }

        /// <summary>
        /// Gets or sets the domain options.
        /// </summary>
        ManagedDomainOptions Options { get; set; }

    }
}
