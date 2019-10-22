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
    /// Internal class.
    /// Min heap code is taken from https://github.com/BlueRaja/High-Speed-Priority-Queue-for-C-Sharp/blob/master/Priority%20Queue/FastPriorityQueue.cs
    /// </summary>
    class TimerHost
    {
        readonly ObservableDomain _domain;
        int _activeCount;
        ObservableTimedEventBase[] _activeEvents;
        // Tracking is basic: any change are tracked with a simple hash set.
        readonly HashSet<ObservableTimedEventBase> _changed;
        ObservableTimedEventBase _first;
        ObservableTimedEventBase _last;

        public TimerHost( ObservableDomain domain )
        {
            _domain = domain;
            _activeEvents = new ObservableTimedEventBase[16];
            _changed = new HashSet<ObservableTimedEventBase>();
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

        public void Clear()
        {
            Array.Clear( _activeEvents, 1, _activeCount );
            _activeCount = 0;
        }

        /// <summary>
        /// Applies all changes: current IsActive property of all changed timed event is handled
        /// and the first next due time is returned so that an external timer can be setup on it.
        /// </summary>
        /// <returns>The first next due time or <see cref="Util.UtcMinValue"/>.</returns>
        public DateTime ApplyChanges()
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

        internal bool IsRaising { get; private set; }

        /// <summary>
        /// Raises all timers' event for which <see cref="ObservableTimedEventBase.ExpectedDueTimeUtc"/> is below <paramref name="current"/>
        /// and returns the number of timers that have fired. 
        /// </summary>
        /// <param name="current">The current time.</param>
        /// <param name="throwException">False to silently ignore any handler exception (only log them).</param>
        /// <returns>The number of timers that have fired.</returns>
        public int RaiseElapsedEvent( DateTime current, bool throwException )
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
                        first.DoRaise( _domain.Monitor, current, throwException );
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

    }
}

