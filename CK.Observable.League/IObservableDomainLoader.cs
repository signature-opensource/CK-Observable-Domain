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
        /// Loads this domain (if it is not yet loaded) and returns a shell on which <see cref="IObservableDomainShell.DisposeAsync(IActivityMonitor)"/>
        /// must be called once the domain is not needed anymore.
        /// </summary>
        /// <returns>The shell to use. Null on error.</returns>
        Task<IObservableDomainShell?> LoadAsync( IActivityMonitor monitor );

    }
}
