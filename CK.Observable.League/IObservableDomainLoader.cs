using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable.League
{
    /// <summary>
    /// Defines a loader of <see cref="ObservableDomain"/> in a <see cref="ObservableLeague"/>. This loader
    /// handles multiple accessors to a domain thanks to <see cref="IObservableDomainShell"/>.
    /// </summary>
    public interface IObservableDomainLoader
    {
        /// <summary>
        /// Gets the domain name.
        /// </summary>
        string DomainName { get; }

        /// <summary>
        /// Gets whether this domain is loaded.
        /// </summary>
        bool IsLoaded { get; }

        /// <summary>
        /// Gets whether this domain is destroyed (the <see cref="Coordinator"/>'s <see cref="Domain"/> has been disposed).
        /// </summary>
        bool IsDestroyed { get; }

        /// <summary>
        /// Loads this domain (if it is not yet loaded) and returns a shell on which <see cref="IObservableDomainShell.DisposeAsync(IActivityMonitor)"/>
        /// must be called once the domain is not needed anymore.
        /// If <see cref="IsDestroyed"/> is true or if the containing <see cref="ObservableLeague"/> is closing, or an error occurred,
        /// then this returns null.
        /// </summary>
        /// <returns>The shell to use. Null on error or if the domain is destroyed or the league is closing.</returns>
        Task<IObservableDomainShell?> LoadAsync( IActivityMonitor monitor );

    }
}
