using CK.Core;
using CK.PerfectEvent;
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
        /// Gets whether this domain is destroyed (the <see cref="OCoordinatorRoot"/>'s <see cref="ODomain"/> has been disposed).
        /// </summary>
        bool IsDestroyed { get; }

        /// <summary>
        /// Gets the transaction events if possible from a given transaction number.
        /// This returns null if an export is required (the <paramref name="transactionNumber"/> is too old),
        /// and an empty array if the transactionNumber is greater or equal to the current transaction number
        /// stored (this could happen: clients may be on par with the current transaction number).
        /// </summary>
        /// <param name="transactionNumber">
        /// The starting transaction number.
        /// Should be between 1 and the current transaction number (included), 0 to trigger a full export.
        /// </param>
        /// <returns>The current transaction number and the set of transaction events to apply or null if an export is required.</returns>
        (int TransactionNumber, IReadOnlyList<JsonEventCollector.TransactionEvent>? Events) GetTransactionEvents( int transactionNumber );

        /// <summary>
        /// Raised whenever a transaction has been successfully committed.
        /// Note that the first transaction is visible: see <see cref="JsonEventCollector.TransactionEvent.TransactionNumber"/>.
        /// </summary>
        PerfectEvent<JsonEventCollector.TransactionEvent> DomainChanged { get; }

        /// <summary>
        /// Loads this domain (if it is not yet loaded) and returns a shell on which <see cref="IObservableDomainShellBase.DisposeAsync(IActivityMonitor)"/>
        /// must be called once the domain is not needed anymore.
        /// If <see cref="IsDestroyed"/> is true or if the containing <see cref="ObservableLeague"/> is closing, or an error occurred,
        /// then this returns null.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="startTimer">
        /// Ensures that the <see cref="ObservableDomain.TimeManager"/> is running or stopped.
        /// When null, it keeps its previous state (it is initially stopped at domain creation) and then its current state is persisted.
        /// </param>
        /// <returns>The shell to use. Null on error or if the domain is destroyed or the league is closing.</returns>
        Task<IObservableDomainShell?> LoadAsync( IActivityMonitor monitor, bool? startTimer = null );

        /// <summary>
        /// Loads this domain (if it is not yet loaded) as a strongly typed one and returns a
        /// shell on which <see cref="IObservableDomainShellBase.DisposeAsync(IActivityMonitor)"/> must be called once the domain is not needed anymore.
        /// If the actual type is not compatible with this or <see cref="IsDestroyed"/> is true or if the containing <see cref="ObservableLeague"/>
        /// is closing, or an error occurred, then this returns null.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="startTimer">
        /// Ensures that the <see cref="ObservableDomain.TimeManager"/> is running or stopped.
        /// When null, it keeps its previous state (it is initially stopped at domain creation) and then its current state is persisted.
        /// </param>
        /// <returns>The shell to use. Null on error or if the domain is destroyed or the league is closing.</returns>
        Task<IObservableDomainShell<T>?> LoadAsync<T>( IActivityMonitor monitor, bool? startTimer = null ) where T : ObservableRootObject;

        /// <summary>
        /// Loads this domain (if it is not yet loaded) as a strongly typed one and returns a
        /// shell on which <see cref="IObservableDomainShellBase.DisposeAsync(IActivityMonitor)"/> must be called once the domain is not needed anymore.
        /// If the actual type is not compatible with this or <see cref="IsDestroyed"/> is true or if the containing <see cref="ObservableLeague"/>
        /// is closing, or an error occurred, then this returns null.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="startTimer">
        /// Ensures that the <see cref="ObservableDomain.TimeManager"/> is running or stopped.
        /// When null, it keeps its previous state (it is initially stopped at domain creation) and then its current state is persisted.
        /// </param>
        /// <returns>The shell to use. Null on error or if the domain is destroyed or the league is closing.</returns>
        Task<IObservableDomainShell<T1, T2>?> LoadAsync<T1, T2>( IActivityMonitor monitor, bool? startTimer = null )
            where T1 : ObservableRootObject
            where T2 : ObservableRootObject;

        /// <summary>
        /// Loads this domain (if it is not yet loaded) as a strongly typed one and returns a
        /// shell on which <see cref="IObservableDomainShellBase.DisposeAsync(IActivityMonitor)"/> must be called once the domain is not needed anymore.
        /// If the actual type is not compatible with this or <see cref="IsDestroyed"/> is true or if the containing <see cref="ObservableLeague"/>
        /// is closing, or an error occurred, then this returns null.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="startTimer">
        /// Ensures that the <see cref="ObservableDomain.TimeManager"/> is running or stopped.
        /// When null, it keeps its previous state (it is initially stopped at domain creation) and then its current state is persisted.
        /// </param>
        /// <returns>The shell to use. Null on error or if the domain is destroyed or the league is closing.</returns>
        Task<IObservableDomainShell<T1, T2, T3>?> LoadAsync<T1, T2, T3>( IActivityMonitor monitor, bool? startTimer = null )
            where T1 : ObservableRootObject
            where T2 : ObservableRootObject
            where T3 : ObservableRootObject;

        /// <summary>
        /// Loads this domain (if it is not yet loaded) as a strongly typed one and returns a
        /// shell on which <see cref="IObservableDomainShellBase.DisposeAsync(IActivityMonitor)"/> must be called once the domain is not needed anymore.
        /// If the actual type is not compatible with this or <see cref="IsDestroyed"/> is true or if the containing <see cref="ObservableLeague"/>
        /// is closing, or an error occurred, then this returns null.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="startTimer">
        /// Ensures that the <see cref="ObservableDomain.TimeManager"/> is running or stopped.
        /// When null, it keeps its previous state (it is initially stopped at domain creation) and then its current state is persisted.
        /// </param>
        /// <returns>The shell to use. Null on error or if the domain is destroyed or the league is closing.</returns>
        Task<IObservableDomainShell<T1, T2, T3, T4>?> LoadAsync<T1, T2, T3, T4>( IActivityMonitor monitor, bool? startTimer = null )
            where T1 : ObservableRootObject
            where T2 : ObservableRootObject
            where T3 : ObservableRootObject
            where T4 : ObservableRootObject;
    }
}
