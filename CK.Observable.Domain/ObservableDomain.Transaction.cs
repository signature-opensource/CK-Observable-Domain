using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CK.Observable
{

    public partial class ObservableDomain
    {
        /// <summary>
        /// Implements <see cref="IObservableTransaction"/>.
        /// </summary>
        sealed class Transaction : IObservableTransaction
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

                SuccessfulTransactionEventArgs? ctx = null;
                bool needPseudoSuccessTransaction = false;
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
                            // The fact that HasWaitingSidekick is true here is because an error occurred.
                            // But this is not enough to know that a rollback has been made.
                            // We don't want to create an automatic transaction here to create the waiting sidekicks if the transaction failed
                            // and the domain is in a non rolled back dirty state or if the Load itself failed.
                            if( _lastLoadStatus.IsDeserializing() && _domain._sidekickManager.HasWaitingSidekick )
                            {
                                using( Monitor.OpenWarn( "A rollback occurred. Instantiating sidekicks." ) )
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
                                    }
                                    else
                                    {
                                        // We saved the day... But we need a somehow "successful" transaction to execute the
                                        // impacts of the rollback.
                                        needPseudoSuccessTransaction = true;
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

                if( _result == null || needPseudoSuccessTransaction )
                {
                    using( Monitor.OpenDebug( needPseudoSuccessTransaction
                                                ? "Transaction rolled back: Handling sidekick instantiation side effects."
                                                : "Transaction has no error. Calling DomainClient.OnTransactionCommit." ) )
                    {
                        ctx = _domain._changeTracker.Commit( _domain,
                                                             _domain.EnsurePropertyInfo,
                                                             _startTime,
                                                             ++_domain._transactionSerialNumber,
                                                             _result != null
                                                                ? new RolledbackTransactionInfo( _result, _lastLoadStatus == CurrentTransactionStatus.Rollingback )
                                                                : null );
                        _domain._transactionCommitTimeUtc = ctx.CommitTimeUtc;
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
                // Back to Readable lock: publishes SuccessfulTransaction.
                if( _result.Success )
                {
                    Debug.Assert( ctx != null );

                    using( Monitor.OpenDebug( "Raising SuccessfulTransaction event." ) )
                    {
                        var errors = _domain.RaiseOnSuccessfulTransaction( ctx );
                        if( errors != null ) _result.SetSuccessfulTransactionErrors( errors );
                    }
                }
                // Before leaving the read lock (nobody can start a new transaction), let's enqueue
                // the transaction result.
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
}
