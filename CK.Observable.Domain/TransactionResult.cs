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
        List<Func<IActivityMonitor, Task>> _rawPostActions;

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
        public CKExceptionData ClientError { get; }

        /// <summary>
        /// Exposes all post actions that must be executed.
        /// </summary>
        public IReadOnlyList<Func<IActivityMonitor, Task>> PostActions => (IReadOnlyList<Func<IActivityMonitor, Task>>)_rawPostActions
                                                                            ?? Array.Empty<Func<IActivityMonitor, Task>>();

        /// <summary>
        /// Attempts to executes all registered <see cref="PostActions"/> if any.
        /// By default, on error, nothing is done (except logging the error, and by default raising the exception again) and
        /// the culprit is let as the first next action to execute.
        /// </summary>
        /// <param name="m">The monitor to use.</param>
        /// <param name="throwException">Set it to false to log any exception and return it instead of rethrowing it.</param>
        /// <returns>The exception (if <paramref name="throwException"/> is false).</returns>
        public async Task<Exception> ExecutePostActionsAsync( IActivityMonitor m, bool throwException = true )
        {
            if( _rawPostActions != null && _rawPostActions.Count > 0 )
            {
                try
                {
                    while( _rawPostActions.Count > 0 )
                    {
                        await _rawPostActions[0].Invoke( m );
                        _rawPostActions.RemoveAt( 0 );
                    }
                }
                catch( Exception ex )
                {
                    m.Error( ex );
                    if( throwException ) throw;
                    return ex;
                }
            }
            return null;
        }

        internal TransactionResult( SuccessfulTransactionContext c )
        {
            StartTimeUtc = c.StartTimeUtc;
            CommitTimeUtc = c.CommitTimeUtc;
            NextDueTimeUtc = c.NextDueTimeUtc;
            Events = c.Events;
            Commands = c.Commands;
            Errors = Array.Empty<CKExceptionData>();
            _rawPostActions = c.RawPostActions;
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

        TransactionResult( in TransactionResult r, CKExceptionData data )
        {
            Debug.Assert( r.ClientError == null, "ClientError occur at most once." );
            StartTimeUtc = r.StartTimeUtc;
            CommitTimeUtc = r.CommitTimeUtc;
            NextDueTimeUtc = r.NextDueTimeUtc;
            Events = r.Events;
            Commands = r.Commands;
            Errors = r.Errors;
            _rawPostActions = r._rawPostActions;
            ClientError = data;
        }

        internal TransactionResult WithClientError( Exception ex ) => new TransactionResult( this, CKExceptionData.CreateFrom( ex ) );


    }
}
