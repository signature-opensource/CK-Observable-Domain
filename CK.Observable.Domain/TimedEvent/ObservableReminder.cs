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
    /// Simple reminder that raises its <see cref="ObservableTimedEventBase{T}.Elapsed"/> event once at <see cref="DueTimeUtc"/> time.
    /// This should not be used directly since <see cref="DomainView.Remind(DateTime, SafeEventHandler{ObservableReminderEventArgs}, object?)"/> method
    /// is easier to use.
    /// </summary>
    [SerializationVersion(0)]
    public sealed class ObservableReminder : ObservableTimedEventBase<ObservableReminderEventArgs>
    {
        // Link to the next free reminder. Can be not null if and only if this reminder
        // is a pooled one and is currently inactive.
        internal ObservableReminder NextFreeReminder;

        /// <summary>
        /// Initializes a new unnamed <see cref="ObservableReminder"/> bound to the current <see cref="ObservableDomain"/>.
        /// </summary>
        /// <param name="dueTimeUtc">
        /// The <see cref="DueTimeUtc"/> time. If this time is in the past (but not <see cref="Util.UtcMinValue"/>), the event will
        /// be raised as soon as possible.
        /// </param>
        public ObservableReminder( DateTime dueTimeUtc )
        {
            if( dueTimeUtc.Kind != DateTimeKind.Utc ) throw new ArgumentException( nameof( dueTimeUtc ), "Must be a Utc DateTime." );
            ExpectedDueTimeUtc = dueTimeUtc;
            ReusableArgs = new ObservableReminderEventArgs( this );
        }

        /// <summary>
        /// Initialize a new pooled reminder.
        /// </summary>
        internal ObservableReminder()
        {
            IsPooled = true;
            ReusableArgs = new ObservableReminderEventArgs( this );
        }

        ObservableReminder( IBinaryDeserializer r, TypeReadInfo? info )
            : base( RevertSerialization.Default )
        {
            Debug.Assert( !IsDestroyed );
            IsPooled = r.ReadBoolean();
            ReusableArgs = new ObservableReminderEventArgs( this );
            if( IsPooled && ActiveIndex == 0 ) TimeManager.ReleaseToPool( this );
        }

        void Write( BinarySerializer w )
        {
            Debug.Assert( !IsDestroyed );
            w.Write( IsPooled );
        }

        /// <summary>
        /// Gets whether this reminder is a pooled one.
        /// Pooled reminders are created and managed by <see cref="DomainView.Remind(DateTime, SafeEventHandler{ObservableReminderEventArgs}, object?)"/>.
        /// Calling <see cref="Dispose"/> when this is true is an error.
        /// </summary>
        public bool IsPooled { get; }

        internal override void OnDeactivate()
        {
            Debug.Assert( !IsDestroyed );
            if( IsPooled )
            {
                ClearHandlesAndTag();
                TimeManager.ReleaseToPool( this );
            }
        }

        /// <summary>
        /// Destroys this reminder.
        /// If <see cref="IsPooled"/> is true, calling this method throws an <see cref="InvalidOperationException"/>.
        /// </summary>
        public override void Destroy()
        {
            if( IsPooled ) throw new InvalidOperationException( "A pooled ObservableReminder cannot be disposed." );
            base.Destroy();
        }

        internal void ForcePooledDestroy()
        {
            Debug.Assert( !IsDestroyed && IsPooled && !IsActive );
            base.Destroy();
        }

        private protected override ObservableReminderEventArgs ReusableArgs { get; }
        
        /// <summary>
        /// Gets or sets the next planned time: set it to <see cref="Util.UtcMinValue"/> or <see cref="Util.UtcMaxValue"/> to disable
        /// this reminder.
        /// If this time is in the past (but not <see cref="Util.UtcMinValue"/>), the event will be raised as soon as possible.
        /// </summary>
        public DateTime DueTimeUtc
        {
            get => ExpectedDueTimeUtc;
            set
            {
                if( ExpectedDueTimeUtc != value )
                {
                    if( value.Kind != DateTimeKind.Utc ) throw new ArgumentException( nameof( DueTimeUtc ), "Must be a Utc DateTime." );
                    this.CheckDestroyed();
                    ExpectedDueTimeUtc = value;
                    TimeManager.OnChanged( this );
                }
            }
        }

        internal override void OnAfterRaiseUnchanged( DateTime current, IActivityMonitor m )
        {
            Debug.Assert( IsActive );
            ExpectedDueTimeUtc = Util.UtcMinValue;
        }

        /// <summary>
        /// Overridden to return info on this reminder.
        /// </summary>
        /// <returns>A readable string.</returns>
        public override string ToString() => $"{(IsDestroyed ? "[Disposed]" : "")}ObservableReminder {(IsActive ? ExpectedDueTimeUtc.ToString( "o" ) : "(inactive)" )}.";

    }

}
