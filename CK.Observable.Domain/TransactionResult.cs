using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Observable
{
    /// <summary>
    /// Encapsulates the result of a <see cref="ObservableDomain.Transaction.Commit"/>
    /// and <see cref="ObservableDomain.Modify(IActivityMonitor, Action, int)"/>.
    /// </summary>
    public class TransactionResult
    {
        // These are used by SideKickManager.
        internal ActionRegistrar<PostActionContext>? _localPostActions;
        internal ActionRegistrar<PostActionContext>? _domainPostActions;

        Task? _domainLockPrevious;
        TaskCompletionSource<bool>? _domainLockCurrent;

        /// <summary>
        /// The empty transaction result is used when absolutely nothing happened. It has no events and no commands,
        /// the <see cref="StartTimeUtc"/> and <see cref="NextDueTimeUtc"/> are <see cref="Util.UtcMinValue"/>.
        /// </summary>
        public static readonly TransactionResult Empty = new TransactionResult( Array.Empty<CKExceptionData>(), Util.UtcMinValue );

        /// <summary>
        /// Gets whether <see cref="Errors"/> is empty, <see cref="ClientError"/> is null and both <see cref="SuccessfulTransactionErrors"/>
        /// and <see cref="CommandHandlingErrors"/> are empty.
        /// </summary>
        public bool Success => Errors.Count == 0 && ClientError == null && SuccessfulTransactionErrors.Count == 0 && CommandHandlingErrors.Count == 0;

        /// <summary>
        /// Gets whether the <see cref="ClientError"/> is critical: it is the call to <see cref="IObservableDomainClient.OnTransactionCommit(in SuccessfulTransactionEventArgs)"/>
        /// that failed.
        /// <para>
        /// This lets the system in an instable, dangerous, state since the transaction has terminated without errors and some external
        /// actions may have been executed before the error occurred so that rolling back the transaction may not be a brilliant idea.
        /// </para>
        /// <para>
        /// Note that if there are transaction <see cref="Errors"/>, then <see cref="IObservableDomainClient.OnTransactionFailure(IActivityMonitor, ObservableDomain, IReadOnlyList{CKExceptionData})"/>
        /// has been called, and even it failed, this is not considered critical.
        /// </para>
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
                    throw new Exception( $"There has been {Errors.Count} error(s) during the transaction. See logs for details." );
                }
                if( SuccessfulTransactionErrors.Count > 0 )
                {
                    throw new Exception( $"There has been {SuccessfulTransactionErrors.Count} error(s) during the transaction OnSuccessful event. See logs for details." );
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
        /// Gets the commands that the transaction generated (all the commands
        /// sent via <see cref="DomainView.SendCommand(in ObservableDomainCommand)"/> or <see cref="SuccessfulTransactionEventArgs.SendCommand(in ObservableDomainCommand)"/>.
        /// Can be empty (and always empty if there are <see cref="Errors"/>).
        /// <para>
        /// These commands have been submitted to the <see cref="ObservableDomainSidekick.ExecuteCommand(IActivityMonitor, in SidekickCommand)"/>
        /// and may have generated one or more <see cref="CommandHandlingErrors"/>.
        /// </para>
        /// </summary>
        public IReadOnlyList<ObservableDomainCommand> Commands { get; }

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
        /// is empty) or <see cref="IObservableDomainClient.OnTransactionFailure"/> (when <see cref="Errors"/> is not empty).
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
        /// Gets whether at least one post actions has been enlisted thanks to the <see cref="SuccessfulTransactionEventArgs.LocalPostActions"/>
        /// <see cref="IActionRegistrar{T}"/> and <see cref="ExecutePostActionsAsync(IActivityMonitor, bool)"/> has not been called yet.
        /// </summary>
        public bool HasLocalPostActions => (_localPostActions?.ActionCount ?? 0) > 0;

        /// <summary>
        /// Gets whether at least one post actions has been enlisted thanks to the <see cref="SuccessfulTransactionEventArgs.DomainPostActions"/>
        /// <see cref="IActionRegistrar{T}"/> and <see cref="ExecutePostActionsAsync(IActivityMonitor, bool)"/> has not been called yet.
        /// </summary>
        public bool HasDomainPostActions => (_domainPostActions?.ActionCount ?? 0) > 0;

        /// <summary>
        /// Overridden to return mainly error related information.
        /// </summary>
        /// <returns>The success or error detail.</returns>
        public override string ToString()
        {
            if( Success ) return $"Success ({(_localPostActions?.ActionCount ?? 0) + (_domainPostActions?.ActionCount ?? 0)} post actions waiting).";
            return $"{Errors.Count} transaction errors, {(IsCriticalError ? "with a" : "no" )} Critical Error, {SuccessfulTransactionErrors.Count} successful transaction errors, {CommandHandlingErrors.Count} command errors handling.";
        }

        /// <summary>
        /// Attempts to execute all registered post actions if any. First the local post actions
        /// and then the one's bound to the domain.
        /// <para>
        /// Note that there may be post actions to execute even if a <see cref="ClientError"/> exists.
        /// </para>
        /// </summary>
        /// <param name="m">The monitor to use.</param>
        /// <param name="throwException">Set it to false to log any exception and return it instead of rethrowing it.</param>
        /// <returns>The exception (if <paramref name="throwException"/> is false) or null if no error occurred.</returns>
        public async Task<Exception?> ExecutePostActionsAsync( IActivityMonitor m, bool throwException = true )
        {
            if( !Success )
            {
                // We cannot release our lock here without waiting for the previous one.
                if( _domainLockPrevious != null ) await _domainLockPrevious;
                _domainLockCurrent?.TrySetResult( true );
                return null;
            }
            Exception? result = null;
            var l = _localPostActions;
            _localPostActions = null;
            if( l != null && l.ActionCount > 0 )
            {
                result = await DoExecute( m, throwException, l, "Local" );
            }
            var d = _domainPostActions;
            if( d != null && d.ActionCount > 0 )
            {
                if( _domainLockPrevious != null ) await _domainLockPrevious;
                if( result != null )
                {
                    m.Warn( $"Skipping execution of {d.ActionCount} domain post actions since executing local post actions raised an error." );
                }
                else
                {
                    _domainPostActions = null;
                    try
                    {
                        result = await DoExecute( m, throwException, d, "Domain" );
                    }
                    finally
                    {
                        _domainLockCurrent?.TrySetResult( true );
                    }
                }
            }
            return result;
        }

        async Task<Exception?> DoExecute( IActivityMonitor m, bool throwException, ActionRegistrar<PostActionContext> actions, string name )
        {
            Debug.Assert( actions != null && actions.ActionCount > 0 );
            var ctx = new PostActionContext( m, actions, this );
            try
            {
                return await ctx.ExecuteAsync( throwException, name: name );
            }
            finally
            {
                await ctx.DisposeAsync();
            }
        }

        internal TransactionResult( SuccessfulTransactionEventArgs c )
        {
            StartTimeUtc = c.StartTimeUtc;
            CommitTimeUtc = c.CommitTimeUtc;
            Commands = c._commands;
            Errors = Array.Empty<CKExceptionData>();
            _domainPostActions = c._domainPostActions;
            _localPostActions = c._localPostActions;
            SuccessfulTransactionErrors = Array.Empty<CKExceptionData>();
            CommandHandlingErrors = Array.Empty<(object, CKExceptionData)>();
        }

        internal TransactionResult( IReadOnlyList<CKExceptionData> errors, DateTime startTime )
        {
            Debug.Assert( startTime != Util.UtcMinValue || Empty == null, "startTime == Util.UtcMinValue ==> is Empty" );
            StartTimeUtc = startTime;
            CommitTimeUtc = DateTime.UtcNow;
            Errors = errors;
            Commands = Array.Empty<ObservableDomainCommand>();
            SuccessfulTransactionErrors = Array.Empty<CKExceptionData>();
            CommandHandlingErrors = Array.Empty<(object, CKExceptionData)>();
        }

        internal void SetClientError( Exception ex )
        {
            ClientError = CKExceptionData.CreateFrom( ex );
        }

        internal void SetSuccessfulTransactionErrors( IReadOnlyList<CKExceptionData> errors )
        {
            SuccessfulTransactionErrors = errors;
        }

        internal void SetCommandHandlingErrors( IReadOnlyList<(object, CKExceptionData)> errors )
        {
            CommandHandlingErrors = errors;
        }

        internal void SetDomainPostActionsLocks( Task? previous, TaskCompletionSource<bool>? current )
        {
            _domainLockPrevious = previous;
            _domainLockCurrent = current;
        }
    }
}
