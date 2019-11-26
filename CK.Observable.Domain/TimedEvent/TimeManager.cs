using CK.Core;
using System;
using System.Collections.Concurrent;
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
        int _activeCount;
        int _count;
        ObservableTimedEventBase[] _activeEvents;
        // Tracking is basic: any change are tracked with a simple hash set.
        readonly HashSet<ObservableTimedEventBase> _changed;
        readonly List<ObservableTimedEventBase> _changedCleanupBuffer;
        ObservableTimedEventBase _first;
        ObservableTimedEventBase _last;
        AutoTimer _current;
        DateTime _currentNext;

        /// <summary>
        /// Default implementation that is available on <see cref="CurrentTimer"/> and can be specialized
        /// and replaced as needed.
        /// </summary>
        public class AutoTimer : IDisposable
        {
            static readonly TimerCallback _timerCallback = new TimerCallback( OnTime );
            static readonly WaitCallback _waitCallback = new WaitCallback( OnTime );

            readonly Timer _timer;
            // This is the shell for the trampoline state and the object used
            // for the RaiseWait().
            readonly TrampolineWorkItem _fromWorkItem;
            int _onTimeLostFlag;

            public AutoTimer( ObservableDomain domain )
            {
                Domain = domain ?? throw new ArgumentNullException( nameof( domain ) );
                _timer = new Timer( _timerCallback, this, Timeout.Infinite, Timeout.Infinite );
                _fromWorkItem = new TrampolineWorkItem( this );
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

                var m = t.Domain.ObtainDomainMonitor( 10, createAutonomousOnTimeout: false );
                if( m != null )
                {
                    // All this stuff is to do exactly what must be done. No more.
                    // This ensures that only ONE trampoline is active at a time and
                    // that if it became useless (because a regular timer call occurred), it is skipped.
                    if( ts == null )
                    {
                        if( Interlocked.CompareExchange( ref t._onTimeLostFlag, 0, 1 ) == 1 )
                        {
                            m.OpenDebug( $"Executing OnTime while a trampoline is pending on Domain '{t.Domain.DomainName}'." );
                        }
                        else
                        {
                            if( t._onTimeLostFlag < 0 )
                            {
                                m.Debug( $"Skipped OnTime on disposed timer for '{t.Domain.DomainName}'." );
                                m.Dispose();
                                return;
                            }
                            m.OpenDebug( $"Executing OnTime on Domain '{t.Domain.DomainName}'." );
                        }
                    }
                    else
                    {
                        if( t._onTimeLostFlag <= 0 )
                        {
                            if( t._onTimeLostFlag == 0 )
                            {
                                m.Debug( $"Skipped useless OnTime trampoline on Domain '{t.Domain.DomainName}'." );
                            }
                            else
                            {
                                m.Debug( $"Skipped useless OnTime trampoline on dispose Domain '{t.Domain.DomainName}'." );
                            }
                            m.Dispose();
                            return;
                        }
                        m.OpenDebug( $"Executing trampoline OnTime on Domain '{t.Domain.DomainName}'." );
                    }
                    t.OnDueTimeAsync( m ).ContinueWith( r =>
                    {
                        // On success, unhandled exception or cancellation, we do nothing.
                        // We only handle one case: if the write lock obtetion failed we need the trampoline
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
                        t.RaiseWait();
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
            /// Note that this <see cref="AutoTimer"/> may not be the <see cref="TimeManager.CurrentTimer"/> of the domain.
            /// </summary>
            public ObservableDomain Domain { get; }

            /// <summary>
            /// Simply calls <see cref="ObservableDomain.SafeModifyAsync"/> with the shared <see cref="ObservableDomain.ObtainDomainMonitor(int)"/>, null actions
            /// and 10 ms timeout: pending timed events are handled if any and if there is no current transaction: <see cref="TransactionResult.Empty"/> is
            /// returned if the write lock failed to be obtained.
            /// </summary>
            protected virtual Task<(TransactionResult, Exception)> OnDueTimeAsync( IActivityMonitor m ) => Domain.SafeModifyAsync( m, null, 10 );

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
                if( nextDueTimeUtc == Util.UtcMinValue || nextDueTimeUtc == Util.UtcMaxValue )
                {
                    _timer.Change( Timeout.Infinite, Timeout.Infinite );
                    monitor.Debug( $"Timer paused ({_timer.GetHashCode()})." );
                }
                else
                {
                    var delta = nextDueTimeUtc - DateTime.UtcNow;
                    var ms = (long)Math.Ceiling( delta.TotalMilliseconds );
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
            /// Disposed the internal <see cref="Timer"/> object.
            /// This is automatically called by <see cref="ObservableDomain.Dispose()"/> on the <see cref="TimeManager.CurrentTimer"/>:
            /// this must be called only when explicit AutoTimer are created/assigned.
            /// </summary>
            public void Dispose()
            {
                if( _onTimeLostFlag >= 0 )
                {
                    _onTimeLostFlag = int.MinValue;
                    using( var waiter = new AutoResetEvent(false) )
                    {
                        _timer.Dispose( waiter );
                        waiter.WaitOne();
                    }
                }
            }
        }

        internal TimeManager( ObservableDomain domain )
        {
            Domain = domain;
            _activeEvents = new ObservableTimedEventBase[16];
            _changed = new HashSet<ObservableTimedEventBase>();
            _changedCleanupBuffer = new List<ObservableTimedEventBase>();
            _current = new AutoTimer( Domain );
            _currentNext = Util.UtcMinValue;
        }

        /// <summary>
        /// Exposes the domain.
        /// </summary>
        internal readonly ObservableDomain Domain;

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
                if( _current != value )
                {
                    _currentNext = Util.UtcMinValue;
                    _current = value;
                }
            }
        }

        /// <summary>
        /// Gets the set of <see cref="ObservableTimer"/>.
        /// </summary>
        public IEnumerable<ObservableTimer> Timers => AllObservableTimedEvents.OfType<ObservableTimer>();

        /// <summary>
        /// Gets the set of <see cref="ObservableReminder"/>.
        /// </summary>
        public IEnumerable<ObservableReminder> Reminders => AllObservableTimedEvents.OfType<ObservableReminder>();

        /// <summary>
        /// Gets the set of all the <see cref="ObservableTimedEventBase"/>.
        /// </summary>
        public IEnumerable<ObservableTimedEventBase> AllObservableTimedEvents
        {
            get
            {
                var o = _first;
                while( o != null )
                {
                    yield return o;
                    o = o.Next;
                }
            }
        }

        ObservableReminder _firstFreeReminder;

        ObservableReminder GetPooledReminder()
        {
            var r = _firstFreeReminder;
            if( _firstFreeReminder == null ) return new ObservableReminder();
            _firstFreeReminder = r.NextFreeReminder;
            return r;
        }

        internal void ReleaseToPool( ObservableReminder r )
        {
            Debug.Assert( r != null );
            r.NextFreeReminder = _firstFreeReminder;
            _firstFreeReminder = r;
        }

        internal void Remind( DateTime dueTimeUtc, SafeEventHandler<ObservableReminderEventArgs> callback, object tag )
        {
            // Utc is checked by the DueTimeUtc setter below.
            if( dueTimeUtc == Util.UtcMinValue || dueTimeUtc == Util.UtcMaxValue ) throw new ArgumentException( nameof( dueTimeUtc ), $"Must be a Utc DateTime, not UtcMinValue nor UtcMaxValue: {dueTimeUtc.ToString("o")}." );
            if( callback == null ) throw new ArgumentNullException( nameof( callback ) );
            var r = GetPooledReminder();
            r.Elapsed += callback;
            r.Tag = tag;
            r.DueTimeUtc = dueTimeUtc;
        }

        internal void SetNextDueTimeUtc( IActivityMonitor m, DateTime nextDueTimeUtc )
        {
            if( _currentNext != nextDueTimeUtc )
            {
                try
                {
                    _current.SetNextDueTimeUtc( m, nextDueTimeUtc );
                    _currentNext = nextDueTimeUtc;
                }
                catch( Exception ex )
                {
                    m.Error( ex );
                }
            }
        }

        internal void OnCreated( ObservableTimedEventBase t )
        {
            if( (t.Next = _first) == null ) _last = t;
            _first = t;
            _changed.Add( t );
            ++_count;
        }

        internal void OnPreDisposed( ObservableTimedEventBase t )
        {
            Domain.CheckBeforeDispose( t );
            if( t.ActiveIndex != 0 ) Deactivate( t ); 
        }

        internal void OnDisposed( ObservableTimedEventBase t )
        {
            if( _first == t ) _first = t.Next;
            else t.Prev.Next = t.Next;
            if( _last == t ) _last = t.Prev;
            else t.Next.Prev = t.Prev;
            _changed.Add( t );
            --_count;
        }

        internal void OnChanged( ObservableTimedEventBase t ) => _changed.Add( t );

        internal void Save( IActivityMonitor m, BinarySerializer w )
        {
            w.WriteNonNegativeSmallInt32( 0 );
            w.WriteNonNegativeSmallInt32( _count );
            var f = _first;
            while( f != null )
            {
                w.WriteObject( f );
                f = f.Next;
            }
        }

        internal void Load( IActivityMonitor m, BinaryDeserializer r )
        {
            Debug.Assert( _count == 0 && _first == null && _last == null && _activeCount == 0 );
            int version = r.ReadNonNegativeSmallInt32();
            int count = r.ReadNonNegativeSmallInt32();
            while( --count >= 0 )
            {
                r.ReadObject();
            }
        }

        internal void OnLoadedActive( ObservableTimedEventBase t )
        {
            Debug.Assert( t.ActiveIndex > 0 );
            EnsureActiveLength( t.ActiveIndex );
            _activeEvents[t.ActiveIndex] = t;
            ++_activeCount;
        }

        internal void Clear( IActivityMonitor monitor )
        {
            Debug.Assert( _activeEvents[0] == null );
            Array.Clear( _activeEvents, 1, _activeCount );
            _count = _activeCount = 0;
            _first = _last = null;
            _current.SetNextDueTimeUtc( monitor, Util.UtcMinValue );
            _firstFreeReminder = null;
        }

        /// <summary>
        /// Applies all changes: current IsActive property of all changed timed event is handled
        /// and the first next due time is returned so that an external timer can be setup on it.
        /// </summary>
        /// <returns>The first next due time or <see cref="Util.UtcMinValue"/>.</returns>
        internal DateTime ApplyChanges()
        {
            DoApplyChanges();
            return _activeCount > 0 ? _activeEvents[1].ExpectedDueTimeUtc : Util.UtcMinValue;
        }

        void DoApplyChanges()
        {
            Debug.Assert( _changedCleanupBuffer.Count == 0 );
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
                    _changedCleanupBuffer.Add( ev );
                }
            }
            if( _changedCleanupBuffer.Count > 0 )
            {
                foreach( var rem in _changedCleanupBuffer )
                {
                    _changed.Remove( rem );
                }
                _changedCleanupBuffer.Clear();
            }
        }

        internal bool IsRaising { get; private set; }

        /// <summary>
        /// Raises all timers' event for which <see cref="ObservableTimedEventBase.ExpectedDueTimeUtc"/> is below <paramref name="current"/>
        /// and returns the number of timers that have fired. 
        /// </summary>
        /// <param name="current">The current time.</param>
        /// <param name="checkChanges">True to check timed event next due time.</param>
        /// <returns>The number of timers that have fired.</returns>
        internal int RaiseElapsedEvent( DateTime current, bool checkChanges )
        {
            if( checkChanges ) DoApplyChanges();
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
                        first.DoRaise( Domain.CurrentMonitor, current, !IgnoreTimedEventException );
                        if( !_changed.Contains( first ) )
                        {
                            first.OnAfterRaiseUnchanged( current, Domain.CurrentMonitor );
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
                                // 10 ms is a "very minimal" step: it is smaller than the approximate thread time slice (20 ms). 
                                first.ForwardExpectedDueTime( Domain.CurrentMonitor, current.AddMilliseconds( 10 ) );
                            }
                            OnNextDueTimeUpdated( first );
                        }
                        Domain.CurrentMonitor.Debug( $"{first}: ActiveIndex={first.ActiveIndex}." );
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

