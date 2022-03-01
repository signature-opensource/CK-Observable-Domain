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
    /// Base behavior for timed event that handles internal timer data and basic life cycle aspects.
    /// Concrete specializations are <see cref="ObservableTimer"/> and <see cref="ObservableReminder"/>.
    /// Note that <see cref="IsActive"/> can be false.
    /// </summary>
    [BinarySerialization.SerializationVersion( 1 )]
    [NotExportable]
    public abstract class ObservableTimedEventBase : IDestroyableObject, BinarySerialization.IDestroyable, BinarySerialization.ICKSlicedSerializable
    {
        internal TimeManager? TimeManager;
        /// <summary>
        /// The ActiveIndex is the index of this object in the heap managed by the TimeManager.
        /// When 0, this is "out of the heap": this is not active.
        /// </summary>
        internal int ActiveIndex;

        /// <summary>
        /// ExpectedDueTimeUtc is the actual due time that is considered by the TimeManager.
        /// </summary>
        internal DateTime ExpectedDueTimeUtc;
        internal ObservableTimedEventBase? Next;
        internal ObservableTimedEventBase? Prev;

        internal ObservableTimedEventBase? NextInClock;
        internal ObservableTimedEventBase? PrevInClock;
        SuspendableClock? _clock;

        ObservableEventHandler<ObservableDomainEventArgs> _disposed;

        protected ObservableTimedEventBase()
        {
            TimeManager = ObservableDomain.GetCurrentActiveDomain().TimeManager;
            TimeManager.OnCreated( this, true );
        }

        #region Old Deserialization
        ObservableTimedEventBase( IBinaryDeserializer r, TypeReadInfo info )
        {
            int index = r.ReadInt32();
            if( index >= 0 )
            {
                TimeManager = ObservableDomain.GetCurrentActiveDomain().TimeManager;
                Debug.Assert( TimeManager != null );
                ActiveIndex = index;
                ExpectedDueTimeUtc = r.ReadDateTime();
                if( info.Version == 0 )
                {
                    _clock = (SuspendableClock?)r.ReadObject();
                    NextInClock = (ObservableTimedEventBase?)r.ReadObject();
                    PrevInClock = (ObservableTimedEventBase?)r.ReadObject();

                    r.ImplementationServices.OnPostDeserialization( () =>
                    {
                        if( _clock != null )
                        {
                            if( _clock.V0Bug == null || !_clock.V0Bug.Contains( this ) )
                            {
                                _clock = null;
                                if( NextInClock != null ) NextInClock = null;
                                if( PrevInClock != null ) PrevInClock = null;
                            }
                        }
                        else
                        {
                            // Oups?
                            if( NextInClock != null ) NextInClock = null;
                            if( PrevInClock != null ) PrevInClock = null;
                        }

                    } );
                }
                _disposed = new ObservableEventHandler<ObservableDomainEventArgs>( r );
                Tag = r.ReadObject();
                // Call to TimeManager.OnCreated is done by TimeManager.Load() that is called at the end of the object load,
                // before the sidekicks so that the order in the linked list of ObservableTimedEventBase is preserved.
                // Activation is also done by TimeManager.Load().
                Debug.Assert( !IsDestroyed );
            }
            else
            {
                Debug.Assert( IsDestroyed );
            }
        }
        #endregion

        #region New Serialization
        /// <summary>
        /// Specialized deserialization constructor for specialized classes.
        /// </summary>
        /// <param name="_">Unused parameter.</param>
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        protected ObservableTimedEventBase( BinarySerialization.Sliced _ ) { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        ObservableTimedEventBase( BinarySerialization.IBinaryDeserializer d, BinarySerialization.ITypeReadInfo info )
        {
            int index = d.Reader.ReadInt32();
            if( index >= 0 )
            {
                TimeManager = ObservableDomain.GetCurrentActiveDomain().TimeManager;
                Debug.Assert( TimeManager != null );
                ActiveIndex = index;
                ExpectedDueTimeUtc = d.Reader.ReadDateTime();
                _disposed = new ObservableEventHandler<ObservableDomainEventArgs>( d );
                Tag = d.ReadNullableObject<object>();
                // Call to TimeManager.OnCreated is done by TimeManager.Load() that is called at the end of the object load,
                // before the sidekicks so that the order in the linked list of ObservableTimedEventBase is preserved.
                // Activation is also done by TimeManager.Load().
                Debug.Assert( !IsDestroyed );
            }
            else
            {
                Debug.Assert( IsDestroyed );
            }
        }
        
        public static void Write( BinarySerialization.IBinarySerializer s, in ObservableTimedEventBase o )
        {
            if( o.IsDestroyed )
            {
                s.Writer.Write( -1 );
            }
            else
            {
                s.Writer.Write( o.ActiveIndex );
                s.Writer.Write( o.ExpectedDueTimeUtc );
                o._disposed.Write( s );
                s.WriteNullableObject( o.Tag );
            }
        }
        #endregion

        /// <summary>
        /// This is used by the <see cref="TimeManager.Save"/> to track lost objects.
        /// </summary>
        internal abstract bool HasHandlers { get; }

        /// <summary>
        /// Gets whether this timed event is active.
        /// There must be at least one Elapsed registered callback for this to be true, and if a
        /// bound <see cref="SuspendableClock"/> exists, it must be active.
        /// </summary>
        public abstract bool IsActive { get; }

        /// <summary>
        /// Gets the <see cref="SuspendableClock"/> to which this <see cref="ObservableTimedEventBase"/> is bound.
        /// </summary>
        public SuspendableClock? SuspendableClock
        {
            get => _clock;
            set
            {
                if( _clock == value ) return;
                this.CheckDestroyed();

                bool previousActiveClock = true;
                bool currentActiveClock = true;
                if( _clock != null )
                {
                    previousActiveClock = _clock.IsActive;
                    _clock.Unbound( this );
                    _clock = null;
                    Debug.Assert( NextInClock == null && PrevInClock == null );
                }
                if( value != null )
                {
                    value.CheckDestroyed();
                    currentActiveClock = value.IsActive;
                    value.Bound( this );
                    _clock = value;
                }
                if( previousActiveClock != currentActiveClock )
                {
                    Debug.Assert( TimeManager != null, "Since this is not disposed." );
                    TimeManager.OnChanged( this );
                }
            }
        }

        /// <summary>
        /// Gets whether this object has been disposed.
        /// </summary>
        public bool IsDestroyed => TimeManager == null;

        /// <summary>
        /// Gets or sets an associated object that can be useful for simple scenario where a state
        /// must be associated to the event source without polluting the object model itself.
        /// This object must be serializable. This property is set to null after <see cref="Destroy"/> has been called.
        /// </summary>
        /// <remarks>
        /// This is a facility, just an easy way to associate data to a timer or a reminder. This should be used with care
        /// as systematic use of this association may denote a lack of modeling.
        /// </remarks>
        public object? Tag { get; set; }

        internal abstract void DoRaise( IActivityMonitor monitor, DateTime current, bool throwException );

        internal abstract void OnAfterRaiseUnchanged( DateTime current, IActivityMonitor m );

        /// <summary>
        /// A timer does nothing on deactivation. Reminders override this: if they are pooled, are
        /// cleared and returned to the reminders' pool. 
        /// </summary>
        internal virtual void OnDeactivate() { }

        internal void SetDeserializedClock( SuspendableClock clock )
        {
            Debug.Assert( _clock == null && TimeManager != null && TimeManager.Domain.IsDeserializing );
            _clock = clock;
        }

        internal void OnSuspendableClockActivated( TimeSpan lastStopDuration )
        {
            Debug.Assert( TimeManager != null && _clock != null && _clock.IsActive );
            // The clock became active.
            // Whenever ExpectedDueTimeUtc is, we postpone it with the duration of the stop. 
            if( ExpectedDueTimeUtc != Util.UtcMinValue && ExpectedDueTimeUtc != Util.UtcMaxValue )
            {
                ExpectedDueTimeUtc += lastStopDuration;
                TimeManager.OnChanged( this );
            }
        }

        /// <summary>
        /// This default implementation applies to reminders.
        /// </summary>
        internal virtual void ForwardExpectedDueTime( IActivityMonitor monitor, DateTime forwarded )
        {
            monitor.Warn( $"{ToString()}: next due time '{ExpectedDueTimeUtc.ToString( "o" )}' has been forwarded to '{forwarded.ToString( "o" )}'." );
            ExpectedDueTimeUtc = forwarded;
        }

        /// <summary>
        /// Raised when this object is <see cref="Destroy()"/>d.
        /// Note that when the call to dispose is made by <see cref="ObservableDomain.Load(IActivityMonitor, System.IO.Stream, bool, Encoding, int, bool)"/>,
        /// this event is not triggered.
        /// </summary>
        public event SafeEventHandler<ObservableDomainEventArgs> Destroyed
        {
            add => _disposed.Add( value, nameof( Destroyed ) );
            remove => _disposed.Remove( value );
        }

        /// <summary>
        /// Destroys this timed event.
        /// </summary>
        public virtual void Destroy()
        {
            if( !IsDestroyed )
            {
                Debug.Assert( TimeManager != null );
                if( _clock != null )
                {
                    _clock.Unbound( this );
                    _clock = null;
                }
                TimeManager.OnPreDisposed( this );
                Debug.Assert( ActiveIndex == 0, "Timed event has been removed from the priority queue." );
                _disposed.Raise( this, TimeManager.Domain.DefaultEventArgs );
                _disposed.RemoveAll();
                TimeManager.OnDestroyed( this );
                TimeManager = null;
                Tag = null;
            }
        }
    }

}
