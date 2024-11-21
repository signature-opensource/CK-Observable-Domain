using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Observable;

/// <summary>
/// Encapsulates the result of a <see cref="ObservableDomain.Transaction.Commit"/>
/// and <see cref="ObservableDomain.Modify(IActivityMonitor, Action, int)"/>.
/// </summary>
public sealed class TransactionResult
{
    static readonly Task<Exception?> _noErrorResult = Task.FromResult((Exception?)null );

    // These are used by SidekickManager.
    internal ActionRegistrar<PostActionContext>? _postActions;
    internal ActionRegistrar<PostActionContext>? _domainPostActions;

    string? _domainName;
    TaskCompletionSource<ActionRegistrar<PostActionContext>?>? _forDomainPostActionsExecutor;
    TaskCompletionSource<Exception?>? _domainPostActionsErrorSource;

    Exception? _postActionsError;
    Task<Exception?> _domainPostActionsError;

    /// <summary>
    /// The empty transaction result is used when absolutely nothing happened. It has no events and no commands,
    /// the <see cref="StartTimeUtc"/> is <see cref="Util.UtcMinValue"/>, <see cref="TransactionNumber"/> is 0
    /// </summary>
    public static readonly TransactionResult EmptySuccess = new TransactionResult( Array.Empty<CKExceptionData>(), Util.UtcMinValue );

    /// <summary>
    /// Gets whether <see cref="Errors"/> is empty, <see cref="ClientError"/> is null, both <see cref="TransactionDoneErrors"/>
    /// and <see cref="CommandHandlingErrors"/> are empty and <see cref="PostActionsError"/> is null and <see cref="DomainPostActionsError"/> must not
    /// yet be resolved or has a null exception.
    /// <para>
    /// The domain post actions execution are asynchronously executed by the <see cref="ObservableDomainPostActionExecutor"/>.
    /// If needed the <see cref="DomainPostActionsError"/> task can be used to await for the completion of the domain post actions 
    /// emitted by this transaction.
    /// </para>
    /// </summary>
    public bool Success => Errors.Count == 0
                           && ClientError == null
                           && TransactionDoneErrors.Count == 0
                           && CommandHandlingErrors.Count == 0
                           && _postActionsError == null
                           && (!_domainPostActionsError.IsCompleted || _domainPostActionsError.Result == null );

    /// <summary>
    /// Implicit cast operator based on <see cref="Success"/>.
    /// </summary>
    /// <param name="r">The transaction result.</param>
    public static implicit operator bool( TransactionResult r ) => r.Success;

    /// <summary>
    /// Gets whether the <see cref="ClientError"/> is critical: it is the call to <see cref="IObservableDomainClient.OnTransactionCommit(TransactionDoneEventArgs)"/>
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
    /// Checks that <see cref="Success"/> is true (and optionally that this is not a successful rollback)
    /// otherwise throws an exception.
    /// </summary>
    /// <param name="considerRolledbackAsFailure">
    /// True to consider that a non null <see cref="RollbackedInfo"/> must be considered a failure: an exception will be thrown.
    /// </param>
    public void ThrowOnFailure( bool considerRolledbackAsFailure )
    {
        if( (considerRolledbackAsFailure && RollbackedInfo != null) || !Success )
        {
            Throw.Exception( GetErrorMessage( considerRolledbackAsFailure ) );
        }
    }

    string? GetErrorMessage( bool considerRolledbackAsFailure )
    {
        if( !Success )
        {
            if( ClientError != null )
            {
                if( Errors.Count > 0 )
                {
                    return $"There has been {Errors.Count} error(s) during the transaction and one of the domain client failed during the OnTransactionFailure call. See logs for details.";
                }
                return $"An exception has been thrown by a domain client during the OnTransactionCommit call. See logs for details.";
            }
            if( Errors.Count > 0 )
            {
                return $"There has been {Errors.Count} error(s) during the transaction. See logs for details.";
            }
            if( TransactionDoneErrors.Count > 0 )
            {
                return $"There has been {TransactionDoneErrors.Count} error(s) during the transaction OnSuccessful event. See logs for details.";
            }
            if( CommandHandlingErrors.Count > 0 )
            {
                return $"There has been {CommandHandlingErrors.Count} error(s) raised by sidekicks handling the commands. See logs for details.";
            }
            if( _postActionsError != null )
            {
                return $"A post action failed. See logs for details: {_postActionsError.Message}";
            }
            Debug.Assert( _domainPostActionsError.IsCompleted && _domainPostActionsError.Result != null, "Success property has tested this." );
            return $"A domain post action eventually failed. See logs for details: : {_domainPostActionsError.Result.Message}";
        }
        if( considerRolledbackAsFailure && RollbackedInfo != null )
        {
            Debug.Assert( !RollbackedInfo.Failure.Success );
            return $"A failed transaction rolled back: {RollbackedInfo.Failure.GetErrorMessage(false)}";
        }
        return null;
    }

