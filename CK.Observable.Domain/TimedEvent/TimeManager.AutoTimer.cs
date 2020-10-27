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
        public class AutoTimer : IDisposable
        {
            static readonly TimerCallback _timerCallback = new TimerCallback( OnTime );
            static readonly WaitCallback _waitCallback = new WaitCallback( OnTime );

            readonly Timer _timer;
            // This is the shell for the trampoline state and the object used
            // for the RaiseWait().
            readonly TrampolineWorkItem _fromWorkItem;
            // int.MinValue when this AutoTimer is disposed,
            // 0 when no lost tick,
            // 1 when a reentrant tick has been detected (a trampoline is waiting).
            int _onTimeLostFlag;

            /// <summary>
            /// Initializes a new <see cref="AutoTimer"/> for a domain.
            /// <see cref="IsActive"/> is initially true.
            /// </summary>
            /// <param name="domain">The associated domain.</param>
            public AutoTimer( ObservableDomain domain )
            {
                Domain = domain ?? throw new ArgumentNullException( nameof( domain ) );
                _timer = new Timer( _timerCallback, this, Timeout.Infinite, Timeout.Infinite );
                _fromWorkItem = new TrampolineWorkItem( this );
                IsActive = true;
            }

            class TrampolineWorkItem
            {
                public readonly AutoTimer Timer;

                public TrampolineWorkItem( AutoTimer t ) => Timer = t;
            }

            static void OnTime( object state )
            {
                Debug.Assert( state is AutoTimer || state is TrampolineWorkItem );

                bool trampolineRequired = false;
                var ts = state as TrampolineWorkItem;
                var t = ts?.Timer ?? (AutoTimer)state;
                if( !t.IsActive ) return;

                var domain = t.Domain;
                var m = domain.ObtainDomainMonitor( 10, createAutonomousOnTimeout: false );
                if( m != null )
                {
                    // All this stuff is to do exactly what must be done. No more.
                    // This ensures that only ONE trampoline is active at a time and
                    // that if it became useless (because a regular timer call occurred), it is skipped.
                    if( ts == null )
                    {
                        if( Interlocked.CompareExchange( ref t._onTimeLostFlag, 0, 1 ) == 1 )
                        {
                            m.OpenDebug( $"Executing OnTime while a trampoline is pending on Domain '{domain.DomainName}'." );
                        }
                        else
                        {
                            if( t._onTimeLostFlag < 0 )
                            {
                                m.Debug( $"Skipped OnTime on disposed timer for '{domain.DomainName}'." );
                                m.Dispose();
                                return;
                            }
                            m.OpenDebug( $"Executing OnTime on Domain '{domain.DomainName}'." );
                        }
                    }
                    else
                    {
                        if( t._onTimeLostFlag <= 0 )
                        {
                            if( t._onTimeLostFlag == 0 )
                            {
                                m.Debug( $"Skipped useless OnTime trampoline on Domain '{domain.DomainName}'." );
                            }
                            else
                            {
                                m.Debug( $"Skipped useless OnTime trampoline on dispose Domain '{domain.DomainName}'." );
                            }
                            m.Dispose();
                            return;
                        }
                        m.OpenDebug( $"Executing trampoline OnTime on Domain '{domain.DomainName}'." );
                    }
                    int eventRaisedCount = domain.TimeManager._totalEventRaised;
                    t.OnDueTimeAsync( m ).ContinueWith( r =>
                    {
                        // On success, unhandled exception or cancellation, we do nothing.
                        // We only handle one case: if the write lock obtention failed we need the trampoline
                        // to retry asap.
                        if( r.IsFaulted ) m.Fatal( "Unhandled exception from AutoTimer.OnDueTimeAsync.", r.Exception );
                        else
                        {
                            if( r.IsCanceled ) m.Warn( "Async operation canceled." );
                            else if( r.Result.Item1 == TransactionResult.Empty )
                            {
                                // Failed to obtain the write lock.
                                trampolineRequired = true;
                            }
                        }
                        m.Dispose();
                        if( domain.TimeManager._totalEventRaised != eventRaisedCount ) t.RaiseWait();
                    }, TaskContinuationOptions.ExecuteSynchronously );
                }
                else trampolineRequired = true;

                // If we need the trampoline, we only need it if we are currently in 'the' trampoline call or there is no
                // active trampoline.
                if( trampolineRequired && (ts != null || Interlocked.CompareExchange( ref t._onTimeLostFlag, 1, 0 ) == 0) )
                {
                    ThreadPool.QueueUserWorkItem( _waitCallback, t._fromWorkItem );
                }
            }

            /// <summary>
            /// Gets or sets whether the actual system timer is running or not.
            /// This is the ultimate flag that can suspend totally timed events raising.
            /// Setting it to true has no direct effect. The internal timer will be activated at
            /// the start of the next domain Modify call.
            /// </summary>
            public bool IsActive { get; set; }

            /// <summary>
            /// Waits until the next call to <see cref="OnDueTimeAsync(IActivityMonitor)"/> from the internal timer finished.
            /// </summary>
            /// <param name="millisecondsTimeout">The number of milliseconds to wait before giving up.</param>
            /// <returns>
            /// True if a call to <see cref="OnDueTimeAsync(IActivityMonitor)"/> from the internal timer finished before the timeout.
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
            /// Simply calls <see cref="ObservableDomain.ModifyNoThrowAsync"/> with the shared <see cref="ObservableDomain.ObtainDomainMonitor(int, bool)"/>, null actions
            /// and 10 ms timeout: pending timed events are handled if any and if there is no current transaction: <see cref="TransactionResult.Empty"/> is
            /// returned if the write lock failed to be obtained.
            /// </summary>
            protected virtual Task<(TransactionResult, Exception)> OnDueTimeAsync( IActivityMonitor m ) => Domain.ModifyNoThrowAsync( m, null, 10 );

            /// <summary>
            /// Must do whatever is needed to call back this <see cref="OnDueTimeAsync(IActivityMonitor)"/> at <paramref name="nextDueTimeUtc"/> (or
            /// right after but not before!).
            /// This is called while the domain's write lock is held.
            /// </summary>
            /// <param name="monitor">The monitor to use.</param>
            /// <param name="nextDueTimeUtc">
            /// The expected callback time. <see cref="Util.UtcMinValue"/> or <see cref="Util.UtcMaxValue"/> pauses the timer.
            /// </param>
            public virtual void SetNextDueTimeUtc( IActivityMonitor monitor, DateTime nextDueTimeUtc )
            {
                if( IsDisposed ) throw new ObjectDisposedException( ToString() );
                if( monitor == null ) throw new ArgumentNullException( nameof( monitor ) );
                if( nextDueTimeUtc == Util.UtcMinValue || nextDueTimeUtc == Util.UtcMaxValue || !IsActive )
                {
                    _timer.Change( Timeout.Infinite, Timeout.Infinite );
                    monitor.Debug( $"Timer paused ({_timer.GetHashCode()})." );
                }
                else
                {
                    var delta = nextDueTimeUtc - DateTime.UtcNow;
                    var ms = (int)Math.Ceiling( delta.TotalMilliseconds );
                    if( ms <= 0 ) ms = 0;
                    if( !_timer.Change( ms, Timeout.Infinite ) )
                    {
                        var msg = $"Timer.Change({ms}) failed.";
                        monitor.Warn( msg );
                        _timer.Change( Timeout.Infinite, Timeout.Infinite );
                        if( !_timer.Change( ms, Timeout.Infinite ) )
                        {
                            monitor.Fatal( msg );
                            return;
                        }
                    }
                    monitor.Debug( $"Timer set in {ms} ms ({_timer.GetHashCode()})." );
                }
            }

            /// <summary>
            /// Disposed the internal <see cref="System.Threading.Timer"/> object.
            /// This is automatically called by <see cref="ObservableDomain.Dispose()"/> on the <see cref="TimeManager.Timer"/>:
            /// this must be called only when explicit AutoTimer are created/assigned.
            /// </summary>
            public void Dispose()
            {
                if( _onTimeLostFlag >= 0 )
                {
                    _onTimeLostFlag = int.MinValue;
                    using( var waiter = new AutoResetEvent( false ) )
                    {
                        _timer.Dispose( waiter );
                        waiter.WaitOne();
                    }
                }
            }
        }
    }
}

