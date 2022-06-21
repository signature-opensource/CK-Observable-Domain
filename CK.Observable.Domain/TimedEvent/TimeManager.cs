using CK.BinarySerialization;
using CK.Core;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace CK.Observable
{
    /// <summary>
    /// Timer management implements the support for <see cref="ObservableTimedEventBase"/>.
    /// </summary>
    public sealed partial class TimeManager : ITimeManager
    {
        // The min heap is stored in an array.
        // The ObservableTimedEventBase.ActiveIndex is the index in this heap: 0 index is not used.
        ObservableTimedEventBase?[] _activeEvents;
        int _activeCount;
        int _count;
        int _timerCount;
        int _totalEventRaised;
        // Tracking is basic: any change is tracked with a simple hash set.
        readonly HashSet<ObservableTimedEventBase> _changed;
        readonly TimedEventCollection _exposedTimedEvents;
        readonly TimerCollection _exposedTimers;
        readonly ReminderCollection _exposedReminders;
        ObservableReminder? _firstFreeReminder;

        ObservableTimedEventBase? _first;
        ObservableTimedEventBase? _last;
        AutoTimer _autoTimer;

        internal TimeManager( ObservableDomain domain )
        {
            Domain = domain;
            _activeEvents = new ObservableTimedEventBase?[16];
            _changed = new HashSet<ObservableTimedEventBase>();
            _autoTimer = new AutoTimer( Domain );
            _exposedTimedEvents = new TimedEventCollection( this );
            _exposedTimers = new TimerCollection( this );
            _exposedReminders = new ReminderCollection( this );
        }

        /// <summary>
        /// Exposes the domain.
        /// </summary>
        internal readonly ObservableDomain Domain;

        /// <summary>
        /// Gets or sets whether exceptions raised by <see cref="ObservableTimedEventBase{T}.Elapsed"/> callbacks
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
        /// Gets or sets the <see cref="AutoTimer"/> that must be used to ensure that <see cref="ObservableTimedEventBase{T}.Elapsed"/> events
        /// are raised even when no activity occur on the domain.
        /// </summary>
        public AutoTimer Timer
        {
            get => _autoTimer;
            set
            {
                if( value == null ) throw new ArgumentNullException( nameof( Timer ) );
                if( _autoTimer != value )
                {
                    _autoTimer = value;
                }
            }
        }

        /// <inheritdoc />
        public bool IsRunning { get; private set; }

        /// <inheritdoc />
        public void Start()
        {
            if( !IsRunning ) Domain.StartOrStopTimeManager( true );
        }

        /// <inheritdoc />
        public void Stop()
        {
            if( IsRunning ) Domain.StartOrStopTimeManager( false );
        }

        /// <inheritdoc/>
        public DateTime NextActiveTime => _autoTimer.NextDueTime;

        class TimerCollection : IReadOnlyCollection<ObservableTimer>
        {
            readonly TimeManager _timeManager;

            public TimerCollection( TimeManager m ) => _timeManager = m;

            public int Count => _timeManager._timerCount;

            public IEnumerator<ObservableTimer> GetEnumerator()
            {
                var o = _timeManager._first;
                while( o != null )
                {
                    if( o is ObservableTimer t ) yield return t;
                    o = o.Next;
                }
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        class ReminderCollection : IReadOnlyCollection<ObservableReminder>
        {
            readonly TimeManager _timeManager;

            public ReminderCollection( TimeManager m ) => _timeManager = m;

            public int Count => _timeManager._count - _timeManager._timerCount;

            public IEnumerator<ObservableReminder> GetEnumerator()
            {
                var o = _timeManager._first;
                while( o != null )
                {
                    if( o is ObservableReminder r ) yield return r;
                    o = o.Next;
                }
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        class TimedEventCollection : IReadOnlyCollection<ObservableTimedEventBase>
        {
            readonly TimeManager _timeManager;

            public TimedEventCollection( TimeManager m ) => _timeManager = m;

            public int Count => _timeManager._count;

            public IEnumerator<ObservableTimedEventBase> GetEnumerator()
            {
                var o = _timeManager._first;
                while( o != null )
                {
                    yield return o;
                    o = o.Next;
                }
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        /// <inheritdoc/>
        public IReadOnlyCollection<ObservableTimer> Timers => _exposedTimers;

        /// <inheritdoc/>
        public IReadOnlyCollection<ObservableReminder> Reminders => _exposedReminders;

        /// <inheritdoc/>
        public IReadOnlyCollection<ObservableTimedEventBase> AllObservableTimedEvents => _exposedTimedEvents;

        /// <inheritdoc />
        public void Remind( DateTime dueTimeUtc, SafeEventHandler<ObservableReminderEventArgs> callback, SuspendableClock? clock, object? tag )
        {
            // Utc is checked by the DueTimeUtc setter below.
            if( dueTimeUtc == Util.UtcMinValue || dueTimeUtc == Util.UtcMaxValue ) throw new ArgumentException( nameof( dueTimeUtc ), $"Must be a Utc DateTime, not UtcMinValue nor UtcMaxValue: {dueTimeUtc.ToString( "o" )}." );
            if( callback == null ) throw new ArgumentNullException( nameof( callback ) );
            var r = GetPooledReminder();
            r.Elapsed += callback;
            r.Tag = tag;
            r.DueTimeUtc = dueTimeUtc;
            r.SuspendableClock = clock;
        }

        ObservableReminder GetPooledReminder()
        {
            var r = _firstFreeReminder;
            if( r == null ) return new ObservableReminder();
            _firstFreeReminder = r.NextFreeReminder;
            return r;
        }

        internal void ReleaseToPool( ObservableReminder r )
        {
            Debug.Assert( r != null );
            r.NextFreeReminder = _firstFreeReminder;
            _firstFreeReminder = r;
            r.SuspendableClock = null;
        }

        internal void TrimPooledReminders( IActivityMonitor monitor, int removePooledReminderCount )
        {
            CheckMinHeapInvariant();

            int count = 0;
            var f = _firstFreeReminder;
            while( f != null && --removePooledReminderCount >= 0 )
            {
                ++count;
                f.ForcePooledDestroy();
                _firstFreeReminder = f = f.NextFreeReminder;
            }

            CheckMinHeapInvariant();

            monitor.Info( $"Removed {count} unused pool reminder." );
        }


        internal void OnCreated( ObservableTimedEventBase t, bool addOnChange )
        {
            Debug.Assert( t is ObservableTimer || t is ObservableReminder );
            Debug.Assert( t.Prev == null && t.Next == null );

            if( (t.Prev = _last) == null ) _first = t;
            else
            {
                Debug.Assert( _last != null );
                _last.Next = t;
            }
            _last = t;
            if( addOnChange ) _changed.Add( t );
            ++_count;
            if( t is ObservableTimer ) ++_timerCount;
        }

        internal void OnPreDisposed( ObservableTimedEventBase t )
        {
            Domain.CheckBeforeDestroy( t );
            if( t.ActiveIndex != 0 ) Deactivate( t );
        }

        internal void OnDestroyed( ObservableTimedEventBase t )
        {
            if( _first == t ) _first = t.Next;
            else
            {
                Debug.Assert( t.Prev != null );
                t.Prev.Next = t.Next;
            }
            if( _last == t ) _last = t.Prev;
            else
            {
                Debug.Assert( t.Next != null );
                t.Next.Prev = t.Prev;
            }
            _changed.Add( t );
            --_count;
            if( t is ObservableTimer ) --_timerCount;
        }

        /// <summary>
        /// Adds the timed event into the set of changed ones.
        /// The change is handled in <see cref="UpdateMinHeap"/> called at the start
        /// and at the end of the ObservableDomain.Modify.
        /// </summary>
        /// <param name="t">The timed event to add.</param>
        internal void OnChanged( ObservableTimedEventBase t ) => _changed.Add( t );

        internal (List<ObservableTimedEventBase>? Lost, int UnusedPoolCount, int PooledReminderCount) Save( IActivityMonitor monitor,
                                                                                                            IBinarySerializer s,
                                                                                                            bool trackLostObjects )
        {
            CheckMinHeapInvariant();
            List<ObservableTimedEventBase>? lostObjects = null;
            s.Writer.WriteNonNegativeSmallInt32( 1 );
            s.Writer.Write( IsRunning );
            s.Writer.WriteNonNegativeSmallInt32( _count );
            ObservableTimedEventBase? f = _first;
            int pooledCount = 0;
            int unusedPoolCount = 0;
            while( f != null )
            {
                Debug.Assert( !f.IsDestroyed, "Disposed Timed event objects are removed from the list." );
                if( s.WriteObject( f ) && trackLostObjects )
                {
                    bool isPooled = f is ObservableReminder r && r.IsPooled;
                    if( isPooled ) ++pooledCount;
                    // If there is at least one handler (destroyed targets have been cleanup) for the timed event,
                    // then it should not lost.
                    if( !f.HasHandlers )
                    {
                        if( isPooled )
                        {
                            // Pooled reminders are not lost, but unused ones are counted
                            // so that when too much unused pooled objects exist, the set
                            // can be trimmed.
                            ++unusedPoolCount;
                        }
                        else
                        {
                            if( lostObjects == null ) lostObjects = new List<ObservableTimedEventBase>();
                            lostObjects.Add( f );
                        }
                    }
                }
                f = f.Next;
            }
            return (lostObjects, unusedPoolCount, pooledCount);
        }

        internal bool Load( IActivityMonitor m, IBinaryDeserializer d )
        {
            int version = d.Reader.ReadNonNegativeSmallInt32();
            bool running = version > 0 ? d.Reader.ReadBoolean() : true;
            int count = d.Reader.ReadNonNegativeSmallInt32();
            while( --count >= 0 )
            {
                var t = d.ReadObject<ObservableTimedEventBase>();
                Debug.Assert( !t.IsDestroyed );
                OnCreated( t, false );
                if( t.ActiveIndex > 0 )
                {
                    EnsureActiveLength( t.ActiveIndex );
                    _activeEvents[t.ActiveIndex] = t;
                    ++_activeCount;
                }
            }
#if DEBUG
            int expectedCount = _count;
            ObservableTimedEventBase? last = null;
            ObservableTimedEventBase? f = _first;
            while( f != null )
            {
                Debug.Assert( --expectedCount >= 0 );
                Debug.Assert( f.Prev == last );
                last = f;
                f = f.Next;
            }
            Debug.Assert( expectedCount == 0 );
            Debug.Assert( _last == last );
#endif
            CheckMinHeapInvariant();
            return running;
        }

        internal void ClearAndStop( IActivityMonitor monitor )
        {
            DoStartOrStop( monitor, false );

            Debug.Assert( _activeEvents[0] == null );
            Array.Clear( _activeEvents, 1, _activeCount );

            _timerCount = _count = _activeCount = 0;
            _first = _last = null;
            _autoTimer.SetNextDueTimeUtc( monitor, Util.UtcMinValue );
            _firstFreeReminder = null;
            _changed.Clear();
        }

        internal void DoStartOrStop( IActivityMonitor monitor, bool start )
        {
            if( start != IsRunning )
            {
                DateTime next = Util.UtcMinValue;
                if( start )
                {
                    UpdateMinHeap();
                    if( _activeCount > 0 ) next = _activeEvents[1]!.ExpectedDueTimeUtc;
                }
                IsRunning = start;
                _autoTimer.SetNextDueTimeUtc( monitor, next );
            }
        }

        [Conditional("DEBUG")]
        void CheckMinHeapInvariant()
        {
            Debug.Assert( AllObservableTimedEvents.Distinct().SequenceEqual( AllObservableTimedEvents ) );
            Debug.Assert( _activeEvents[0] == null );
            int i = 1;
            while( i <= _activeCount )
            {
                Debug.Assert( _activeEvents[i]!.ActiveIndex == i );
                Debug.Assert( _activeEvents[i]!.IsActive );
                ++i;
            }
            while( i < _activeEvents.Length )
            {
                Debug.Assert( _activeEvents[i] == null );
                ++i;
            }
        }

        /// <summary>
        /// Updates the heap based on all timed event that has changed.
        /// The _changed set is processed and cleared.
        /// </summary>
        internal void UpdateMinHeap()
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
                }
            }
            _changed.Clear();
            CheckMinHeapInvariant();
        }

        internal bool IsRaising { get; private set; }

        /// <summary>
        /// Raises all timers' event for which <see cref="ObservableTimedEventBase.ExpectedDueTimeUtc"/> is below <paramref name="current"/>.
        /// </summary>
        /// <param name="monitor">The monitor: should be the Domain.Monitor that has been obtained by the AutoTimer.</param>
        /// <param name="current">The current time.</param>
        /// <param name="checkChanges">True to check timed event next due time.</param>
        internal void RaiseElapsedEvent( IActivityMonitor monitor, DateTime current )
        {
            Debug.Assert( IsRunning );
            UpdateMinHeap();
            IsRaising = true;
            try
            {
                int loopCount = 100;
                int count = 0;
                DateTime nextFire = Util.UtcMinValue;
                again:
                if( _activeCount == 0 )
                {
                    nextFire = Util.UtcMinValue;
                }
                else
                {
                    do
                    {
                        var first = _activeEvents[1];
                        Debug.Assert( first != null );
                        if( first.ExpectedDueTimeUtc > current )
                        {
                            nextFire = first.ExpectedDueTimeUtc;
                            break;
                        }
                        else
                        {
                            _totalEventRaised++;
                            _changed.Remove( first );
                            try
                            {
                                first.DoRaise( monitor, current, !IgnoreTimedEventException );
                            }
                            finally
                            {
                                if( !_changed.Remove( first ) )
                                {
                                    first.OnAfterRaiseUnchanged( current, monitor );
                                }
                                if( !first.IsDestroyed )
                                {
                                    if( !first.IsActive )
                                    {
                                        Deactivate( first );
                                    }
                                    else
                                    {
                                        if( first.ExpectedDueTimeUtc <= current )
                                        {
                                            // 10 ms is a "very minimal" step: it is smaller than the approximate thread time slice (20 ms). 
                                            first.ForwardExpectedDueTime( monitor, current.AddMilliseconds( 10 ) );
                                        }
                                        OnNextDueTimeUpdated( first );
                                    }
                                }
                            }
                            ++count;
                        }
                    }
                    while( _activeCount > 0 );
                }
                if( count != 0 && _changed.Count > 0 )
                {
                    if( --loopCount >= 0 )
                    {
                        UpdateMinHeap();
                        goto again;
                    }
                    monitor.Warn( $"Too many Timer or Reminder update cycles. Possible culprits are: {_changed.Select( c => c.ToString() ).Concatenate()}" );
                }
                _autoTimer.SetNextDueTimeUtc( monitor, nextFire );
            }
            finally
            {
                CheckMinHeapInvariant();
                IsRaising = false;
            }
        }

        #region Heap implementation

        void Activate( ObservableTimedEventBase timer )
        {
            EnsureActiveLength( ++_activeCount );
            _activeEvents[_activeCount] = timer;
            timer.ActiveIndex = _activeCount;
            MoveUp( timer );
        }

        void EnsureActiveLength( int idx )
        {
            while( idx >= _activeEvents.Length )
            {
                Array.Resize( ref _activeEvents, _activeEvents.Length * 2 );
            }
        }

        void OnNextDueTimeUpdated( ObservableTimedEventBase timer )
        {
            // MoveDown will be called if timer is the current root.
            int parentIndex = timer.ActiveIndex >> 1;
            if( parentIndex > 0 && IsBefore( timer, _activeEvents[parentIndex]! ) )
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
            ev.OnDeactivate();
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
            Debug.Assert( last != null );
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
                Debug.Assert( parentNode != null );
                if( IsBefore( parentNode, timer ) )
                {
                    return;
                }
                _activeEvents[timer.ActiveIndex] = parentNode;
                parentNode.ActiveIndex = timer.ActiveIndex;
                timer.ActiveIndex = parent;
            }
            else
            {
                return;
            }
            while( parent > 1 )
            {
                parent >>= 1;
                var parentNode = _activeEvents[parent];
                Debug.Assert( parentNode != null );
                if( IsBefore( parentNode, timer ) )
                {
                    break;
                }
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
            if( childLeftIndex > _activeCount )
            {
                return;
            }
            // Check if the left-child is before the current timer.
            int childRightIndex = childLeftIndex + 1;
            var childLeft = _activeEvents[childLeftIndex];
            Debug.Assert( childLeft != null );
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
                Debug.Assert( childRight != null );
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
                Debug.Assert( childRight != null );
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
                Debug.Assert( childLeft != null );
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
                    Debug.Assert( childRight != null );
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
                    Debug.Assert( childRight != null );
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