    /// <summary>
    /// Gets the start time (UTC) of the transaction.
    /// This is <see cref="Util.UtcMinValue"/> if and only if this result is the <see cref="Empty"/> object.
    /// </summary>
    public DateTime StartTimeUtc { get; }

    /// <summary>
    /// Gets the time (UTC) of the transaction commit.
    /// This is the time of the commit, even if the transaction is on error.
    /// </summary>
    public DateTime CommitTimeUtc { get; }

    /// <summary>
    /// Gets the transaction number.
    /// This is 0 if the transaction is on error.
    /// </summary>
    public int TransactionNumber { get; }

    /// <summary>
    /// Gets the information of the rolled back if this successful transaction has recovered
    /// a transaction failure.
    /// <para>
    /// It happens when a transaction fails and has been reloaded by one of the <see cref="ObservableDomain.DomainClient"/>
    /// and one or more sidekicks are waiting to be instantiated.
    /// This successful transaction result captures the impacts of the rollback that may be triggered by the sidekicks instantiation.
    /// </para>
    /// </summary>
    public RolledbackTransactionInfo? RollbackedInfo { get; }

    /// <summary>
    /// Gets the commands that the transaction generated (all the commands
    /// sent via <see cref="DomainView.SendCommand(in ObservableDomainCommand)"/> or <see cref="TransactionDoneEventArgs.SendCommand(in ObservableDomainCommand)"/>.
    /// Can be empty (and always empty if there are <see cref="Errors"/>).
    /// <para>
    /// These commands have been submitted to the <see cref="ObservableDomainSidekick.ExecuteCommand(IActivityMonitor, in SidekickCommand)"/>
    /// and may have generated one or more <see cref="CommandHandlingErrors"/>.
    /// </para>
    /// </summary>
    public IReadOnlyList<ObservableDomainCommand> Commands { get; }

    /// <summary>
    /// Gets the initial error if the transaction failed to start because a
    /// <see cref="IObservableDomainClient.OnTransactionStart(IActivityMonitor, ObservableDomain, DateTime)"/> failed.
    /// <para>
    /// This error also appears in <see cref="Errors"/>.
    /// </para>
    /// </summary>
    public CKExceptionData? OnClientStartError { get; }

    /// <summary>
    /// Gets whether the transaction failed because the lock couldn't be obtained in
    /// less than the allowed timeout.
    /// </summary>
    public bool IsStartTimeoutError { get; }

    /// <summary>
    /// Gets the errors that aborted the transaction (or the <see cref="OnClientStartError"/> if the transaction couldn't start).
    /// This is empty on success but this doesn't mean that everything went well: a <see cref="ClientError"/> may have occurred
    /// (and that is critical), or <see cref="TransactionDoneErrors"/> or <see cref="CommandHandlingErrors"/> may have been
    /// thrown by sidekicks (this is less critical since the domain's transaction itself is fine).
    /// </summary>
    public IReadOnlyList<CKExceptionData> Errors { get; }

    /// <summary>
    /// Gets the error that occurred during the call to <see cref="IObservableDomainClient.OnTransactionCommit"/> (when <see cref="Errors"/>
    /// is empty) or <see cref="IObservableDomainClient.OnTransactionFailure"/> (when <see cref="Errors"/> is not empty).
    /// </summary>
    public CKExceptionData? ClientError { get; private set; }

