using CK.Core;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CK.Observable;

/// <summary>
/// Primary interface to implement actual behavior behind an observable domain.
/// This is intended to be implemented as a a chain of responsibility: Start,
/// Commit and Failure should be synchronously propagated through a linked list
/// (or tree structure) of such clients.
/// See <see cref="MemoryTransactionProviderClient"/> for concrete implementations of transaction manager.
/// </summary>
public interface IObservableDomainClient
{
    /// <summary>
    /// Called before a transaction starts.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="d">The associated domain.</param>
    /// <param name="timeUtc">The date time utc of the transaction start.</param>
    void OnTransactionStart( IActivityMonitor monitor, ObservableDomain d, DateTime timeUtc );

    /// <summary>
    /// Called when a transaction ends successfully or has been rolled back. The domain's write lock is held while this is called.
    /// Any exception raised by this method will set <see cref="TransactionResult.IsCriticalError"/> to true.
    /// <para>
    /// Implementations may capture any required domain object's state and use
    /// <see cref="TransactionDoneEventArgs.PostActions"/> or <see cref="TransactionDoneEventArgs.DomainPostActions"/>
    /// to post asynchronous actions (or to send commands thanks to <see cref="TransactionDoneEventArgs.SendCommand(in ObservableDomainCommand)"/>
    /// that will be processed by the sidekicks).
    /// </para>
    /// </summary>
    /// <param name="context">The successful context.</param>
    void OnTransactionCommit( TransactionDoneEventArgs context );

    /// <summary>
    /// Called from inside a transaction whenever an unhandled exception is thrown.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="d">The associated domain.</param>
    /// <param name="ex">The exception that has been raised.</param>
    /// <param name="swallowError">
    /// Defaults to false since by default the transaction will fail.
    /// Setting this to true will silently swallow the exception (this is up to this implementation
    /// to log it) and generate a call to <see cref="OnTransactionCommit"/> with a <see cref="TransactionDoneEventArgs"/>.
    /// </param>
    void OnUnhandledException( IActivityMonitor monitor, ObservableDomain d, Exception ex, ref bool swallowError );

    /// <summary>
    /// Called when an error occurred in a transaction.
    /// This can implement a roll back mechanism: <see cref="OnTransactionCommit(TransactionDoneEventArgs)"/> will be called.
    /// If no roll back is done, this is the last event that the client will see from the transaction.
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
