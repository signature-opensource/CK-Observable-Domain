using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{ 
    /// <summary>
    /// Simple timer that raises its <see cref="ObservableTimedEventBase.Elapsed"/> event repeatedly.
    /// The event time is based on the <see cref="DueTimeUtc"/>: we try to always raise the event based on a multiple
    /// of the <see cref="IntervalMilliSeconds"/> from <see cref="DueTimeUtc"/>.
    /// </summary>
    public class ObservableTimer : ObservableTimedEventBase
    {
        int _milliSeconds;
        bool _isActive;

        /// <summary>
        /// Initializes a new unnamed <see cref="ObservableTimer"/> bound to the current <see cref="ObservableDomain"/>.
        /// Initial configuration is adjusted by <see cref="AdjustNextDueTimeUtc(DateTime, DateTime, int)"/> with base time <see cref="DateTime.UtcNow"/>.
        /// </summary>
        /// <param name="firstDueTimeUtc">
        /// The first time where the event must be fired. This is adjusted by <see cref="AdjustNextDueTimeUtc(DateTime, DateTime, int)"/> with
        /// base time <see cref="DateTime.UtcNow"/>.
        /// </param>
        /// <param name="intervalMilliSeconds">The interval in millisecond (defaults to 1 second). Must be positive.</param>
        public ObservableTimer( DateTime firstDueTimeUtc, int intervalMilliSeconds = 1000 )
        {
            ExpectedDueTimeUtc = AdjustNextDueTimeUtc( DateTime.UtcNow, firstDueTimeUtc, intervalMilliSeconds );
            _milliSeconds = intervalMilliSeconds;
        }

        internal override bool GetIsActive() => _isActive && ExpectedDueTimeUtc != Util.UtcMinValue && ExpectedDueTimeUtc != Util.UtcMaxValue;

        /// <summary>
        /// Gets or sets whether this timer is active. Note that to be active <see cref="DueTimeUtc"/> must not be <see cref="Util.UtcMinValue"/>
        /// nor <see cref="Util.UtcMaxValue"/>.
        /// </summary>
        public new bool IsActive
        {
            get => GetIsActive();
            set
            {
                if( _isActive != value )
                {
                    CheckDisposed();
                    _isActive = value;
                    TimeManager.OnChanged( this );
                }
            }
        }

        /// <summary>
        /// Gets the next due time.
        /// If this is <see cref="Util.UtcMinValue"/> nor <see cref="Util.UtcMaxValue"/>, then <see cref="ObservableTimedEventBase.IsActive">IsActive</see>
        /// is false.
        /// </summary>
        public DateTime DueTimeUtc { get; private set; }

        /// <summary>
        /// Gets or sets the <see cref="ObservableTimerMode"/> to apply.
        /// Defaults to <see cref="ObservableTimerMode.Relaxed"/> (<see cref="DueTimeUtc"/> is allowed to shift by any numer of <see cref="IntervalMilliSeconds"/> steps,
        /// only a warning is emitted).
        /// </summary>
        public ObservableTimerMode Mode { get; set; }

        /// <summary>
        /// Gets or sets the interval, expressed in milliseconds, at which the <see cref="ObservableTimedEventBase.Elapsed"/> event must repeatedly fire.
        /// The value must be greater than zero.
        /// </summary>
        public int IntervalMilliSeconds
        {
            get => _milliSeconds;
            set
            {
                if( _milliSeconds != value )
                {
                    CheckDisposed();
                    if( DueTimeUtc == Util.UtcMinValue || DueTimeUtc == Util.UtcMaxValue )
                    {
                        if( value <= 0 ) throw new ArgumentOutOfRangeException( nameof( IntervalMilliSeconds ) );
                        _milliSeconds = value;
                    }
                    else Reconfigure( DueTimeUtc, value );
                }
            }
        }

        /// <summary>
        /// Reconfigures this <see cref="ObservableTimer"/> with a new <see cref="DueTimeUtc"/> and <see cref="IntervalMilliSeconds"/>.
        /// </summary>
        /// <param name="firstDueTimeUtc">
        /// The first time where the event must be fired. If this time is in the past (but not <see cref="Util.UtcMinValue"/> nor <see cref="Util.UtcMaxValue"/>),
        /// the event will be raised as soon as possible.
        /// </param>
        /// <param name="intervalMilliSeconds">The interval in millisecond (defaults to 1 second).</param>
        public void Reconfigure( DateTime firstDueTimeUtc, int intervalMilliSeconds )
        {
            CheckDisposed();
            ExpectedDueTimeUtc = DueTimeUtc = AdjustNextDueTimeUtc( DateTime.UtcNow, firstDueTimeUtc, intervalMilliSeconds );
            _milliSeconds = intervalMilliSeconds;
            TimeManager.OnChanged( this );
        }

        internal override void OnAfterRaiseUnchanged()
        {
            Debug.Assert( IsActive );
            ExpectedDueTimeUtc = DueTimeUtc = DueTimeUtc.AddMilliseconds( _milliSeconds );
        }

        internal override void ForwardExpectedDueTime( IActivityMonitor monitor, DateTime forwarded )
        {
            Debug.Assert( ExpectedDueTimeUtc < forwarded );
            int stepCount = (int)Math.Ceiling( (forwarded - ExpectedDueTimeUtc).TotalMilliseconds / _milliSeconds );
            Debug.Assert( stepCount > 0 );
            var mode = Mode & ~ObservableTimerMode.ThrowException;
            var throwEx = (Mode & ObservableTimerMode.ThrowException) != 0;
            var msg = $"{ToString()}: next due time '{ExpectedDueTimeUtc.ToString( "o" )}' has been forwarded to '{forwarded.ToString( "o" )}'. ";

            void RaiseError()
            {
                msg += $" This is an error since Mode is {mode}.";
                if( throwEx ) throw new CKException( msg );
                else monitor.Error( msg );
            }

            if( stepCount == 1 )
            {
                msg += "No event lost.";
                if( mode == ObservableTimerMode.AllowSlidingAdjustment )
                {
                    ExpectedDueTimeUtc = forwarded;
                }
                else
                {
                    ExpectedDueTimeUtc = DueTimeUtc = ExpectedDueTimeUtc.AddMilliseconds( _milliSeconds );
                    msg += $" DueTimeUtc aligned to {ExpectedDueTimeUtc.ToString( "o" )}.";
                }
                if( mode == ObservableTimerMode.Critical ) RaiseError();
                else monitor.Warn( msg );
            }
            else
            {
                msg += $"{stepCount-1} event(s) lost!";
                ExpectedDueTimeUtc = DueTimeUtc = ExpectedDueTimeUtc.AddMilliseconds( stepCount * _milliSeconds );
                msg += $" DueTimeUtc aligned to {ExpectedDueTimeUtc.ToString( "o" )}.";
                if( mode == ObservableTimerMode.Relaxed ) monitor.Warn( msg );
                else RaiseError();
            }
        }

        /// <summary>
        /// Ensures that the <paramref name="firstDueTimeUtc"/> will occur after or on <paramref name="baseTimeUtc"/>.
        /// </summary>
        /// <param name="baseTimeUtc">Typically equals <see cref="DateTime.UtcNow"/>. Must be in Utc.</param>
        /// <param name="firstDueTimeUtc">The first due time. Must be in Utc. When <see cref="Util.UtcMinValue"/> or <see cref="Util.UtcMaxValue"/> it is returned as-is.</param>
        /// <param name="intervalMilliSeconds">The interval. Must be positive.</param>
        /// <returns>The adjusted first due time, necessarily after the <paramref name="baseTimeUtc"/>.</returns>
        public static DateTime AdjustNextDueTimeUtc( DateTime baseTimeUtc, DateTime firstDueTimeUtc, int intervalMilliSeconds )
        {
            if( firstDueTimeUtc.Kind != DateTimeKind.Utc ) throw new ArgumentException( nameof( firstDueTimeUtc ), "Must be a Utc DateTime." );
            if( intervalMilliSeconds <= 0 ) throw new ArgumentOutOfRangeException( nameof( intervalMilliSeconds ) );
            if( firstDueTimeUtc != Util.UtcMinValue && firstDueTimeUtc != Util.UtcMaxValue )
            {
                if( firstDueTimeUtc < baseTimeUtc )
                {
                    int adjust = ((int)Math.Ceiling( (baseTimeUtc - firstDueTimeUtc).TotalMilliseconds / intervalMilliSeconds )) * intervalMilliSeconds;
                    firstDueTimeUtc = firstDueTimeUtc.AddMilliseconds( adjust );
                }
            }
            return firstDueTimeUtc;
        }

        /// <summary>
        /// Overridden to return the <see cref="ObservableTimedEventBase.Name"/> of this reminder.
        /// </summary>
        /// <returns>A readable string.</returns>
        public override string ToString() => $"{(IsDisposed ? "[Disposed]" : "")}ObservableTimer '{Name ?? "<no name>"}'";

    }

}