    /// <summary>
    /// Gets the errors that occurred during the handling of <see cref="ObservableDomain.TransactionDone"/> event
    /// or when calling <see cref="ObservableDomainSidekick.OnTransactionResult"/>.
    /// </summary>
    public IReadOnlyList<CKExceptionData> TransactionDoneErrors { get; private set; }

    /// <summary>
    /// Gets the errors that occurred during the call to <see cref="ObservableDomainSidekick.ExecuteCommand"/>.
    /// Each value tuple contains the faulty command and the exception data.
    /// </summary>
    public IReadOnlyList<(object, CKExceptionData)> CommandHandlingErrors { get; private set; }

    /// <summary>
    /// Gets the error of the <see cref="TransactionDoneEventArgs.PostActions"/> execution if any.
    /// </summary>
    public Exception? PostActionsError { get; }

    /// <summary>
    /// Gets the future or resolved error of the <see cref="ObservableDomainPostActionExecutor"/> if any.
    /// Awaiting this enables to wait for the processing of <see cref="TransactionDoneEventArgs.DomainPostActions"/>
    /// emitted by this transaction.
    /// </summary>
    public Task<Exception?> DomainPostActionsError => _domainPostActionsError ?? _noErrorResult;

    /// <summary>
    /// Overridden to return mainly error related information.
    /// </summary>
    /// <returns>The success or error detail.</returns>
    public override string ToString()
    {
        if( Success )
        {
            return $"Success ({(_postActions?.ActionCount ?? 0) + (_domainPostActions?.ActionCount ?? 0)} post actions waiting).";
        }
        return $"{Errors.Count} transaction errors, {(IsCriticalError ? "with a" : "no" )} Critical Error, {TransactionDoneErrors.Count} OnSuccessfulTransaction errors, {CommandHandlingErrors.Count} command errors handling.";
    }

    internal Task ExecutePostActionsAsync( IActivityMonitor m, bool parallelDomainPostActions )
    {
        var l = _postActions;
        if( l == null ) return Task.CompletedTask;
        _postActions = null;

        // We keep the reference to the domain post actions (this may be useful one day).
        var d = _domainPostActions;
        Debug.Assert( d != null );

        // If an error occurred, directly signal the DomainPostActions with null: the Domain executor will skip it.
        if( !Success )
        {
            m.Warn( $"Skipping execution of {l.ActionCount} post actions and {d.ActionCount} Domain post actions because of a previous error." );
            if( _forDomainPostActionsExecutor != null )
            {
                Debug.Assert( _domainPostActionsErrorSource != null );
                _forDomainPostActionsExecutor.SetResult( null );
                _domainPostActionsErrorSource.SetResult( null );
            }
            return Task.CompletedTask;
        }
        Debug.Assert( _forDomainPostActionsExecutor != null, "This result has been submitted to the Domain executor." );

        // If domain post actions must not wait for post actions, set the domain post actions immediately, regardless of the
        // number of post actions count.
        if( parallelDomainPostActions )
        {
            _forDomainPostActionsExecutor.SetResult( d );
        }
        if( l.ActionCount > 0 )
        {
            return Execute();
        }
        if( !parallelDomainPostActions ) _forDomainPostActionsExecutor.SetResult( d );

        return Task.CompletedTask;

        async Task Execute()
        {
            var ctx = new PostActionContext( m, l, this );
            try
            {
                Debug.Assert( _domainName != null, "TransactionResult on valid result constructor sets this." );
                _postActionsError = await ctx.ExecuteAsync( throwException: false, name: $"domain '{_domainName}' (PostActions)" ).ConfigureAwait( false );
                if( !parallelDomainPostActions )
                {
                    if( _postActionsError != null )
                    {
                        ForgetDomainActions();
                    }
                    else
                    {
                        _forDomainPostActionsExecutor.SetResult( d );
                    }
                }
            }
            catch( Exception ex )
            {
                _postActionsError = ex;
                if( !parallelDomainPostActions ) ForgetDomainActions();
            }
            finally
            {
                await ctx.DisposeAsync().ConfigureAwait( false );
            }
        }

        void ForgetDomainActions()
        {
            if( d != null && d.ActionCount > 0 )
            {
                m.Warn( $"Skipping execution of {d.ActionCount} domain post actions since executing a post action raised an error." );
            }
            _forDomainPostActionsExecutor.SetResult( null );
            // No execution leads to non error.
            Debug.Assert( _domainPostActionsErrorSource != null );
            _domainPostActionsErrorSource.SetResult( null );
        }
    }

