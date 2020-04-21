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
        ActionRegisterer<PostActionContext> _postActions;

        /// <summary>
        /// The empty transaction result is used when absolutely nothing happened. It has no events and no commands,
        /// the <see cref="StartTimeUtc"/> and <see cref="NextDueTimeUtc"/> are <see cref="Util.UtcMinValue"/>.
        /// </summary>
        public static readonly TransactionResult Empty = new TransactionResult( Array.Empty<CKExceptionData>(), Util.UtcMinValue, Util.UtcMinValue );

        /// <summary>
        /// Gets whether <see cref="Errors"/> is empty and <see cref="ClientError"/> is null.
        /// </summary>
        public bool Success => Errors.Count == 0 && ClientError == null;

        /// <summary>
        /// Gets whether the <see cref="ClientError"/> is critical: it is the call to <see cref="IObservableDomainClient.OnTransactionCommit"/>
        /// that failed.
        /// This lets the system in an instable, dangerous, state since the transaction has terminated without errors and some external
        /// impacts may have been executed before the error occurred so that rolling back the transaction may not be a brilliant idea.
        /// </summary>
        public bool IsCriticalError => Errors.Count == 0 && ClientError != null;

        /// <summary>
        /// Checks that <see cref="Success"/> is true otherwise throw an exception.
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
        public CKExceptionData ClientError { get; private set; }

        /// <summary>
        /// Gets whether at least one post actions has been enlisted thanks to <see cref="SuccessfulTransactionContext.AddPostAction(Func{PostActionContext, Task})"/>
        /// and <see cref="ExecutePostActionsAsync(IActivityMonitor, bool)"/> has not been called yet.
        /// </summary>
        public bool HasPostActions => _postActions != null;

        /// <summary>
        /// Attempts to execute all registered post actions if any.
        /// <para>
        /// Note that there may be post actions to execute even if a <see cref="ClientError"/> exists.
        /// Whether they must be executed or ignored is a decision that must be handled by the consumer of this result: by
        /// default, <see cref="ObservableDomain.ModifyAsync(IActivityMonitor, Action, int)"/> doesn't call this method if a Client error occurred. 
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

        internal TransactionResult( SuccessfulTransactionContext c )
        {
            StartTimeUtc = c.StartTimeUtc;
            CommitTimeUtc = c.CommitTimeUtc;
            NextDueTimeUtc = c.NextDueTimeUtc;
            Events = c.Events;
            Commands = c.Commands;
            Errors = Array.Empty<CKExceptionData>();
            if( c._postActions.ActionCount > 0 ) _postActions = c._postActions;
        }

        internal TransactionResult( IReadOnlyList<CKExceptionData> errors, DateTime startTime, DateTime nextDueTime )
        {
            Debug.Assert( startTime != Util.UtcMinValue || Empty == null, "startTime == Util.UtcMinValue ==> is Empty" );
            StartTimeUtc = startTime;
            CommitTimeUtc = DateTime.UtcNow;
            NextDueTimeUtc = nextDueTime;
            Errors = errors;
            Events = Array.Empty<ObservableEvent>();
            Commands = Array.Empty<ObservableCommand>();
        }

        internal TransactionResult SetClientError( Exception ex )
        {
            ClientError = CKExceptionData.CreateFrom( ex );
            return this;
        }

    }
}
