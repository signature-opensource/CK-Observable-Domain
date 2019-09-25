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
        List<Func<IActivityMonitor, Task>> _rawPostActions;

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
        public IReadOnlyList<Func<IActivityMonitor, Task>> PostActions => (IReadOnlyList<Func<IActivityMonitor, Task>>)_rawPostActions
                                                                            ?? Array.Empty<Func<IActivityMonitor, Task>>();

        /// <summary>
        /// Attempts to executes all registered <see cref="PostActions"/> if any.
        /// On error, nothing is done (except logging the error) and the culprit is let as the first next action to execute.
        /// </summary>
        /// <param name="m">The monitor to use.</param>
        /// <returns>The awaitable.</returns>
        public async Task ExecutePostActionsAsync( IActivityMonitor m )
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
                throw;
            }
        }

        internal TransactionResult( SuccessfulTransactionContext c )
        {
            TimeUtc = c.TimeUtc;
            Events = c.Events;
            Commands = c.Commands;
            Errors = Array.Empty<CKExceptionData>();
            _rawPostActions = c.RawPostActions;
        }

        internal TransactionResult( IReadOnlyList<CKExceptionData> errors )
        {
            TimeUtc = DateTime.UtcNow;
            Errors = errors;
            Events = Array.Empty<ObservableEvent>();
            Commands = Array.Empty<ObservableCommand>();
        }

        TransactionResult( in TransactionResult r, CKExceptionData data )
        {
            Debug.Assert( r.ClientError == null, "ClientError occur at most once." );
            TimeUtc = r.TimeUtc;
            Events = r.Events;
            Commands = r.Commands;
            Errors = r.Errors;
            _rawPostActions = r._rawPostActions;
            ClientError = data;
        }

        internal TransactionResult WithClientError( Exception ex ) => new TransactionResult( this, CKExceptionData.CreateFrom( ex ) );


    }
}
