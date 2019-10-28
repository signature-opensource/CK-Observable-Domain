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
    public sealed class TimeManager
    {
        readonly ObservableDomain _domain;
        int _activeCount;
        ObservableTimedEventBase[] _activeEvents;
        // Tracking is basic: any change are tracked with a simple hash set.
        readonly HashSet<ObservableTimedEventBase> _changed;
        ObservableTimedEventBase _first;
        ObservableTimedEventBase _last;
        AutoTimer _current;

        /// <summary>
        /// Default implementation that is available on <see cref="CurrentTimer"/> and can be specialized
        /// and replaced as needed.
        /// </summary>
        public class AutoTimer : IDisposable
        {
            readonly Timer _timer;
            int _reentrantGuard;
            static readonly TimerCallback _callback = new TimerCallback( OnTime );

            public AutoTimer( ObservableDomain domain )
            {
                Domain = domain ?? throw new ArgumentNullException( nameof( domain ) );
                _timer = new Timer( _callback, this, Timeout.Infinite, Timeout.Infinite );
            }

            static void OnTime( object state )
            {
                Debug.Assert( state is AutoTimer );
                var t = (AutoTimer)state;
                if( Interlocked.CompareExchange( ref t._reentrantGuard, 1, 0 ) == 0 )
                {
                    var m = t.Domain.ObtainDomainMonitor( 0, createAutonomousOnTimeout: false );
                    if( m != null )
                    {
                        t.OnDueTimeAsync( m ).ContinueWith( _ =>
                        {
                            m.Dispose();
                            Interlocked.Decrement( ref t._reentrantGuard );
                        } );
                    }
                }
            }

            /// <summary>
            /// Gets whether this AutoTimer has been <see cref="Dispose"/>d.
            /// </summary>
            public bool IsDisposed => _reentrantGuard < 0;

            /// <summary>
            /// Gets the domain to which this timer is bound.
            /// Note that this <see cref="AutoTimer"/> may not be the <see cref="TimeManager.CurrentTimer"/> of the domain.
            /// </summary>
            public ObservableDomain Domain { get; }

            /// <summary>
            /// Simply calls <see cref="ObservableDomain.SafeModifyAsync"/> the shared <see cref="ObservableDomain.ObtainDomainMonitor(int)"/>, null actions and 0 timeout:
            /// pending timed events are handled if any and if there is no current transaction.
            /// </summary>
            protected virtual Task OnDueTimeAsync( IActivityMonitor m ) => Domain.SafeModifyAsync( m, null, 0 );

            /// <summary>
            /// Must do whatever is needed to call back this <see cref="FromExternalTimerModifyAsync(IActivityMonitor)"/> or
            /// <see cref="FromExternalTimerModifyAsync(Func{IActivityMonitor, Task}, Func{IActivityMonitor, TransactionResult, Exception, Task})"/> methods
            /// at <paramref name="nextDueTimeUtc"/>.
            /// This is called while the domain's write lock is held.
            /// </summary>
            /// <param name="monitor">The monitor to use.</param>
            /// <param name="nextDueTimeUtc">The expected callback time.</param>
            public virtual void SetNextDueTimeUtc( IActivityMonitor monitor, DateTime nextDueTimeUtc )
            {
                if( IsDisposed ) throw new ObjectDisposedException( GetType().FullName );
                if( monitor == null ) throw new ArgumentNullException( nameof( monitor ) );
                var delta = nextDueTimeUtc - DateTime.UtcNow;
                var ms = delta <= TimeSpan.Zero ? 0 : (long)delta.TotalMilliseconds;
                _timer.Change( ms, Timeout.Infinite );
                monitor.Debug( $"Timer set in {ms} MilliSeconds." );
            }

            /// <summary>
            /// Disposed the internal <see cref="Timer"/> object.
            /// This is automatically called by <see cref="ObservableDomain.Dispose()"/> on the <see cref="TimeManager.CurrentTimer"/>:
            /// this must be called only when explicit AutoTimer are created/assigned.
            /// </summary>
            public void Dispose()
            {
                if( _reentrantGuard >= 0 )
                {
                    _reentrantGuard = int.MinValue;
                    _timer.Dispose();
                }
            }
        }

        internal TimeManager( ObservableDomain domain )
        {
            _domain = domain;
            _activeEvents = new ObservableTimedEventBase[16];
            _changed = new HashSet<ObservableTimedEventBase>();
            _current = new AutoTimer( _domain );
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
        /// Gets or sets the <see cref="AutoTimer"/> that must be used to ensure that <see cref="ObservableTimedEventBase.Elapsed"/> events
        /// are raised even when no activity occur on the domain.
        /// </summary>
        public AutoTimer CurrentTimer
        {
            get => _current;
            set
            {
                if( value == null ) throw new ArgumentNullException( nameof( CurrentTimer ) );
                _current = value;
            }
        }

        internal void SetNextDueTimeUtc( IActivityMonitor m, DateTime nextDueTimeUtc )
        {
            try
            {
                _current.SetNextDueTimeUtc( m, nextDueTimeUtc );
            }
            catch( Exception ex )
            {
                m.Error( ex );
            }
        }

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
                        first.DoRaise( _domain.CurrentMonitor, current, IgnoreTimedEventException );
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
                                first.ForwardExpectedDueTime( _domain.CurrentMonitor, current.AddMilliseconds( 10 ) );
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

