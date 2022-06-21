using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable.League
{
    /// <summary>
    /// Internal interface: this is what a managed <see cref="ODomain"/> (in the <see cref="OCoordinatorRoot"/> domain) sees.
    /// </summary>
    interface IInternalManagedDomain
    {
        /// <summary>
        /// Gets whether the domain can be loaded, at least because the domain type can be resolved.
        /// </summary>
        bool IsLoadable { get; }

        /// <summary>
        /// Gets whether the domain is currently loaded.
        /// See <see cref="ManagedDomainOptions.LifeCycleOption"/>.
        /// </summary>
        bool IsLoaded { get; }

        /// <summary>
        /// Gets the domain name.
        /// </summary>
        string DomainName { get; }

        /// <summary>
        /// Gets the domain options.
        /// </summary>
        ManagedDomainOptions Options { get; }

        /// <summary>
        /// Synchronizes the ODomain and the actual domain.
        /// This is called:
        ///  - Right after the load of the league to initializes Client and decide whether the domain must be initially loaded or not.
        ///  - By each Coordinators' domain commit if the corresponding ODomain.Options changed.
        ///  - By domain's commit to update the ODomain.NextActiveTime. 
        /// Parameters are captured immutables: <see cref="ObservableLeague.DomainClient.OnTransactionCommit(in TransactionDoneEventArgs)"/>
        /// can safely defer the execution via <see cref="TransactionDoneEventArgs.PostActions"/>.
        /// </summary>
        /// <remarks>
        /// The only case where <paramref name="options"/> and <paramref name="nextActiveTime"/> are both non null
        /// is during the initial call by <see cref="ObservableLeague.LoadAsync"/>.
        /// </remarks>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="options">
        /// The updated options if called by the <see cref="OCoordinatorRoot"/> domain or
        /// by the initial <see cref="ObservableLeague.LoadAsync"/>, null otherwise.
        /// </param>
        /// <param name="nextActiveTime">
        /// The <see cref="ITimeManager.NextActiveTime"/> when called after each successful commit by the domain itself
        /// or by the initial <see cref="ObservableLeague.LoadAsync"/>, null otherwise.
        /// </param>
        /// <returns>The awaitable.</returns>
        Task SynchronizeOptionsAsync( IActivityMonitor monitor, ManagedDomainOptions? options, DateTime? nextActiveTime );

        /// <summary>
        /// Destroys the managed domain: the managed <see cref="ODomain"/> has been destroyed.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="league">The containing league.</param>
        void Destroy( IActivityMonitor monitor, IManagedLeague league );

    }
}
