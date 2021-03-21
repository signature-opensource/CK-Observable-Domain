using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace CK.Observable
{
    /// <summary>
    /// A suspendable clock is a clock that shifts back when <see cref="IsActive"/> is set to false.
    /// Whenever <see cref="IsActive"/> is false, the time is frozen: <see cref="UtcNow"/> is the time when
    /// the clock has been deactivated. When the clock is reactivated then the elapsed time during its deactivation
    /// offsets the <see cref="UtcNow"/>.
    /// <para>
    /// </para>
    /// Timers and reminders can be bound (or unbound) to suspendable clocks simply by setting
    /// the <see cref="ObservableTimedEventBase.SuspendableClock"/> property.
    /// <para>
    /// Timers and reminders don't fire when they are bound to a <see cref="SuspendableClock"/> that is deactivated.
    /// Their next due time (<see cref="ObservableTimer.DueTimeUtc"/> and <see cref="ObservableReminder.DueTimeUtc"/>)
    /// is postponed by the duration of the deactivation when the clock is reactivated.
    /// </para>
    /// </summary>
    /// <remarks>
    /// This is sealed <see cref="InternalObject"/>, it cannot be specialized and is not exported to remote clients.
    /// </remarks>
    [SerializationVersion( 3 )]
    public sealed class SuspendableClock : InternalObject
    {
        ObservableTimedEventBase? _firstInClock;
        ObservableTimedEventBase? _lastInClock;
        ObservableEventHandler<ObservableDomainEventArgs> _isActiveChanged;

        TimeSpan _cumulativeOffset;
        DateTime _lastStop;
        int _count;
        bool _isActive;
        bool _cumulateUnloadedTime;

        /// <summary>
        /// Creates a new <see cref="SuspendableClock"/>.
        /// </summary>
        /// <param name="isActive">Whether this clock is initially active or not.</param>
        public SuspendableClock( bool isActive = true )
        {
            Debug.Assert( _cumulativeOffset == TimeSpan.Zero );
            _cumulateUnloadedTime = true;
            _isActive = isActive;
            if( !isActive )
            {
                _lastStop = DateTime.UtcNow;
            }
        }

        List<ObservableTimedEventBase>? _0Bug;
        bool _0BugDone;
        internal IReadOnlyList<ObservableTimedEventBase>? V0Bug
        {
            get
            {
                if( !_0BugDone && _0Bug != null )
                {
                    _0BugDone = true;
                    for( int i = 0; i < _0Bug.Count; ++i )
                    {
                        var o = _0Bug[i];
                        o.PrevInClock = i == 0 ? null : _0Bug[i - 1];
                        o.NextInClock = i == _0Bug.Count - 1 ? null : _0Bug[i + 1];
                    }
                    _firstInClock = _0Bug[0];
                    _lastInClock = _0Bug[^1];
                }
                return _0Bug;
            }
        }

        SuspendableClock( IBinaryDeserializer r, TypeReadInfo info )
            : base( RevertSerialization.Default )
        {
            _cumulativeOffset = r.ReadTimeSpan();
            _isActive = r.ReadBoolean();
            if( _isActive )
            {
                var t = r.ReadDateTime();
                if( t != Util.UtcMinValue )
                {
                    _cumulateUnloadedTime = true;
                    var unloadedDuration = DateTime.UtcNow - t;
                    r.ImplementationServices.OnPostDeserialization( () => AdjustCumulativeOffset( unloadedDuration ) );
                }
            }
            else
            {
                _cumulateUnloadedTime = r.ReadBoolean();
                _lastStop = r.ReadDateTime();
            }
            if( info.Version == 0 )
            {
                _firstInClock = (ObservableTimedEventBase?)r.ReadObject();
                _lastInClock = (ObservableTimedEventBase?)r.ReadObject();
                var t = _firstInClock;
                while( t != null )
                {
                    if( _0Bug == null ) _0Bug = new List<ObservableTimedEventBase>();
                    _0Bug.Add( t );
                    t = t.NextInClock;
                }
                _count = _0Bug?.Count ?? 0;
            }
            else if( info.Version == 1 )
            {
                int count = r.ReadNonNegativeSmallInt32();
                while( --count >= 0 )
                {
                    if( count == 6425 ) break;
                    var t = (ObservableTimedEventBase)r.ReadObject()!;
                    t.SetDeserializedClock( this );
                    Bound( t );
                }
            }
            else if( info.Version == 2 )
            {
                // There is a bug in count/linked list (0Bug was... buggy on the _lastInClock).
                // Rely only on the linked list that has been written with boolean markers.
                int count = r.ReadNonNegativeSmallInt32();
                while( r.ReadBoolean() )
                {
                    var t = (ObservableTimedEventBase)r.ReadObject()!;
                    t.SetDeserializedClock( this );
                    Bound( t );
                }
                CheckInvariant();
            }
            else
            {
                int count = r.ReadNonNegativeSmallInt32();
                while( --count >= 0 )
                {
                    var t = (ObservableTimedEventBase)r.ReadObject()!;
                    t.SetDeserializedClock( this );
                    Bound( t );
                }
                CheckInvariant();
            }
            _isActiveChanged = new ObservableEventHandler<ObservableDomainEventArgs>( r );
        }

        void Write( BinarySerializer w )
        {
            w.Write( _cumulativeOffset );
            w.Write( _isActive );
            if( _isActive )
            {
                // Fact: when IsActive is true, we don't care of _lastStop value: it will
                // be reset next time IsActive is set to false.
                // We use this fact to handle the "unloaded" time here.
                w.Write( _cumulateUnloadedTime ? DateTime.UtcNow : Util.UtcMinValue );
            }
            else
            {
                w.Write( _cumulateUnloadedTime );
                w.Write( _lastStop );
            }
            CheckInvariant();
            w.WriteNonNegativeSmallInt32( _count );
            ObservableTimedEventBase? t = _firstInClock;
            while( t != null )
            {
                w.WriteObject( t );
                t = t.NextInClock;
            }
            _isActiveChanged.Write( w );
        }

        /// <summary>
        /// Gets the number of <see cref="ObservableTimedEventBase"/> bound to this clock.
        /// </summary>
        public int Count => _count;

        /// <summary>
        /// Gets the current time for this clock. It is <see cref="DateTime.UtcNow"/> only if this
        /// clock has never been deactivated, otherwise it is in the past.
        /// <para>
        /// This is <see cref="DateTime.UtcNow"/> - <see cref="CumulativeOffset"/> when <see cref="IsActive"/> is true,
        /// otherwise it the last time when IsActive became false.
        /// </para>
        /// </summary>
        public DateTime UtcNow => _isActive ? DateTime.UtcNow.Subtract( _cumulativeOffset ) : _lastStop;

        /// <summary>
        /// Gets the time span during which this clock has been suspended (this is greater than or
        /// equal to <see cref="TimeSpan.Zero"/>): this is increased each time <see cref="IsActive"/> becomes true.
        /// </summary>
        public TimeSpan CumulativeOffset => _cumulativeOffset;

        /// <summary>
        /// Gets or sets whether the time spent unloaded should be added to <see cref="CumulativeOffset"/>
        /// when <see cref="IsActive"/> is true.
        /// Defaults to true.
        /// <para>
        /// When IsActive is false, we have nothing to do: the time of the last suspension is memorized and will
        /// remain the same whether the domain is loaded or not (the time spent unloaded, by design, appear in the CumulativeOffset).
        /// </para>
        /// </summary>
        public bool CumulateUnloadedTime
        {
            get => _cumulateUnloadedTime;
            set => _cumulateUnloadedTime = value;
        }

        /// <summary>
        /// Gets or sets whether this clock is active.
        /// Defaults to true.
        /// </summary>
        public bool IsActive
        {
            get => _isActive;
            set
            {
                if( _isActive != value )
                {
                    if( value )
                    {
                        _isActive = true;
                        AdjustCumulativeOffset( DateTime.UtcNow - _lastStop );
                    }
                    else
                    {
                        // Suspending this clock: all the bound ObservableTimedEventBase become
                        // deactivated as soon as we set _isActive to false.
                        _lastStop = DateTime.UtcNow;
                        var t = _firstInClock;
                        while( t != null )
                        {
                            // Small optim: consider only currently active beasts.
                            if( t.IsActive ) ActualDomain.TimeManager.OnChanged( t );
                            t = t.NextInClock;
                        }
                        _isActive = false;
                    }
                    if( _isActiveChanged.HasHandlers ) _isActiveChanged.Raise( this, Domain.DefaultEventArgs );
                }
            }
        }

        void AdjustCumulativeOffset( TimeSpan lastStopDuration )
        {
            _cumulativeOffset += lastStopDuration;
            var t = _firstInClock;
            while( t != null )
            {
                t.OnSuspendableClockActivated( lastStopDuration );
                t = t.NextInClock;
            }
        }

        /// <summary>
        /// Raised when <see cref="IsActive"/> has changed.
        /// </summary>
        public event SafeEventHandler<ObservableDomainEventArgs> IsActiveChanged
        {
            add
            {
                this.CheckDestroyed();
                _isActiveChanged.Add( value, nameof( IsActiveChanged ) );
            }
            remove => _isActiveChanged.Remove( value );
        }


        protected internal override void OnDestroy()
        {
            CheckInvariant();
            while( _firstInClock != null )
            {
                Debug.Assert( _firstInClock.SuspendableClock == this );
                _firstInClock.SuspendableClock = null;
            }
            base.OnDestroy();
        }

        internal void Unbound( ObservableTimedEventBase o )
        {
            --_count;
            if( _firstInClock == o ) _firstInClock = o.NextInClock;
            else o.PrevInClock.NextInClock = o.NextInClock;
            if( _lastInClock == o ) _lastInClock = o.PrevInClock;
            else o.NextInClock.PrevInClock = o.PrevInClock;
            CheckInvariant();
        }

        internal void Bound( ObservableTimedEventBase o )
        {
            Debug.Assert( o.NextInClock == null && o.PrevInClock == null );
            ++_count;

            if( (o.PrevInClock = _lastInClock) == null ) _firstInClock = o;
            else _lastInClock.NextInClock = o;
            _lastInClock = o;

            CheckInvariant();
        }

        [Conditional("DEBUG")]
        void CheckInvariant()
        {
            Debug.Assert( (_count > 0 && _firstInClock != null && _lastInClock != null) || (_count == 0 && _firstInClock == null && _lastInClock == null) );
            ObservableTimedEventBase? prev = null;
            var t = _firstInClock;
            for( int i = 0; i < _count; ++i )
            {
                Debug.Assert( t != null );
                Debug.Assert( t.PrevInClock == prev );
                prev = t;
                t = t.NextInClock;
            }
            Debug.Assert( _lastInClock == prev );
            Debug.Assert( t == null );
        }
    }
}