    internal Task<ActionRegistrar<PostActionContext>?> DomainActions => _forDomainPostActionsExecutor!.Task;

    internal void SetDomainPostActionsResult( Exception? result )
    {
        Debug.Assert( _domainPostActionsErrorSource != null );
        _domainPostActionsErrorSource.SetResult( result );
    }

    internal TransactionResult( TransactionDoneEventArgs c )
    {
        Debug.Assert( c.RollbackedInfo?.Failure.RollbackedInfo == null, "A rollback is not rolled back." );
        _domainName = c.Domain.DomainName;
        StartTimeUtc = c.StartTimeUtc;
        CommitTimeUtc = c.CommitTimeUtc;
        Commands = c._commands;
        Errors = Array.Empty<CKExceptionData>();
        TransactionNumber = c.TransactionNumber;
        _domainPostActions = c._domainPostActions;
        _postActions = c._localPostActions;
        TransactionDoneErrors = Array.Empty<CKExceptionData>();
        CommandHandlingErrors = Array.Empty<(object, CKExceptionData)>();
        RollbackedInfo = c.RollbackedInfo;
        // Will be changed by InitializeOnSuccess to the _domainPostActionsErrorSource.Task
        // that will be set by the ObservableDomainPostActionExecutor.
        _domainPostActionsError = _noErrorResult;
    }

    internal TransactionResult( IReadOnlyList<CKExceptionData> errors, DateTime startTime )
    {
        Debug.Assert( _postActions == null, "Leave the _postActions null as if the ExecutePostActions has been already called." );
        Debug.Assert( startTime != Util.UtcMinValue || EmptySuccess == null, "startTime == Util.UtcMinValue ==> EmptySuccess is being initialized." );
        StartTimeUtc = startTime;
        CommitTimeUtc = DateTime.UtcNow;
        Errors = errors;
        Debug.Assert( TransactionNumber == 0 );
        Commands = Array.Empty<ObservableDomainCommand>();
        TransactionDoneErrors = Array.Empty<CKExceptionData>();
        CommandHandlingErrors = Array.Empty<(object, CKExceptionData)>();
        _domainPostActionsError = _noErrorResult;
    }

    internal TransactionResult( Exception onStartClientError )
        : this( CKExceptionData.CreateFrom( onStartClientError ), false )
    {
    }

    internal TransactionResult( CKExceptionData data, bool isTimeout )
    {
        Debug.Assert( data != null );
        Debug.Assert( _postActions == null, "Leave the _postActions null as if the ExecutePostActions has been already called." );
        StartTimeUtc = CommitTimeUtc = DateTime.UtcNow;
        Errors = new[] { OnClientStartError = data };
        Debug.Assert( TransactionNumber == 0 );
        Commands = Array.Empty<ObservableDomainCommand>();
        TransactionDoneErrors = Array.Empty<CKExceptionData>();
        CommandHandlingErrors = Array.Empty<(object, CKExceptionData)>();
        _domainPostActionsError = _noErrorResult;
        IsStartTimeoutError = isTimeout;
    }

    internal void InitializeOnSuccess()
    {
        Debug.Assert( _postActions != null, "This result was initially successful." );
        _forDomainPostActionsExecutor = new TaskCompletionSource<ActionRegistrar<PostActionContext>?>( TaskCreationOptions.RunContinuationsAsynchronously );
        _domainPostActionsErrorSource = new TaskCompletionSource<Exception?>( TaskCreationOptions.RunContinuationsAsynchronously );
        _domainPostActionsError = _domainPostActionsErrorSource.Task;
    }

    internal void SetClientError( Exception ex )
    {
        ClientError = CKExceptionData.CreateFrom( ex );
    }

    internal void SetSuccessfulTransactionErrors( IReadOnlyList<CKExceptionData> errors )
    {
        TransactionDoneErrors = errors;
    }

    internal void SetCommandHandlingErrors( IReadOnlyList<(object, CKExceptionData)> errors )
    {
        CommandHandlingErrors = errors;
    }
}
