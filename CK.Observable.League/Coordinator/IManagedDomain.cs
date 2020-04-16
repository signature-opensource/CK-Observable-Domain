using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable.League
{
    /// <summary>
    /// Internal interface: this is what a <see cref="Domain"/> (in the <see cref="Coordinator"/> domain) sees.
    /// </summary>
    interface IManagedDomain
    {
        /// <summary>
        /// Gets whether the domain can be loaded, at least because the domain type can be resolved.
        /// </summary>
        bool IsLoadable { get; }

        /// <summary>
        /// Gets whether the domain is currently loaded.
        /// See <see cref="ManagedDomainOptions.LoadOption"/>.
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
        /// Applies the <see cref="Domain.Options"/>. This is called
        /// after the domain has been created and on each change of its Options form the <see cref="Coordinator"/> domain
        /// or at the end of each transaction from the domain itself.
        /// Parameters are captured immutables: <see cref="ObservableLeague.DomainClient.OnTransactionCommit(in SuccessfulTransactionContext)"/>
        /// can safely defer the execution via <see cref="SuccessfulTransactionContext.PostActions"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="options">The updated options if not null.</param>
        /// <param name="hasActiveTimedEvents">Whether the <see cref="ITimeManager.ActiveTimedEventsCount"/> is positive.</param>
        /// <returns>The awaitable.</returns>
        Task SynchronizeOptionsAsync( IActivityMonitor monitor, ManagedDomainOptions? options, bool? hasActiveTimedEvents );

        /// <summary>
        /// Destroys the managed domain: the <see cref="Domain"/> has been disposed.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="league">The containing league.</param>
        void Destroy( IActivityMonitor monitor, IManagedLeague league );

    }
}
