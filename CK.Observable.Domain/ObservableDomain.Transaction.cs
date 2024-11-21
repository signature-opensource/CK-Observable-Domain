using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CK.Observable;


public partial class ObservableDomain
{
    /// <summary>
    /// Implements <see cref="IInternalTransaction"/>.
    /// </summary>
    sealed class Transaction : IInternalTransaction
    {
        readonly ObservableDomain? _previous;
        readonly ObservableDomain _domain;
        readonly IDisposableGroup _monitorGroup;
        readonly DateTime _startTime;
        CKExceptionData[] _errors;
        TransactionResult? _result;

        // Internal flag set by Load.
        internal CurrentTransactionStatus _lastLoadStatus;

        public Transaction( ObservableDomain d, IActivityMonitor monitor, DateTime startTime, IDisposableGroup g )
        {
            _domain = d;
            Monitor = monitor;
            _previous = CurrentThreadDomain;
            CurrentThreadDomain = d;
            _startTime = startTime;
            _monitorGroup = g;
            _errors = Array.Empty<CKExceptionData>();
        }

        public DateTime StartTime => _startTime;

        public IActivityMonitor Monitor { get; }

        public IReadOnlyList<CKExceptionData> Errors => _errors;

        public void AddError( Exception ex ) => AddError( CKExceptionData.CreateFrom( ex ) );

        public void AddError( CKExceptionData d )
        {
            // Be safe here.
            if( d != null )
            {
                Array.Resize( ref _errors, _errors.Length + 1 );
                _errors[_errors.Length - 1] = d;
            }
        }

        public TransactionResult Commit()
        {
            // If result has already been initialized, we exit immediately.
            if( _result != null ) return _result;

            Debug.Assert( _domain._currentTran == this );
            Debug.Assert( _domain._lock.IsWriteLockHeld );

            TransactionDoneEventArgs? ctx = null;
            bool rollbackHasSidekicks = false;
            if( _errors.Length != 0 )
            {
                using( Monitor.OpenWarn( "Committing a Transaction on error. Calling DomainClient.OnTransactionFailure." ) )
                {
                    // On errors, resets the change tracker and sends the errors to the Clients.
                    // No new transaction error can appear here, we can create the result.
                    _result = new TransactionResult( _errors, _startTime );
                    _domain._changeTracker.Reset();
                    try
                    {
                        _domain.DomainClient?.OnTransactionFailure( Monitor, _domain, _errors );
                        // Handle rollback.
                        if( _lastLoadStatus.IsDeserializing() )
                        {
                            if( _domain._sidekickManager.HasWaitingSidekick )
                            {
                                using( Monitor.OpenWarn( $"Rollback {_lastLoadStatus}: Instantiating sidekicks." ) )
                                {
                                    Exception? firstError = null;
                                    int c = _errors.Length;
                                    _domain._sidekickManager.CreateWaitingSidekicks( Monitor,
                                                                                     ex => { if( firstError == null ) firstError = ex; },
                                                                                     finalCall: true );
                                    // If an error occurred, consider it a Client (critical) error.
                                    if( firstError != null )
                                    {
                                        _result.SetClientError( firstError );
                                        _lastLoadStatus = CurrentTransactionStatus.Regular;
                                    }
                                    else
                                    {
                                        rollbackHasSidekicks = true;
                                    }
                                }
                            }
                        }
                    }
                    catch( Exception ex )
                    {
                        Monitor.Error( "Error in DomainClient.OnTransactionFailure.", ex );
                        _result.SetClientError( ex );
                        _lastLoadStatus = CurrentTransactionStatus.Regular;
                    }
                }
            }

            if( _result == null || _lastLoadStatus.IsDeserializing() )
            {
                using( Monitor.OpenDebug( _result == null
                                            ? "Transaction has no error. Calling DomainClient.OnTransactionCommit."
                                            : rollbackHasSidekicks
                                                ? "Transaction rolled back: Handling sidekick instantiation side effects."
                                                : "Transaction rolled back." ) )
                {
                    // Should we always increment the transaction status (take the max of the previous and restored + 1)?
                    // For the moment, we always increment the current or restored one...
                    // except if no sidekicks have been instantiated: we stay on the restored one.
                    if( _result == null || rollbackHasSidekicks )
                    {
                        ++_domain._transactionSerialNumber;
                    }
                    ctx = _domain._changeTracker.Commit( _domain,
                                                         _domain.EnsurePropertyInfo,
                                                         _startTime,
                                                         _domain._transactionSerialNumber,
                                                         _result != null
                                                            ? new RolledbackTransactionInfo( _result, _lastLoadStatus == CurrentTransactionStatus.Rollingback )
                                                            : null );
                    _domain._transactionCommitTimeUtc = ctx.CommitTimeUtc;
                    // Swaps the result (if it was not null - on error, it has been captured in the TransactionDoneEventArgs ctx...
                    // But from now on, this result is the "real" transaction and may have any number of side effects triggered by a roll back).
                    _result = new TransactionResult( ctx );
                    try
                    {
                        _domain.DomainClient?.OnTransactionCommit( ctx );
                    }
                    catch( Exception ex )
                    {
                        Monitor.Fatal( "Error in IObservableDomainClient.OnTransactionCommit. This is a Critical error since the Domain state integrity may be compromised.", ex );
                        _result.SetClientError( ex );
                        ctx = null;
                    }
                }
            }

            CurrentThreadDomain = _previous;
            _domain._currentTran = null;
            _monitorGroup.Dispose();

            Monitor.Debug( "Leaving WriteLock." );
            _domain._lock.ExitWriteLock();
            // Back to Readable lock: publishes SuccessfulTransaction
            if( _result.Success )
            {
                Debug.Assert( ctx != null );

                using( Monitor.OpenDebug( "Raising TransactionDone event." ) )
                {
                    var errors = _domain.RaiseTransactionEventResult( ctx );
                    if( errors != null ) _result.SetSuccessfulTransactionErrors( errors );
                }
            }
            // Before leaving the read lock (nobody can start a new transaction), let's enqueue
            // the transaction result (if no error have been added by RaiseTransactionResult above).
            if( _result.Success  )
            {
                _result.InitializeOnSuccess();
                _domain._domainPostActionExecutor.Enqueue( _result );
            }
            Monitor.Debug( "Leaving UpgradeableReadLock." );
            _domain._lock.ExitUpgradeableReadLock();
            // Outside of the lock: on success, sidekicks execute the Command objects.
            if( _result.Success )
            {
                using( Monitor.OpenDebug( "No error so far: submitting Commands to sidekicks." ) )
                {
                    Debug.Assert( _result._postActions != null && _result._domainPostActions != null );
                    var errors = _domain._sidekickManager.ExecuteCommands( Monitor, _result, _result._postActions, _result._domainPostActions );
                    if( errors != null ) _result.SetCommandHandlingErrors( errors );
                }
            }
            Monitor.Debug( $"Committed: {_result}" );
            return _result;
        }

        public void Dispose()
        {
            if( _domain._currentTran == this )
            {
                // Disposing the transaction without a Commit is an error (that
                // may trigger a rollback if a domain client can do it).
                AddError( UncomittedTransaction );
                Commit();
            }
        }
    }

}
