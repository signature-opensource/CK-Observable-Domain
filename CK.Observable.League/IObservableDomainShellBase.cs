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
    /// <see cref="IObservableDomainShell{T}"/> and the others provide clean type handling.
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
        /// This sends the <see cref="ObservableDomain.SnapshotDomainCommand"/> command from inside a ModifyAsync so that an updated
        /// memory snapshot is made if needed and calls the internal <see cref="IObservableDomainClient"/> to save it into
        /// the store.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <returns>True on success, false if any error prevented the save.</returns>
        Task<bool> SaveAsync( IActivityMonitor monitor );

        /// <summary>
        /// Gets an inspector for the domain.
        /// This inspector can be used only until <see cref="DisposeAsync(IActivityMonitor)"/> or <see cref="IAsyncDisposable.DisposeAsync()"/>
        /// is called otherwise an <see cref="ObjectDisposedException"/> is raised.
        /// </summary>
        IObservableDomainInspector DomainInspector { get; }

        /// <summary>
        /// Releases this shell.
        /// The domain is unloaded if this is the last released shell.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <returns>True if the managed domain has actually been unloaded.</returns>
        ValueTask<bool> DisposeAsync( IActivityMonitor monitor );
    }
}
