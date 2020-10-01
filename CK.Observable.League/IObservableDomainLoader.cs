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
        /// Loads this domain (if it is not yet loaded) and returns a shell on which <see cref="IObservableDomainShellBase.DisposeAsync(IActivityMonitor)"/>
        /// must be called once the domain is not needed anymore.
        /// If <see cref="IsDestroyed"/> is true or if the containing <see cref="ObservableLeague"/> is closing, or an error occurred,
        /// then this returns null.
        /// </summary>
        /// <returns>The shell to use. Null on error or if the domain is destroyed or the league is closing.</returns>
        Task<IObservableDomainShell?> LoadAsync( IActivityMonitor monitor );

        /// <summary>
        /// Loads this domain (if it is not yet loaded) as a strongly typed one and returns a
        /// shell on which <see cref="IObservableDomainShellBase.DisposeAsync(IActivityMonitor)"/> must be called once the domain is not needed anymore.
        /// If the actual type is not compatible with this or <see cref="IsDestroyed"/> is true or if the containing <see cref="ObservableLeague"/>
        /// is closing, or an error occurred, then this returns null.
        /// </summary>
        /// <returns>The shell to use. Null on error or if the domain is destroyed or the league is closing.</returns>
        Task<IObservableDomainShell<T>?> LoadAsync<T>( IActivityMonitor monitor ) where T : ObservableRootObject;

        /// <summary>
        /// Loads this domain (if it is not yet loaded) as a strongly typed one and returns a
        /// shell on which <see cref="IObservableDomainShellBase.DisposeAsync(IActivityMonitor)"/> must be called once the domain is not needed anymore.
        /// If the actual type is not compatible with this or <see cref="IsDestroyed"/> is true or if the containing <see cref="ObservableLeague"/>
        /// is closing, or an error occurred, then this returns null.
        /// </summary>
        /// <returns>The shell to use. Null on error or if the domain is destroyed or the league is closing.</returns>
        Task<IObservableDomainShell<T1, T2>?> LoadAsync<T1, T2>( IActivityMonitor monitor )
            where T1 : ObservableRootObject
            where T2 : ObservableRootObject;

        /// <summary>
        /// Loads this domain (if it is not yet loaded) as a strongly typed one and returns a
        /// shell on which <see cref="IObservableDomainShellBase.DisposeAsync(IActivityMonitor)"/> must be called once the domain is not needed anymore.
        /// If the actual type is not compatible with this or <see cref="IsDestroyed"/> is true or if the containing <see cref="ObservableLeague"/>
        /// is closing, or an error occurred, then this returns null.
        /// </summary>
        /// <returns>The shell to use. Null on error or if the domain is destroyed or the league is closing.</returns>
        Task<IObservableDomainShell<T1, T2, T3>?> LoadAsync<T1, T2, T3>( IActivityMonitor monitor )
            where T1 : ObservableRootObject
            where T2 : ObservableRootObject
            where T3 : ObservableRootObject;

        /// <summary>
        /// Loads this domain (if it is not yet loaded) as a strongly typed one and returns a
        /// shell on which <see cref="IObservableDomainShellBase.DisposeAsync(IActivityMonitor)"/> must be called once the domain is not needed anymore.
        /// If the actual type is not compatible with this or <see cref="IsDestroyed"/> is true or if the containing <see cref="ObservableLeague"/>
        /// is closing, or an error occurred, then this returns null.
        /// </summary>
        /// <returns>The shell to use. Null on error or if the domain is destroyed or the league is closing.</returns>
        Task<IObservableDomainShell<T1, T2, T3, T4>?> LoadAsync<T1, T2, T3, T4>( IActivityMonitor monitor )
            where T1 : ObservableRootObject
            where T2 : ObservableRootObject
            where T3 : ObservableRootObject
            where T4 : ObservableRootObject;
    }
}
