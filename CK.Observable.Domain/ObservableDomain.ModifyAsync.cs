using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    public partial class ObservableDomain
    {
        /// <summary>
        /// Allow modifications of this ObservableDomain, and on success executes the <see cref="TransactionDoneEventArgs.PostActions"/> and
        /// send the <see cref="TransactionDoneEventArgs.DomainPostActions"/> to a background executor so that they are executed in the same
        /// order as the transactions that emitted them.
        /// <para>
        /// This always throw on any error.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="actions">
        /// The actions to execute inside the ObservableDomain's current transaction.
        /// Can be null: only waiting sidekick instantiation and pending timed events are executed if any.
        /// </param>
        /// <param name="millisecondsTimeout">
        /// The maximum number of milliseconds to wait for a read access before throwing.
        /// Wait indefinitely by default.
        /// </param>
        /// <param name="considerRolledbackAsFailure">
        /// True to throw, even if the failed transaction has been successfully rolled back.
        /// </param>
        /// <param name="parallelDomainPostActions">
        /// False to wait for the success of the <see cref="TransactionDoneEventArgs.PostActions"/> that are executed here before
        /// allowing the <see cref="TransactionDoneEventArgs.DomainPostActions"/> to run: if any post action fails, domain post actions are skipped.
        /// <para>
        /// By default, post actions are executed and domain post actions can immediately be executed by the internal executor (as
        /// soon as all previous transaction's domain post actions have ran of course).
        /// </para>
        /// </param>
        /// <param name="waitForDomainPostActionsCompletion">
        /// True to wait for the completions of all the domain post actions emitted by the transaction.
        /// By default, only the transaction's post actions are awaited.
        /// </param>
        /// <returns>The transaction result.</returns>
        public Task<TransactionResult> ModifyThrowAsync( IActivityMonitor monitor,
                                                         Action? actions,
                                                         int millisecondsTimeout = -1,
                                                         bool considerRolledbackAsFailure = true,
                                                         bool parallelDomainPostActions = true,
                                                         bool waitForDomainPostActionsCompletion = false )
        {
            return ModifyAsync( monitor,
                                actions,
                                true,
                                millisecondsTimeout,
                                considerRolledbackAsFailure,
                                parallelDomainPostActions,
                                waitForDomainPostActionsCompletion );
        }

        /// <summary>
        /// Same as <see cref="ModifyThrowAsync"/> with a returned value. Using this (when errors must be thrown) is easier and avoids a closure.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="actions">
        /// The actions to execute inside the ObservableDomain's current transaction that returns a result.
        /// </param>
        /// <param name="millisecondsTimeout">
        /// The maximum number of milliseconds to wait for a read access before returning <see cref="TransactionResult.Empty"/>.
        /// Wait indefinitely by default.
        /// </param>
        /// <param name="parallelDomainPostActions">
        /// False to wait for the success of the <see cref="TransactionDoneEventArgs.PostActions"/> that are executed here before
        /// allowing the <see cref="TransactionDoneEventArgs.DomainPostActions"/> to run: if any post action fails, domain post actions are skipped.
        /// <para>
        /// By default, post actions are executed and domain post actions can immediately be executed by the internal executor (as
        /// soon as all previous transaction's domain post actions have ran of course).
        /// </para>
        /// </param>
        /// <param name="waitForDomainPostActionsCompletion">
        /// True to wait for the completions of all the domain post actions emitted by the transaction.
        /// By default, only the transaction's post actions are awaited.
        /// </param>
        /// <returns>The action's result.</returns>
        public async Task<TResult> ModifyThrowAsync<TResult>( IActivityMonitor monitor,
                                                              Func<TResult> actions,
                                                              int millisecondsTimeout = -1,
                                                              bool parallelDomainPostActions = true,
                                                              bool waitForDomainPostActionsCompletion = false )
        {
            Throw.CheckNotNullArgument( actions );
            TResult result = default!;
            await ModifyAsync( monitor,
                               () => result = actions(),
                               false,
                               millisecondsTimeout,
                               true,
                               parallelDomainPostActions,
                               waitForDomainPostActionsCompletion );
            return result;
        }

        /// <summary>
        /// Allow modifications of this ObservableDomain, and on success executes the <see cref="TransactionDoneEventArgs.PostActions"/> and
        /// send the <see cref="TransactionDoneEventArgs.DomainPostActions"/> to a background executor so that they are executed in the same
        /// order as the transactions that emitted them.
        /// <para>
        /// This never throw: the returned result captures all the possible errors.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="actions">
        /// The actions to execute inside the ObservableDomain's current transaction.
        /// Can be null: only waiting sidekick instantiation and pending timed events are executed if any.
        /// </param>
        /// <param name="millisecondsTimeout">
        /// The maximum number of milliseconds to wait for a read access before returning <see cref="TransactionResult.Empty"/>.
        /// Wait indefinitely by default.
        /// </param>
        /// <param name="considerRolledbackAsFailure">
        /// False to consider that rolling back is "normal: the (hopefully successful) transaction is returned.
        /// <para>
        /// By default a successful rollback is an error: the <see cref="RolledbackTransactionInfo.Failure"/> of the
        /// <see cref="TransactionResult.RollbackedInfo"/> is returned if the transaction has been successfully rolled back
        /// (the successful roll back did its job and it cannot be observed since the failure is returned).
        /// </para>
        /// </param>
        /// <param name="parallelDomainPostActions">
        /// False to wait for the success of the <see cref="TransactionDoneEventArgs.PostActions"/> that are executed here before
        /// allowing the <see cref="TransactionDoneEventArgs.DomainPostActions"/> to run: if any post action fails, domain post actions are skipped.
        /// <para>
        /// By default, post actions are executed by this method and domain post actions can immediately be executed by the internal executor (as
        /// soon as all previous transaction's domain post actions have ran of course).
        /// </para>
        /// </param>
        /// <param name="waitForDomainPostActionsCompletion">
        /// True to wait for the completions of all the domain post actions emitted by the transaction.
        /// By default, only the transaction's post actions that are executed by this method are awaited.
        /// </param>
        /// <returns>The transaction result.</returns>
        public Task<TransactionResult> TryModifyAsync( IActivityMonitor monitor,
                                                       Action? actions,
                                                       int millisecondsTimeout = -1,
                                                       bool considerRolledbackAsFailure = true,
                                                       bool parallelDomainPostActions = true,
                                                       bool waitForDomainPostActionsCompletion = false )
        {
            return ModifyAsync( monitor,
                                actions,
                                false,
                                millisecondsTimeout,
                                considerRolledbackAsFailure,
                                parallelDomainPostActions,
                                waitForDomainPostActionsCompletion );
        }

        /// <summary>
        /// Allow modifications of this ObservableDomain, and on success executes the <see cref="TransactionDoneEventArgs.PostActions"/> and
        /// send the <see cref="TransactionDoneEventArgs.DomainPostActions"/> to a background executor so that they are executed in the same
        /// order as the transactions that emitted them.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="actions">
        /// The actions to execute inside the ObservableDomain's current transaction.
        /// Can be null: only waiting sidekick instantiation and pending timed events are executed if any.
        /// </param>
        /// <param name="throwException">
        /// True to throw on any error.
        /// When false, the returned result captures the errors.
        /// </param>
        /// <param name="millisecondsTimeout">
        /// The maximum number of milliseconds to wait for a read access before giving up.
        /// Wait indefinitely by default.
        /// </param>
        /// <param name="considerRolledbackAsFailure">
        /// False to consider that rolling back is "normal: the (hopefully successful) transaction is returned.
        /// <para>
        /// By default a successful rollback is an error: the <see cref="RolledbackTransactionInfo.Failure"/> of the
        /// <see cref="TransactionResult.RollbackedInfo"/> is returned if the transaction has been successfully rolled back
        /// (the successful roll back did its job and it cannot be observed since the failure is returned).
        /// </para>
        /// </param>
        /// <param name="parallelDomainPostActions">
        /// False to wait for the success of the <see cref="TransactionDoneEventArgs.PostActions"/> that are executed here before
        /// allowing the <see cref="TransactionDoneEventArgs.DomainPostActions"/> to run: if any post action fails, domain post actions are skipped.
        /// <para>
        /// By default, post actions are executed by this method and domain post actions can immediately be executed by the internal executor (as
        /// soon as all previous transaction's domain post actions have ran of course).
        /// </para>
        /// </param>
        /// <param name="waitForDomainPostActionsCompletion">
        /// True to wait for the completions of all the domain post actions emitted by the transaction.
        /// By default, only the transaction's post actions that are executed by this method are awaited.
        /// </param>
        /// <returns>The transaction result.</returns>
        public async Task<TransactionResult> ModifyAsync( IActivityMonitor monitor,
                                                          Action? actions,
                                                          bool throwException,
                                                          int millisecondsTimeout,
                                                          bool considerRolledbackAsFailure,
                                                          bool parallelDomainPostActions,
                                                          bool waitForDomainPostActionsCompletion )
        {
            Throw.CheckNotNullArgument( monitor );
            if( !TryEnterUpgradeableReadAndWriteLockAtOnce( millisecondsTimeout ) )
            {
                var msg = $"Write lock not obtained in less than {millisecondsTimeout} ms.";
                if( throwException ) Throw.Exception( msg );
                monitor.Warn( msg );
                return new TransactionResult( CKExceptionData.Create( msg ), isTimeout: true );
            }
            var (tx, ex) = DoCreateObservableTransaction( monitor, throwException );
            Debug.Assert( (tx != null) != (ex != null), "The Transaction XOR IObservableDomainClient.OnTransactionStart() exception." );

            if( ex != null ) return new TransactionResult( ex );
            Debug.Assert( tx != null );
            var tr = DoModifyAndCommit( actions, tx );
            await tr.ExecutePostActionsAsync( monitor, parallelDomainPostActions ).ConfigureAwait( false );
            if( throwException ) tr.ThrowOnFailure( considerRolledbackAsFailure );
            if( waitForDomainPostActionsCompletion )
            {
                await tr.DomainPostActionsError;
                if( throwException ) tr.ThrowOnFailure( considerRolledbackAsFailure );
            }
            return considerRolledbackAsFailure && tr.RollbackedInfo != null ? tr.RollbackedInfo.Failure : tr;
        }

        bool TryEnterUpgradeableReadAndWriteLockAtOnce( int millisecondsTimeout )
        {
            var start = DateTime.UtcNow;
            if( _lock.TryEnterUpgradeableReadLock( millisecondsTimeout ) )
            {
                if( millisecondsTimeout > 0 )
                {
                    millisecondsTimeout -= ((int)(DateTime.UtcNow.Ticks - start.Ticks) / (int)TimeSpan.TicksPerMillisecond);
                    if( millisecondsTimeout < 0 ) millisecondsTimeout = 0;
                }
                if( _lock.TryEnterWriteLock( millisecondsTimeout ) )
                {
                    return true;
                }
                _lock.ExitUpgradeableReadLock();
            }
            return false;
        }

        /// <summary>
        /// Returns the created Transaction XOR an IObservableDomainClient.OnTransactionStart exception.
        /// Write lock must be held before the call and kept until (but released on error).
        /// </summary>
        /// <param name="m">The monitor to use.</param>
        /// <param name="throwException">Whether to throw or return the potential IObservableDomainClient.OnTransactionStart exception.</param>
        /// <returns>The transaction XOR the IObservableDomainClient.OnTransactionStart exception.</returns>
        (Transaction?, Exception?) DoCreateObservableTransaction( IActivityMonitor m, bool throwException )
        {
            Debug.Assert( m != null && _lock.IsWriteLockHeld );
            var startTime = DateTime.UtcNow;
            // This group is left Open on success. It will be closed at the end of the transaction.
            IDisposableGroup group = null!;
            try
            {
                group = m.OpenTrace( "Starting transaction." );
                // This could throw and be handled just like other pre-transaction errors (when a buggy client throws during OnTransactionStart).
                // Depending on throwException parameter, it will be re-thrown or returned (returning the exception is for TryModifyAsync).
                // See DoDispose method for the discussion about disposal...
                CheckDisposed();
                DomainClient?.OnTransactionStart( m, this, startTime );
            }
            catch( Exception ex )
            {
                m.Error( "While calling IObservableDomainClient.OnTransactionStart().", ex );
                group.Dispose();
                _lock.ExitWriteLock();
                if( throwException ) throw;
                return (null, ex);
            }
            // No OnTransactionStart error.
            var t = new Transaction( this, m, startTime, group );
            _currentTran = t;
            return (t, null);
        }

        /// <summary>
        /// Modify the domain once a transaction has been opened and calls the <see cref="IObservableDomainClient"/>:
        /// all this occurs in the lock and it is released at the end.
        /// This never throws since the transaction result contains the errors.
        /// </summary>
        /// <param name="actions">The actions to execute.</param>
        /// <param name="t">The transaction.</param>
        /// <returns>The transaction result. Will never be null.</returns>
        TransactionResult DoModifyAndCommit( Action? actions, Transaction t )
        {
            Debug.Assert( t != null );
            try
            {
                if( _sidekickManager.HasWaitingSidekick )
                {
                    // Starting a transaction with waiting sidekicks means that we have just deserialized or initialized the domain.
                    // If sidekick instantiation fails, this is a serious error: the transaction will fail on error.
                    _sidekickManager.CreateWaitingSidekicks( t.Monitor, t.AddError, false );
                }
                if( _timeManager.IsRunning )
                {
                    _timeManager.RaiseElapsedEvent( t.Monitor, t.StartTime );
                }
                foreach( var tracker in _trackers )
                {
                    tracker.BeforeModify( t.Monitor, t.StartTime );
                }
                bool updatedMinHeapDone = false;
                if( actions != null )
                {
                    actions();
                }
                // Always call the "final call" to update the lock free sidekick index.
                if( _sidekickManager.CreateWaitingSidekicks( t.Monitor, t.AddError, true ) )
                {
                    var now = DateTime.UtcNow;
                    foreach( var tracker in _trackers ) tracker.AfterModify( t.Monitor, t.StartTime, now - t.StartTime );
                    if( _timeManager.IsRunning )
                    {
                        updatedMinHeapDone = true;
                        _timeManager.RaiseElapsedEvent( t.Monitor, now );
                    }
                }
                if( !updatedMinHeapDone )
                {
                    // If the time manager is not running, we must
                    // handle the changed timed events so that the
                    // active timed event min heap is up to date.
                    _timeManager.UpdateMinHeap();
                }
            }
            catch( Exception ex )
            {
                bool swallowError = false;
                Exception? exOnUnhandled = null;
                if( DomainClient != null )
                {
                    try
                    {
                        DomainClient?.OnUnhandledException( t.Monitor, this, ex, ref swallowError );
                    }
                    catch( Exception ex2 )
                    {
                        swallowError = false;
                        exOnUnhandled = ex2;
                    }
                }
                if( !swallowError )
                {
                    t.Monitor.Error( ex );
                    t.AddError( ex );
                    if( exOnUnhandled != null )
                    {
                        t.Monitor.Error( "Unhandled exception while calling DomainClient.OnUnhandledError with the previous exception.", exOnUnhandled );
                        t.AddError( exOnUnhandled );
                    }
                }
            }
            return t.Commit();
        }

    }
}
