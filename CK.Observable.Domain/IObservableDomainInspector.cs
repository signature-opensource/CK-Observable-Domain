using CK.Core;
using System;
using System.Threading.Tasks;

namespace CK.Observable;

/// <summary>
/// Exposes low-level and debug accessors and methods of an <see cref="ObservableDomain"/>.
/// </summary>
public interface IObservableDomainInspector
{
    /// <summary>
    /// Gets the <see cref="LostObjectTracker"/> that has been computed by the last <see cref="Save"/> call.
    /// Use <see cref="EnsureLostObjectTracker(IActivityMonitor, int)"/> to refresh it.
    /// </summary>
    ObservableDomain.LostObjectTracker? CurrentLostObjectTracker { get; }

    /// <summary>
    /// Updates <see cref="CurrentLostObjectTracker"/> if its <see cref="LostObjectTracker.TransactionNumber"/> is
    /// not the current <see cref="TransactionSerialNumber"/>.
    /// On error (or if a read access failed to be obtained), returns null.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="millisecondsTimeout">
    /// The maximum number of milliseconds to wait for a read access before giving up.
    /// Wait indefinitely by default.
    /// </param>
    /// <returns>The tracker on success, null if timeout occurred.</returns>
    ObservableDomain.LostObjectTracker? EnsureLostObjectTracker( IActivityMonitor monitor, int millisecondsTimeout = -1 );

    /// <summary>
    /// Triggers a garbage collection on this domain.
    /// First, <see cref="EnsureLostObjectTracker(IActivityMonitor, int)"/> is called to update the <see cref="CurrentLostObjectTracker"/>
    /// and then, the detected lost objects are unloaded in a <see cref="ObservableDomain.ModifyAsync(IActivityMonitor, Action?, bool, int, bool, bool, bool)"/>.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="millisecondsTimeout">
    /// The maximum number of milliseconds to wait for the write access before giving up (this is also used for
    /// the read access if <see cref="EnsureLostObjectTracker(IActivityMonitor, int)"/> must update the <see cref="CurrentLostObjectTracker"/>).
    /// Waits indefinitely by default.
    /// </param>
    /// <returns>True on success, false if timeout or an error occurred.</returns>
    Task<bool> GarbageCollectAsync( IActivityMonitor monitor, int millisecondsTimeout = -1 );

    /// <summary>
    /// Called on each successful transaction on this domain: provides a way to inspect the <see cref="ObservableEvent"/> emitted by a transaction.
    /// </summary>
    event Action<ITransactionDoneEvent>? TransactionDone;
}
