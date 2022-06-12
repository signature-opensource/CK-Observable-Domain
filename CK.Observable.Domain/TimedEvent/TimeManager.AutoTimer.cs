using CK.Core;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Observable
{
    public sealed partial class TimeManager
    {
        /// <summary>
        /// Default implementation that is available on <see cref="Timer"/> and can be specialized
        /// and replaced as needed... Well... If you really need it, that I strongly doubt.
        /// </summary>
        public sealed class AutoTimer : IDisposable
        {
            static readonly TimerCallback _timerCallback = new TimerCallback( OnTime );
            static readonly WaitCallback _waitCallback = new WaitCallback( OnTime );

            readonly Timer _timer;
            // This is the shell for the trampoline state and the object used
            // for the RaiseWait().
            readonly TrampolineWorkItem _fromWorkItem;
            DateTime _nextDueTime;
            // int.MinValue when this AutoTimer is disposed,
            // -1 when it will be disposed.
            // 0 when no lost tick,
            // 1 when a reentrant tick has been detected (a trampoline is waiting).
            int _onTimeLostFlag;

            /// <summary>
            /// Initializes a new <see cref="AutoTimer"/> for a domain.
            /// <see cref="IsActive"/> is initially false.
            /// </summary>
            /// <param name="domain">The associated domain.</param>
            public AutoTimer( ObservableDomain domain )
            {
                Domain = domain ?? throw new ArgumentNullException( nameof( domain ) );
                _timer = new Timer( _timerCallback, this, Timeout.Infinite, Timeout.Infinite );
                _fromWorkItem = new TrampolineWorkItem( this );
                _nextDueTime = Util.UtcMinValue;
            }

            class TrampolineWorkItem
            {
                public readonly AutoTimer Timer;

                public TrampolineWorkItem( AutoTimer t ) => Timer = t;
            }

            static void OnTime( object? state )
            {
                Debug.Assert( state is AutoTimer || state is TrampolineWorkItem );

                bool trampolineRequired = false;
                var ts = state as TrampolineWorkItem;
                var t = ts?.Timer ?? (AutoTimer)state;
                if( t._nextDueTime == Util.UtcMinValue || t._onTimeLostFlag < 0 ) return;

                var domain = t.Domain;
                var monitor = domain.ObtainDomainMonitor( 10, createAutonomousOnTimeout: false );
                if( monitor != null )
                {
                    // All this stuff is to do exactly what must be done. No more.
                    // This ensures that only ONE trampoline is active at a time and
                    // that if it became useless (because a regular timer call occurred), it is skipped.
                    if( ts == null )
                    {
                        if( Interlocked.CompareExchange( ref t._onTimeLostFlag, 0, 1 ) == 1 )
                        {
                            monitor.OpenDebug( $"Executing OnTime while a trampoline is pending on Domain '{domain.DomainName}'." );
                        }
                        else
                        {
                            if( t._onTimeLostFlag < 0 )
                            {
                                monitor.Debug( $"Skipped OnTime on disposed timer for '{domain.DomainName}'." );
                                monitor.Dispose();
                                return;
                            }
                            monitor.OpenDebug( $"Executing OnTime on Domain '{domain.DomainName}'." );
                        }
                    }
                    else
                    {
                        if( t._onTimeLostFlag <= 0 )
                        {
                            if( t._onTimeLostFlag == 0 )
                            {
                                monitor.Debug( $"Skipped useless OnTime trampoline on Domain '{domain.DomainName}'." );
                            }
                            else
                            {
                                monitor.Debug( $"Skipped useless OnTime trampoline on dispose Domain '{domain.DomainName}'." );
                            }
                            monitor.Dispose();
                            return;
                        }
                        monitor.OpenDebug( $"Executing trampoline OnTime on Domain '{domain.DomainName}'." );
                    }
                    int eventRaisedCount = domain.TimeManager._totalEventRaised;
                    _ = domain.ModifyAsync( monitor,
                                            null,
                                            throwException: false,
                                            millisecondsTimeout: 10,
                                            considerRolledbackAsFailure: false,
                                            parallelDomainPostActions: true,
                                            waitForDomainPostActionsCompletion: false )
                              .ContinueWith( r =>
                              {
                                  // On success, unhandled exception or cancellation, we do nothing.
                                  // We only handle one case: if the write lock obtention failed we need the trampoline
                                  // to retry asap.
                                  if( r.IsFaulted ) monitor.Fatal( "Unhandled exception from domain.ModifyAsync with throwException: false. This cannot happen.", r.Exception );
                                  else
                                  {
                                      if( r.IsCanceled ) monitor.Warn( "Async operation canceled." );
                                      else if( r.Result == TransactionResult.Empty )
                                      {
                                          // Failed to obtain the write lock.
                                          trampolineRequired = true;
                                      }
                                  }
                                  monitor.Dispose();
                                  if( domain.TimeManager._totalEventRaised != eventRaisedCount ) t.RaiseWait();
                              }, default, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default );
                }
                else trampolineRequired = true;

                // If we need the trampoline, we only need it if we are currently in 'the' trampoline call or there is no
                // active trampoline.
                if( trampolineRequired && (ts != null || Interlocked.CompareExchange( ref t._onTimeLostFlag, 1, 0 ) == 0) )
                {
                    ThreadPool.UnsafeQueueUserWorkItem( _waitCallback, t._fromWorkItem );
                }
            }

            /// <summary>
            /// Waits until the next call to the Domain.ModifyAsync from the internal timer finished.
            /// </summary>
            /// <param name="millisecondsTimeout">The number of milliseconds to wait before giving up.</param>
            /// <returns>
            /// True if a call to Domain.ModifyAsync from the internal timer finished before the timeout.
            /// False otherwise.
            /// </returns>
            public bool WaitForNext( int millisecondsTimeout = Timeout.Infinite )
            {
                lock( _fromWorkItem ) return Monitor.Wait( _fromWorkItem, millisecondsTimeout );
            }

            internal void RaiseWait()
            {
                lock( _fromWorkItem ) Monitor.PulseAll( _fromWorkItem );
            }

            /// <summary>
            /// Gets whether this AutoTimer has been <see cref="Dispose"/>d.
            /// </summary>
            public bool IsDisposed => _onTimeLostFlag < 0;

            /// <summary>
            /// Gets the domain to which this timer is bound.
            /// Note that this <see cref="AutoTimer"/> may not be the <see cref="TimeManager.Timer"/> of the domain.
            /// </summary>
            public ObservableDomain Domain { get; }

            /// <summary>
            /// Gets the next due time of the timer.
            /// Defaults to <see cref="Util.UtcMinValue"/> when nothing is planned.
            /// </summary>
            public DateTime NextDueTime => _nextDueTime;

            /// <summary>
            /// Must do whatever is needed to call back this <see cref="OnDueTimeAsync(IActivityMonitor)"/> at <paramref name="nextDueTimeUtc"/> (or
            /// right after but not before!).
            /// This is called while the domain's write lock is held.
            /// </summary>
            /// <param name="monitor">The monitor to use.</param>
            /// <param name="nextDueTimeUtc">
            /// The expected callback time. <see cref="Util.UtcMinValue"/> pauses the timer (just like when <see cref="IsActive"/> is false).
            /// </param>
            public void SetNextDueTimeUtc( IActivityMonitor monitor, DateTime nextDueTimeUtc )
            {
                // Fast path (nonetheless normalizing max to min).
                if( nextDueTimeUtc == Util.UtcMaxValue ) nextDueTimeUtc = Util.UtcMinValue;
                if( nextDueTimeUtc == _nextDueTime ) return;

                Throw.CheckNotNullArgument( monitor );
                // Allow Dispose to have been called here.
                if( _onTimeLostFlag < 0 )
                {
                    monitor.Warn( _onTimeLostFlag == -1 ? "Timer is being disposed." : "Timer has been disposed." );
                    nextDueTimeUtc = Util.UtcMinValue;
                    return;
                }
                _nextDueTime = nextDueTimeUtc;
                if( nextDueTimeUtc == Util.UtcMinValue )
                {
                    _timer.Change( Timeout.Infinite, Timeout.Infinite );
                    monitor.Debug( $"System.Timer paused." );
                }
                else
                {
                    var delta = nextDueTimeUtc - DateTime.UtcNow;
                    var ms = (int)Math.Ceiling( delta.TotalMilliseconds );
                    if( ms <= 0 ) ms = 0;
                    if( !_timer.Change( ms, 100 ) )
                    {
                        var msg = $"System.Timer.Change({ms}) failed.";
                        monitor.Warn( msg );
                        _timer.Change( Timeout.Infinite, Timeout.Infinite );
                        if( !_timer.Change( ms, 100 ) )
                        {
                            monitor.Fatal( msg );
                            return;
                        }
                    }
                    monitor.Debug( $"System.Timer set in {ms} ms." );
                }
            }

            internal void QuickStopBeforeDispose()
            {
                Interlocked.Exchange( ref _onTimeLostFlag, -1 );
            }

            /// <summary>
            /// Disposes the internal <see cref="System.Threading.Timer"/> object and makes this AutoTimer silent.
            /// This is automatically called by <see cref="ObservableDomain.Dispose()"/> on the <see cref="TimeManager.Timer"/>:
            /// this must be called only to disable the real timer (testing should be the only context where it makes sense).
            /// </summary>
            public void Dispose()
            {
                if( _onTimeLostFlag >= -1 )
                {
                    _onTimeLostFlag = int.MinValue;
                    _timer.Dispose();
                }
            }
        }
    }
}

