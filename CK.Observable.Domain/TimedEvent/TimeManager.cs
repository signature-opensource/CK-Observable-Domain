using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Observable
{
    /// <summary>
    /// Timer management implements the support for <see cref="ObservableTimedEventBase"/>.
    /// </summary>
    public class TimeManager
    {
        readonly ObservableDomain _domain;
        readonly Timer _timer;
        int _activeCount;
        ObservableTimedEventBase[] _activeEvents;
        // Tracking is basic: any change are tracked with a simple hash set.
        readonly HashSet<ObservableTimedEventBase> _changed;
        ObservableTimedEventBase _first;
        ObservableTimedEventBase _last;

        internal TimeManager( ObservableDomain domain )
        {
            _domain = domain;
            _activeEvents = new ObservableTimedEventBase[16];
            _changed = new HashSet<ObservableTimedEventBase>();
            _timer = new Timer( new TimerCallback( OnTime ), this, Timeout.Infinite, Timeout.Infinite );
        }

        /// <summary>
        /// Gets or sets whether exceptions raised by <see cref="ObservableTimedEventBase.Elapsed"/> callbacks
        /// are emitted as <see cref="Core.LogLevel.Warn"/> logs.
        /// <para>
        /// By default, exceptions stops the current transaction just like any uncaught exception during <see cref="ObservableDomain.Modify(IActivityMonitor, Action, int)"/>
        /// execution.
        /// </para>
        /// <para>
        /// Setting this to true should be done under rare scenario: it is better to consider such errors as critical ones.
        /// </para>
        /// </summary>
        public bool IgnoreTimedEventException { get; set; }

        /// <summary>
        /// Extension point to bypass the default internal implementation that can be set on <see cref="ExternalTimer"/>.
        /// </summary>
        public interface ITimer
        {
            /// <summary>
            /// Must do whatever is needed to call back this <see cref="FromExternalTimerModifyAsync(IActivityMonitor)"/> or
            /// <see cref="FromExternalTimerModifyAsync(Func{IActivityMonitor, Task}, Func{IActivityMonitor, TransactionResult, Exception, Task})"/> methods
            /// at <paramref name="nextDueTimeUtc"/>.
            /// This is called while the write lock is held: this is guaranteed to be serialized (by <see cref="ObservableDomain"/>).
            /// </summary>
            /// <param name="monitor">The monitor to use.</param>
            /// <param name="domain">The domain that must be called back.</param>
            /// <param name="nextDueTimeUtc">The expected callback time.</param>
            void SetNextDueTimeUtc( IActivityMonitor monitor, ObservableDomain domain, DateTime nextDueTimeUtc );
        }

        /// <summary>
        /// Gets or sets external timer must be used to call <see cref="FromExternalTimerModifyAsync(IActivityMonitor)"/> or
        /// <see cref="FromExternalTimerModifyAsync(Func{IActivityMonitor, Task}, Func{IActivityMonitor, TransactionResult, Exception, Task})"/> methods.
        /// </summary>
        public ITimer ExternalTimer { get; set; }

        internal void SetNextDueTimeUtc( DateTime nextDueTimeUtc )
        {
            var external = ExternalTimer;
            if( external != null ) external.SetNextDueTimeUtc( _domain.Monitor, _domain, nextDueTimeUtc );
            else
            {
                var delta = nextDueTimeUtc - DateTime.UtcNow;
                var ms = delta <= TimeSpan.Zero ? 0 : (long)delta.TotalMilliseconds;
                _timer.Change( ms, Timeout.Infinite );
                _domain.Monitor.Debug( $"Timer set in {ms} MilliSeconds." );
            }
        }

        static void OnTime( object state )
        {
            TimeManager manager = (TimeManager)state;
            manager.FromExternalTimerModifyAsync();
        }

        /// <summary>
        /// Entry point for external timer management. Reentrancies are silently ignored.
        /// This should be called based on the last <see cref="TransactionResult.NextDueTimeUtc"/>.
        /// <para>
        /// By default, this never throws any exceptions: the exception that may be raised by <see cref="IObservableDomainClient.OnTransactionStart(IActivityMonitor,ObservableDomain, DateTime)"/>
        /// or <see cref="TransactionResult.ExecutePostActionsAsync(IActivityMonitor, bool)"/> is logged in the provided monitor and returned by this method.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use. Must not be null.</param>
        /// <returns>The transaction result (that may be <see cref="TransactionResult.Empty"/>) and the potential exception.</returns>
        public Task<(TransactionResult, Exception)> FromExternalTimerModifyAsync( IActivityMonitor monitor ) => _domain.FromTimerAsync( monitor );

        /// <summary>
        /// Entry point for external timer management. Reentrancies are silently ignored. This should be called based on the
        /// last <see cref="TransactionResult.NextDueTimeUtc"/>.
        /// <para>
        /// By default, this never throws any exceptions: all exceptions are logged in the monitor bound to this domain and returned.
        /// This enables timers to avoid the allocation of a new ActivityMonitor: this method exclusively acquires the monitor bound to
        /// this domain and the two <paramref name="beforeTransaction"/> and <paramref name="afterTransaction"/> hooks can use it.
        /// </para>
        /// <para>
        /// This also handles reentrancy since this methods acts as an exclusive lock on the domain monitor.
        /// </para>
        /// </summary>
        /// <param name="beforeTransaction">Optional hook that is called before the operation.</param>
        /// <param name="afterTransaction">
        /// Optional hook that is called after the operation with the transaction result (that may
        /// be <see cref="TransactionResult.Empty"/>) and the <see cref="IObservableDomainClient.OnTransactionStart"/>
        /// or <see cref="TransactionResult.ExecutePostActionsAsync"/> exception if any.</param>
        /// <returns>
        /// The transaction result (that may be <see cref="TransactionResult.Empty"/>) and any exception raised
        /// by <paramref name="beforeTransaction"/> or <paramref name="afterTransaction"/>.
        /// </returns>
        public Task<(TransactionResult, Exception)> FromExternalTimerModifyAsync(
            Func<IActivityMonitor, Task> beforeTransaction = null,
            Func<IActivityMonitor, TransactionResult, Exception, Task> afterTransaction = null ) => _domain.FromTimerHookAsync( beforeTransaction, afterTransaction );


        internal void OnCreated( ObservableTimedEventBase t )
        {
            if( (t.Next = _first) == null ) _last = t;
            _first = t;
            _changed.Add( t );
        }

        internal void OnDisposed( ObservableTimedEventBase t )
        {
            if( _first == t ) _first = t.Next;
            else t.Prev.Next = t.Next;
            if( _last == t ) _last = t.Prev;
            else t.Next.Prev = t.Prev;
            _changed.Add( t );
        }

        internal void OnChanged( ObservableTimedEventBase t ) => _changed.Add( t );

        internal void Clear()
        {
            Array.Clear( _activeEvents, 1, _activeCount );
            _activeCount = 0;
        }

        /// <summary>
        /// Applies all changes: current IsActive property of all changed timed event is handled
        /// and the first next due time is returned so that an external timer can be setup on it.
        /// </summary>
        /// <returns>The first next due time or <see cref="Util.UtcMinValue"/>.</returns>
        internal DateTime ApplyChanges()
        {
            foreach( var ev in _changed )
            {
                if( ev.IsActive )
                {
                    if( ev.ActiveIndex == 0 ) Activate( ev );
                    else OnNextDueTimeUpdated( ev );
                }
                else
                {
                    if( ev.ActiveIndex != 0 ) Deactivate( ev );
                    else OnNextDueTimeUpdated( ev );
                }
            }
            return _activeCount > 0 ? _activeEvents[1].ExpectedDueTimeUtc : Util.UtcMinValue;
        }

        internal bool IsRaising { get; private set; }

        /// <summary>
        /// Raises all timers' event for which <see cref="ObservableTimedEventBase.ExpectedDueTimeUtc"/> is below <paramref name="current"/>
        /// and returns the number of timers that have fired. 
        /// </summary>
        /// <param name="current">The current time.</param>
        /// <returns>The number of timers that have fired.</returns>
        internal int RaiseElapsedEvent( DateTime current )
        {
            IsRaising = true;
            try
            {
                int count = 0;
                while( _activeCount > 0 )
                {
                    var first = _activeEvents[1];
                    if( first.ExpectedDueTimeUtc <= current )
                    {
                        _changed.Remove( first );
                        first.DoRaise( _domain.Monitor, current, IgnoreTimedEventException );
                        if( !_changed.Contains( first ) )
                        {
                            first.OnAfterRaiseUnchanged();
                        }
                        _changed.Remove( first );
                        if( !first.IsActive )
                        {
                            Deactivate( first );
                        }
                        else
                        {
                            if( first.ExpectedDueTimeUtc <= current )
                            {
                                first.ForwardExpectedDueTime( _domain.Monitor, current.AddMilliseconds( 10 ) );
                            }
                            OnNextDueTimeUpdated( first );
                        }
                        ++count;
                    }
                    else break;
                }
                return count;
            }
            finally
            {
                IsRaising = false;
            }
        }

        #region Heap implementation

        void Activate( ObservableTimedEventBase timer )
        {
            if( _activeCount >= _activeEvents.Length - 1 )
            {
                Array.Resize( ref _activeEvents, _activeEvents.Length * 2 );
            }
            _activeCount++;
            _activeEvents[_activeCount] = timer;
            timer.ActiveIndex = _activeCount;
            MoveUp( timer );
        }

        void OnNextDueTimeUpdated( ObservableTimedEventBase timer )
        {
            // MoveDown will be called if timer is the current root.
            int parentIndex = timer.ActiveIndex >> 1;
            if( parentIndex > 0 && IsBefore( timer, _activeEvents[parentIndex] ) )
            {
                MoveUp( timer );
            }
            else
            {
                MoveDown( timer );
            }
        }

        void Deactivate( ObservableTimedEventBase ev )
        {
            Debug.Assert( ev.ActiveIndex > 0 && Array.IndexOf( _activeEvents, ev ) == ev.ActiveIndex );
            // If the event is the last one, we can remove it immediately.
            if( ev.ActiveIndex == _activeCount )
            {
                _activeEvents[_activeCount] = null;
                _activeCount--;
                ev.ActiveIndex = 0;
                return;
            }
            // Swap the event with the last one.
            var last = _activeEvents[_activeCount];
            _activeEvents[ev.ActiveIndex] = last;
            last.ActiveIndex = ev.ActiveIndex;
            _activeEvents[_activeCount] = null;
            _activeCount--;
            ev.ActiveIndex = 0;
            // Now bubble last (which is no longer the actual last) up or down as appropriate.
            OnNextDueTimeUpdated( last );
        }

        void MoveUp( ObservableTimedEventBase timer )
        {
            int parent;
            if( timer.ActiveIndex > 1 )
            {
                parent = timer.ActiveIndex >> 1;
                var parentNode = _activeEvents[parent];
                if( IsBefore( parentNode, timer ) ) return;

                _activeEvents[timer.ActiveIndex] = parentNode;
                parentNode.ActiveIndex = timer.ActiveIndex;
                timer.ActiveIndex = parent;
            }
            else return;
            while( parent > 1 )
            {
                parent >>= 1;
                var parentNode = _activeEvents[parent];
                if( IsBefore( parentNode, timer ) ) break;
                // Move parent down the heap to make room.
                _activeEvents[timer.ActiveIndex] = parentNode;
                parentNode.ActiveIndex = timer.ActiveIndex;

                timer.ActiveIndex = parent;
            }
            _activeEvents[timer.ActiveIndex] = timer;
        }

        void MoveDown( ObservableTimedEventBase ev )
        {
            int finalActiveIndex = ev.ActiveIndex;
            int childLeftIndex = 2 * finalActiveIndex;

            // If leaf node, we're done
            if( childLeftIndex > _activeCount ) return;

            // Check if the left-child is before the current timer.
            int childRightIndex = childLeftIndex + 1;
            var childLeft = _activeEvents[childLeftIndex];
            if( IsBefore( childLeft, ev ) )
            {
                // Check if there is a right child. If not, swap and finish.
                if( childRightIndex > _activeCount )
                {
                    ev.ActiveIndex = childLeftIndex;
                    childLeft.ActiveIndex = finalActiveIndex;
                    _activeEvents[finalActiveIndex] = childLeft;
                    _activeEvents[childLeftIndex] = ev;
                    return;
                }
                // Check if the left-child is before the right-child.
                var childRight = _activeEvents[childRightIndex];
                if( IsBefore( childLeft, childRight ) )
                {
                    // Left is before: move it up and continue.
                    childLeft.ActiveIndex = finalActiveIndex;
                    _activeEvents[finalActiveIndex] = childLeft;
                    finalActiveIndex = childLeftIndex;
                }
                else
                {
                    // Right is even more before: move it up and continue.
                    childRight.ActiveIndex = finalActiveIndex;
                    _activeEvents[finalActiveIndex] = childRight;
                    finalActiveIndex = childRightIndex;
                }
            }
            // Not swapping with left-child, does right-child exist?
            else if( childRightIndex > _activeCount )
            {
                return;
            }
            else
            {
                // Check if the right-child is higher-priority than the current node
                var childRight = _activeEvents[childRightIndex];
                if( IsBefore( childRight, ev ) )
                {
                    childRight.ActiveIndex = finalActiveIndex;
                    _activeEvents[finalActiveIndex] = childRight;
                    finalActiveIndex = childRightIndex;
                }
                // Neither child is higher-priority than current, so finish and stop.
                else
                {
                    return;
                }
            }

            while( true )
            {
                childLeftIndex = 2 * finalActiveIndex;

                // If leaf node, we're done.
                if( childLeftIndex > _activeCount )
                {
                    ev.ActiveIndex = finalActiveIndex;
                    _activeEvents[finalActiveIndex] = ev;
                    break;
                }

                // Check if the left-child is before than the current timer.
                childRightIndex = childLeftIndex + 1;
                childLeft = _activeEvents[childLeftIndex];
                if( IsBefore( childLeft, ev ) )
                {
                    // Check if there is a right child. If not, swap and finish.
                    if( childRightIndex > _activeCount )
                    {
                        ev.ActiveIndex = childLeftIndex;
                        childLeft.ActiveIndex = finalActiveIndex;
                        _activeEvents[finalActiveIndex] = childLeft;
                        _activeEvents[childLeftIndex] = ev;
                        break;
                    }
                    // Check if the left-child is before than the right-child.
                    var childRight = _activeEvents[childRightIndex];
                    if( IsBefore( childLeft, childRight ) )
                    {
                        // Left is before: move it up and continue.
                        childLeft.ActiveIndex = finalActiveIndex;
                        _activeEvents[finalActiveIndex] = childLeft;
                        finalActiveIndex = childLeftIndex;
                    }
                    else
                    {
                        // Right is even more before: move it up and continue.
                        childRight.ActiveIndex = finalActiveIndex;
                        _activeEvents[finalActiveIndex] = childRight;
                        finalActiveIndex = childRightIndex;
                    }
                }
                // Not swapping with left-child, does right-child exist?
                else if( childRightIndex > _activeCount )
                {
                    ev.ActiveIndex = finalActiveIndex;
                    _activeEvents[finalActiveIndex] = ev;
                    break;
                }
                else
                {
                    // Check if the right-child is before than the current timer.
                    var childRight = _activeEvents[childRightIndex];
                    if( IsBefore( childRight, ev ) )
                    {
                        childRight.ActiveIndex = finalActiveIndex;
                        _activeEvents[finalActiveIndex] = childRight;
                        finalActiveIndex = childRightIndex;
                    }
                    // Neither child is before than current, so finish and stop.
                    else
                    {
                        ev.ActiveIndex = finalActiveIndex;
                        _activeEvents[finalActiveIndex] = ev;
                        break;
                    }
                }
            }
        }

        bool IsBefore( ObservableTimedEventBase e1, ObservableTimedEventBase e2 ) => e1.ExpectedDueTimeUtc < e2.ExpectedDueTimeUtc;

        #endregion
    }
}

