using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable.League
{
    /// <summary>
    /// Internal interface: this is what a managed <see cref="Domain"/> (in the <see cref="Coordinator"/> domain) sees.
    /// </summary>
    interface IManagedDomain
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
        /// Applies the <see cref="Domain.Options"/>. This is called
        /// after the domain has been created and on each change of its Options from the <see cref="Coordinator"/> domain
        /// or at the end of each transaction from the domain itself.
        /// Parameters are captured immutables: <see cref="ObservableLeague.DomainClient.OnTransactionCommit(in SuccessfulTransactionEventArgs)"/>
        /// can safely defer the execution via <see cref="SuccessfulTransactionEventArgs.PostActions"/>.
        /// </summary>
        /// <remarks>
        /// The only case where <paramref name="options"/> and <paramref name="nextActiveTime"/> are both non null
        /// is during the initial call by <see cref="ObservableLeague.LoadAsync"/>.
        /// </remarks>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="options">
        /// The updated options if called by the <see cref="Coordinator"/> domain or
        /// by the initial <see cref="ObservableLeague.LoadAsync"/>, null otherwise.
        /// </param>
        /// <param name="nextActiveTime">
        /// The <see cref="ITimeManager.NextActiveTime"/> when called after each successful commit by the domain itself
        /// or by the initial <see cref="ObservableLeague.LoadAsync"/>, null otherwise.
        /// </param>
        /// <returns>The awaitable.</returns>
        Task SynchronizeOptionsAsync( IActivityMonitor monitor, ManagedDomainOptions? options, DateTime? nextActiveTime );

        /// <summary>
        /// Destroys the managed domain: the managed <see cref="Domain"/> has been destroyed.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="league">The containing league.</param>
        void Destroy( IActivityMonitor monitor, IManagedLeague league );

    }
}
