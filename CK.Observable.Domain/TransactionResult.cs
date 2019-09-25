using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace CK.Observable
{
    /// <summary>
    /// Encapsulates the result of a <see cref="ObservableDomain.Transaction.Commit"/>
    /// and <see cref="ObservableDomain.Modify(Action)"/>.
    /// </summary>
    public class TransactionResult
    {
        /// <summary>
        /// The empty transaction result with no events and no commands: both lists are empty.
        /// </summary>
        public static readonly TransactionResult Empty = new TransactionResult( Array.Empty<CKExceptionData>() );

        /// <summary>
        /// Gets the time (UTC) of the transaction commit.
        /// </summary>
        public DateTime TimeUtc { get; }

        /// <summary>
        /// Gets the events that the transaction generated (all <see cref="ObservableObject"/> changes).
        /// Can be empty (and always empty if there are <see cref="Errors"/>).
        /// </summary>
        public IReadOnlyList<ObservableEvent> Events { get; }

        /// <summary>
        /// Gets the commands that the transaction generated (all the commands
        /// sent via <see cref="ObservableObject.SendCommand"/>.
        /// Can be empty (and always empty if there are <see cref="Errors"/>).
        /// </summary>
        public IReadOnlyList<ObservableCommand> Commands { get; }

        /// <summary>
        /// Gets the errors that actually aborted the transaction.
        /// This is empty on success.
        /// </summary>
        public IReadOnlyList<CKExceptionData> Errors { get; }

        /// <summary>
        /// Gets the error that occured during the call to <see cref="IObservableDomainClient.OnTransactionCommit"/> (when <see cref="Errors"/>
        /// is empty) or <see cref="IObservableDomainClient.OnTransactionFailure"/> (when <see cref="Errors"/> is NOT empty).
        /// </summary>
        public CKExceptionData ClientError { get; }

        /// <summary>
        /// Exposes all post actions that must be executed.
        /// </summary>
        public IReadOnlyList<Func<IActivityMonitor, Task>> PostActions { get; }

        internal TransactionResult( SuccessfulTransactionContext c )
        {
            TimeUtc = c.TimeUtc;
            Events = c.Events;
            Commands = c.Commands;
            Errors = Array.Empty<CKExceptionData>();
            PostActions = c.PostActions;
        }

        internal TransactionResult( IReadOnlyList<CKExceptionData> errors )
        {
            TimeUtc = DateTime.UtcNow;
            Errors = errors;
            Events = Array.Empty<ObservableEvent>();
            Commands = Array.Empty<ObservableCommand>();
            PostActions = Array.Empty<Func<IActivityMonitor, Task>>();
        }

        TransactionResult( in TransactionResult r, CKExceptionData data )
        {
            Debug.Assert( r.ClientError == null, "ClientError occur at most once." );
            TimeUtc = r.TimeUtc;
            Events = r.Events;
            Commands = r.Commands;
            Errors = r.Errors;
            PostActions = r.PostActions;
            ClientError = data;
        }

        internal TransactionResult WithClientError( Exception ex ) => new TransactionResult( this, CKExceptionData.CreateFrom( ex ) );


    }
}
