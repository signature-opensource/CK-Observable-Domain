using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable.League
{
    /// <summary>
    /// Provides a tracking reference and a isolation shell on a loaded <see cref="IObservableDomain"/>
    /// in a <see cref="ObservableLeague"/>. This is a base interface: <see cref="IObservableDomainShell"/>,
    /// <see cref="IObservableDomainShell{T}"/> and the the others provide clean type handling.
    /// <para>
    /// The <see cref="DisposeAsync(IActivityMonitor)"/> must be called once this domain is no more required.
    /// </para>
    /// <para>
    /// Note that this <see cref="IAsyncDisposable.DisposeAsync"/> implementation will use the activity monitor that has been
    /// used to <see cref="IObservableDomainLoader.LoadAsync(IActivityMonitor)"/> this shell.
    /// </para>
    /// </summary>
    public interface IObservableDomainShellBase : IAsyncDisposable
    {
        /// <summary>
        /// Gets the domain name.
        /// </summary>
        string DomainName { get; }

        /// <summary>
        /// Gets whether this domain is destroyed (the <see cref="Coordinator"/>'s <see cref="Domain"/> has been disposed).
        /// </summary>
        bool IsDestroyed { get; }

        /// <summary>
        /// Saves this domain (only if it needs to be saved because the effect of last transaction has not been saved yet).
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <returns>True on success, false on error.</returns>
        Task<bool> SaveAsync( IActivityMonitor monitor );

        /// <summary>
        /// Releases this shell.
        /// The domain is unloaded if this is the last released shell.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <returns>True if the managed domain has actually be unloaded (ie. disposed)..</returns>
        ValueTask<bool> DisposeAsync( IActivityMonitor monitor );
    }
}
