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
    [SerializationVersion( 0 )]
    public sealed class SuspendableClock : InternalObject
    {
        ObservableTimedEventBase? _firstInClock;
        ObservableTimedEventBase? _lastInClock;
        ObservableEventHandler<ObservableDomainEventArgs> _isActiveChanged;

        TimeSpan _cumulativeOffset;
        DateTime _lastStop;
        bool _isActive;

        /// <summary>
        /// Creates a new <see cref="SuspendableClock"/>.
        /// </summary>
        /// <param name="isActive">Whether this clock is initally active or not.</param>
        public SuspendableClock( bool isActive = true )
        {
            Debug.Assert( _cumulativeOffset == TimeSpan.Zero );
            _isActive = isActive;
            if( !isActive )
            {
                _lastStop = DateTime.UtcNow;
            }
        }

        SuspendableClock( IBinaryDeserializerContext d )
            : base( d )
        {
            var r = d.StartReading().Reader;
            _isActive = r.ReadBoolean();
            _lastStop = r.ReadDateTime();
            _cumulativeOffset = r.ReadTimeSpan();
            _firstInClock = (ObservableTimedEventBase?)r.ReadObject();
            _lastInClock = (ObservableTimedEventBase?)r.ReadObject();
            _isActiveChanged = new ObservableEventHandler<ObservableDomainEventArgs>( r );
        }

        void Write( BinarySerializer w )
        {
            w.Write( _isActive );
            w.Write( _lastStop );
            w.Write( _cumulativeOffset );
            w.WriteObject( _firstInClock );
            w.WriteObject( _lastInClock );
            _isActiveChanged.Write( w );
        }

        /// <summary>
        /// Gets the current time for this clock. It is <see cref="DateTime.UtcNow"/> only if this
        /// clock has never been deactivated, otherwise it is in the past.
        /// </summary>
        public DateTime UtcNow => _isActive ? DateTime.UtcNow.Subtract( _cumulativeOffset ) : _lastStop;

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
                        var lastStopDuration = DateTime.UtcNow - _lastStop;
                        _cumulativeOffset += lastStopDuration;
                        _isActive = true;
                        var t = _firstInClock;
                        while( t != null )
                        {
                            t.OnSuspendableClockActivated( lastStopDuration );
                            t = t.NextInClock;
                        }
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

        /// <summary>
        /// Raised when <see cref="IsActive"/> has changed.
        /// </summary>
        public event SafeEventHandler<ObservableDomainEventArgs> IsActiveChanged
        {
            add
            {
                this.CheckDisposed();
                _isActiveChanged.Add( value, nameof( IsActiveChanged ) );
            }
            remove => _isActiveChanged.Remove( value );
        }


        protected internal override void Dispose( bool shouldDisposeObjects )
        {
            if( shouldDisposeObjects )
            {
                while( _firstInClock != null )
                {
                    Debug.Assert( _firstInClock.SuspendableClock == this );
                    _firstInClock.SuspendableClock = null;
                }
            }
            base.Dispose( shouldDisposeObjects );
        }

        internal void Unbound( ObservableTimedEventBase o )
        {
            if( _firstInClock == o ) _firstInClock = o.NextInClock;
            else o.PrevInClock.NextInClock = o.NextInClock;
            if( _lastInClock == o ) _lastInClock = o.PrevInClock;
            else o.NextInClock.PrevInClock = o.PrevInClock;
        }

        internal void Bound( ObservableTimedEventBase o )
        {
            Debug.Assert( o.NextInClock == null );
            if( (o.NextInClock = _firstInClock) == null ) _lastInClock = o;
            else _firstInClock.PrevInClock = o;
            _firstInClock = o;
        }
    }
}
