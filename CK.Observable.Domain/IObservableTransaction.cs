using CK.Core;
using System;
using System.Collections.Generic;

namespace CK.Observable
{
    /// <summary>
    /// Obtaining this interface is required to modify a <see cref="ObservableDomain"/>.
    /// </summary>
    public interface IObservableTransaction : IDisposable
    {
        /// <summary>
        /// Gets the transaction start time.
        /// </summary>
        DateTime StartTime { get; }

        /// <summary>
        /// Gets the monitor associated to the transaction.
        /// </summary>
        IActivityMonitor Monitor { get; }

        /// <summary>
        /// Commits all changes and retrieves the events on success.
        /// If errors occurred, the <see cref="TransactionResult"/> contains
        /// the errors but no events nor commands.
        /// <para>
        /// <para>
        /// This method executes the commands by calling all the registered sidekicks (see <see cref="ObservableDomainSidekick.ExecuteCommand"/>).
        /// </para>
        /// </para>
        /// This method NEVER throws: it calls <see cref="IObservableDomainClient.OnTransactionFailure"/>
        /// or <see cref="IObservableDomainClient.OnTransactionCommit"/>, and may set
        /// <see cref="TransactionResult.ClientError"/> if an Exception is thrown
        /// when calling them. Then, on success, it sets the <see cref="TransactionResult.CommandErrors"/> list if errors
        /// occurred during command processing.
        /// </summary>
        /// <returns>The transaction result.</returns>
        TransactionResult Commit();

        /// <summary>
        /// Gets any errors that have been added by <see cref="AddError"/>.
        /// </summary>
        IReadOnlyList<CKExceptionData> Errors { get; }

        /// <summary>
        /// Adds an error to this transaction.
        /// This prevents this transaction to be successfully committed; calling <see cref="Commit"/>
        /// will be the same as disposing this transaction without committing: a <see cref="TransactionResult"/>
        /// with only <see cref="Errors"/> will be obtained.
        /// </summary>
        /// <param name="d">An exception data.</param>
        void AddError( CKExceptionData d );
    }
}
