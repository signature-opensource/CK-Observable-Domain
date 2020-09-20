using CK.Core;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CK.Observable
{
    /// <summary>
    /// Primary interface to implement actual behavior behind an observable domain.
    /// This is is intented to be implemented as a a chain of responsibility: Start,
    /// Commit and Failure should be synchronously propagated through a linked list
    /// (or tree structure) of such clients.
    /// See <see cref="TransactionEventCollectorClient"/> or <see cref="MemoryTransactionProviderClient"/>
    /// for concrete implementations of transaction manager.
    /// </summary>
    public interface IObservableDomainClient
    {
        /// <summary>
        /// Called when the domain instance is created. It may be brand new (<see cref="IObservableDomain.TransactionSerialNumber"/> is 0)
        /// or has been loaded from a stream (<see cref="IObservableDomain.TransactionSerialNumber"/> is greater than 0).
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="d">The newly created or loaded domain.</param>
        void OnDomainCreated( IActivityMonitor monitor, ObservableDomain d );

        /// <summary>
        /// Called before a transaction starts.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="d">The associated domain.</param>
        /// <param name="timeUtc">The date time utc of the transaction start.</param>
        void OnTransactionStart( IActivityMonitor monitor, ObservableDomain d, DateTime timeUtc );

        /// <summary>
        /// Called when a transaction ends successfully. The domain's write lock is held while this is called.
        /// Any exception raised by this method will set <see cref="TransactionResult.IsCriticalError"/> to true.
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

        /// <summary>
        /// Called at the start of the <see cref="ObservableDomain.Dispose(IActivityMonitor)"/> while the write lock is held.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="d">The domain being disposed.</param>
        void OnDomainDisposed( IActivityMonitor monitor, ObservableDomain d );
    }
}
