using CK.Core;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CK.Observable
{
    /// <summary>
    /// Primary interface to implement actual behavior behind an observable domain.
    /// This is is intented to be implemented as a a chain of responsibility: Start,
    /// Commit and Failure should be propagated through a linked list (or tree structure)
    /// of such managers.
    /// See <see cref="TransactionEventCollectorClient"/> or <see cref="MemoryTransactionProviderClient"/>
    /// for concrete implementations of transaction manager.
    /// </summary>
    public interface IObservableDomainClient
    {
        /// <summary>
        /// Called when the domain instance is created.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="d">The newly created domain.</param>
        /// <param name="timeUtc">The date time utc of the creation.</param>
        void OnDomainCreated( IActivityMonitor monitor, ObservableDomain d, DateTime timeUtc );

        /// <summary>
        /// Called before a transaction starts.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="d">The associated domain.</param>
        /// <param name="timeUtc">The date time utc of the transaction start.</param>
        void OnTransactionStart( IActivityMonitor monitor, ObservableDomain d, DateTime timeUtc );

        /// <summary>
        /// Called when a transaction ends successfully.
        /// </summary>
        /// <param name="context">The successful context.</param>
        void OnTransactionCommit( in SuccessfulTransactionContext context );

        /// <summary>
        /// Called when an error occurred in a transaction.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="d">The associated domain.</param>
        /// <param name="errors">
        /// A necessarily non null list of errors with at least one error.
        /// </param>
        void OnTransactionFailure( IActivityMonitor monitor, ObservableDomain d, IReadOnlyList<CKExceptionData> errors );
    }
}
