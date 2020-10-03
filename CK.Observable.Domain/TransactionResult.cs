using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace CK.Observable
{
    /// <summary>
    /// Encapsulates the result of a <see cref="ObservableDomain.Transaction.Commit"/>
    /// and <see cref="ObservableDomain.Modify(IActivityMonitor, Action, int)"/>.
    /// </summary>
    public class TransactionResult
    {
        internal ActionRegistrar<PostActionContext> _postActions;

        /// <summary>
        /// The empty transaction result is used when absolutely nothing happened. It has no events and no commands,
        /// the <see cref="StartTimeUtc"/> and <see cref="NextDueTimeUtc"/> are <see cref="Util.UtcMinValue"/>.
        /// </summary>
        public static readonly TransactionResult Empty = new TransactionResult( Array.Empty<CKExceptionData>(), Util.UtcMinValue, Util.UtcMinValue );

        /// <summary>
        /// Gets whether <see cref="Errors"/> is empty, <see cref="ClientError"/> is null and <see cref="CommandHandlingErrors"/> is empty.
        /// </summary>
        public bool Success => Errors.Count == 0 && ClientError == null && CommandHandlingErrors.Count == 0;

        /// <summary>
        /// Gets whether the <see cref="ClientError"/> is critical: it is the call to <see cref="IObservableDomainClient.OnTransactionCommit(in SuccessfulTransactionEventArgs)"/>
        /// that failed.
        /// This lets the system in an instable, dangerous, state since the transaction has terminated without errors and some external
        /// impacts may have been executed before the error occurred so that rolling back the transaction may not be a brilliant idea.
        /// </summary>
        public bool IsCriticalError => Errors.Count == 0 && ClientError != null;

        /// <summary>
        /// Checks that <see cref="Success"/> is true otherwise throws an exception.
        /// </summary>
        public void ThrowOnTransactionFailure()
        {
            if( !Success )
            {
                if( ClientError != null )
                {
                    if( Errors.Count > 0 )
                    {
                        throw new Exception( $"There has been {Errors.Count} error(s) during the transaction and one of the domain client failed during the OnTransactionFailure call. See logs for details." );
                    }
                    throw new Exception( $"An exception has been thrown by Domain a client during the OnTransactionCommit call. See logs for details.", CKException.CreateFrom( ClientError ) );
                }
                if( Errors.Count > 0 )
                {
                    if( Errors.Count == 1 )
                    {
                        throw new Exception( $"There has been {Errors.Count} error(s) during the transaction. See logs for details.", CKException.CreateFrom( Errors[0] ) );
                    }
                    throw new Exception( $"There has been {Errors.Count} error(s) during the transaction. See logs for details." );
                }
                if( CommandHandlingErrors.Count > 0 )
                {
                    throw new Exception( $"There has been {CommandHandlingErrors.Count} error(s) raised by command handling. See logs for details." );
                }
            }
        }

        /// <summary>
        /// Gets the start time (UTC) of the transaction.
        /// This is <see cref="Util.UtcMinValue"/> if and only if this result is the <see cref="Empty"/> object.
        /// </summary>
        public DateTime StartTimeUtc { get; }

        /// <summary>
        /// Gets the time (UTC) of the transaction commit.
        /// </summary>
        public DateTime CommitTimeUtc { get; }

        /// <summary>
        /// Gets the next due time (UTC) of the <see cref="ObservableTimedEventBase"/>.
        /// This is available even if this result is on error.
        /// </summary>
        public DateTime NextDueTimeUtc { get; }

        /// <summary>
        /// Gets the commands that the transaction generated (all the commands
        /// sent via <see cref="DomainView.SendCommand"/> or <see cref="SuccessfulTransactionEventArgs.SendCommand"/>.
        /// Can be empty (and always empty if there are <see cref="Errors"/>).
        /// </summary>
        public IReadOnlyList<object> Commands { get; }

        /// <summary>
        /// Gets the errors that actually aborted the transaction.
        /// This is empty on success but this doesn't mean that everything went well: a <see cref="ClientError"/> may have occurred
        /// (and that is critical), or <see cref="SuccessfulTransactionErrors"/> or <see cref="CommandHandlingErrors"/> may have been
        /// thrown by sidekicks (this is less critical since the domain's transaction itself is fine).
        /// <para>
        /// Note that any errors raised by <see cref="ExecutePostActionsAsync(IActivityMonitor, bool)"/> are outside of the scope of this <see cref="TransactionResult"/>.
        /// </para>
        /// </summary>
        public IReadOnlyList<CKExceptionData> Errors { get; }

        /// <summary>
        /// Gets the error that occured during the call to <see cref="IObservableDomainClient.OnTransactionCommit"/> (when <see cref="Errors"/>
        /// is empty) or <see cref="IObservableDomainClient.OnTransactionFailure"/> (when <see cref="Errors"/> is NOT empty).
        /// </summary>
        public CKExceptionData ClientError { get; private set; }

        /// <summary>
        /// Gets the errors that occured during the handling of <see cref="ObservableDomain.OnSuccessfulTransaction"/> event
        /// or when calling <see cref="ObservableDomainSidekick.OnSuccessfulTransaction"/>.
        /// </summary>
        public IReadOnlyList<CKExceptionData> SuccessfulTransactionErrors { get; private set; }

        /// <summary>
        /// Gets the errors that occured during the call to <see cref="ObservableDomainSidekick.ExecuteCommand"/>.
        /// Each value tuple contains the faulty command and the exception data.
        /// </summary>
        public IReadOnlyList<(object, CKExceptionData)> CommandHandlingErrors { get; private set; }

        /// <summary>
        /// Gets whether at least one post actions has been enlisted thanks to <see cref="IActionRegistrar{T}"/>
        /// and <see cref="ExecutePostActionsAsync(IActivityMonitor, bool)"/> has not been called yet.
        /// </summary>
        public bool HasPostActions => _postActions != null && _postActions.ActionCount > 0;

        /// <summary>
        /// Attempts to execute all registered post actions if any.
        /// <para>
        /// Note that there may be post actions to execute even if a <see cref="ClientError"/> exists.
        /// </para>
        /// </summary>
        /// <param name="m">The monitor to use.</param>
        /// <param name="throwException">Set it to false to log any exception and return it instead of rethrowing it.</param>
        /// <returns>The exception (if <paramref name="throwException"/> is false).</returns>
        public Task<Exception> ExecutePostActionsAsync( IActivityMonitor m, bool throwException = true )
        {
            var actions = System.Threading.Interlocked.Exchange( ref _postActions, null );
            if( actions != null )
            {
                var ctx = new PostActionContext( m, actions, this );
                return ctx.ExecuteAsync( throwException );
            }
            return Task.FromResult<Exception>( null );
        }

        internal TransactionResult( SuccessfulTransactionEventArgs c )
        {
            StartTimeUtc = c.StartTimeUtc;
            CommitTimeUtc = c.CommitTimeUtc;
            NextDueTimeUtc = c.NextDueTimeUtc;
            Commands = c._commands;
            Errors = Array.Empty<CKExceptionData>();
            _postActions = c._postActions;
            SuccessfulTransactionErrors = Array.Empty<CKExceptionData>();
            CommandHandlingErrors = Array.Empty<(object, CKExceptionData)>();
        }

        internal TransactionResult( IReadOnlyList<CKExceptionData> errors, DateTime startTime, DateTime nextDueTime )
        {
            Debug.Assert( startTime != Util.UtcMinValue || Empty == null, "startTime == Util.UtcMinValue ==> is Empty" );
            StartTimeUtc = startTime;
            CommitTimeUtc = DateTime.UtcNow;
            NextDueTimeUtc = nextDueTime;
            Errors = errors;
            Commands = Array.Empty<object>();
            SuccessfulTransactionErrors = Array.Empty<CKExceptionData>();
            CommandHandlingErrors = Array.Empty<(object, CKExceptionData)>();
        }

        internal TransactionResult SetClientError( Exception ex )
        {
            ClientError = CKExceptionData.CreateFrom( ex );
            return this;
        }

        internal TransactionResult SetSuccessfulTransactionErrors( IReadOnlyList<CKExceptionData> errors )
        {
            SuccessfulTransactionErrors = errors;
            return this;
        }

        internal TransactionResult SetCommandHandlingErrors( IReadOnlyList<(object, CKExceptionData)> errors )
        {
            CommandHandlingErrors = errors;
            return this;
        }

    }
}
