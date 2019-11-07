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
    /// Simple reminder that raises its <see cref="ObservableTimedEventBase.Elapsed"/> event once at <see cref="DueTimeUtc"/> time.
    /// This should not be used directly since <see cref="ObservableObject.Remind"/> and <see cref="InternalObject.Remind"/> methods are
    /// easier to use.
    /// </summary>
    [SerializationVersion(0)]
    public sealed class ObservableReminder : ObservableTimedEventBase
    {
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
        }

        ObservableReminder( IBinaryDeserializerContext c )
            : base( c )
        {
            var r = c.StartReading();
        }

        void Write( BinarySerializer w )
        {
        }

        internal override bool GetIsActive() => ExpectedDueTimeUtc != Util.UtcMinValue && ExpectedDueTimeUtc != Util.UtcMaxValue; 

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
                    this.CheckDisposed();
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
        /// Overridden to return the <see cref="ObservableTimedEventBase.Name"/> of this reminder.
        /// </summary>
        /// <returns>A readable string.</returns>
        public override string ToString() => $"{(IsDisposed ? "[Disposed]" : "")}ObservableReminder '{Name ?? "<no name>"}'";

    }

}
