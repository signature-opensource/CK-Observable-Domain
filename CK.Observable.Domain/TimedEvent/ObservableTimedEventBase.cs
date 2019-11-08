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
    /// </summary>
    [SerializationVersion( 0 )]
    public abstract class ObservableTimedEventBase : IDisposableObject
    {
        internal TimeManager TimeManager;
        internal int ActiveIndex;

        /// <summary>
        /// ExpectedDueTimeUtc is the actual due time that is considered by the TimeManager.
        /// </summary>
        internal DateTime ExpectedDueTimeUtc;
        internal ObservableTimedEventBase Next;
        internal ObservableTimedEventBase Prev;

        ObservableEventHandler<ObservableDomainEventArgs> _disposed;

        internal ObservableTimedEventBase()
        {
            TimeManager = ObservableDomain.GetCurrentActiveDomain().TimeManager;
            TimeManager.OnCreated( this );
        }

        protected ObservableTimedEventBase( IBinaryDeserializerContext c )
            : this()
        {
            var r = c.StartReading();
            ActiveIndex = r.ReadInt32();
            ExpectedDueTimeUtc = r.ReadDateTime();
            _disposed = new ObservableEventHandler<ObservableDomainEventArgs>( r );
            Tag = r.ReadObject();
            if( ActiveIndex != 0 ) TimeManager.OnLoadedActive( this );
        }

        void Write( BinarySerializer w )
        {
            Debug.Assert( !IsDisposed );
            w.Write( ActiveIndex );
            w.Write( ExpectedDueTimeUtc );
            _disposed.Write( w );
            w.WriteObject( Tag );
        }

        /// <summary>
        /// Gets whether this timed event is active.
        /// There must be at least one registered callback for this to be true.
        /// </summary>
        public abstract bool IsActive { get; }

        /// <summary>
        /// Gets whether this object has been disposed.
        /// </summary>
        public bool IsDisposed => TimeManager == null;

        /// <summary>
        /// Gets the domain to which this timed even belongs.
        /// Null when <see cref="IsDisposed"/> is true.
        /// </summary>
        public ObservableDomain Domain => TimeManager?.Domain;

        /// <summary>
        /// Gets or sets an associated object that can be useful for simple scenario where a state
        /// must be associated to the event source without polluting the object model itself.
        /// This object must be serializable. This property is set to null after <see cref="Dispose"/> has been called.
        /// </summary>
        public object Tag { get; set; }

        internal abstract void DoRaise( IActivityMonitor monitor, DateTime current, bool throwException );

        internal abstract void OnAfterRaiseUnchanged( DateTime current, IActivityMonitor m );

        internal abstract void OnDeactivate();

        /// <summary>
        /// This default implementation applies to reminders.
        /// </summary>
        internal virtual void ForwardExpectedDueTime( IActivityMonitor monitor, DateTime forwarded )
        {
            monitor.Warn( $"{ToString()}: next due time '{ExpectedDueTimeUtc.ToString( "o" )}' has been forwarded to '{forwarded.ToString( "o" )}'." );
            ExpectedDueTimeUtc = forwarded;
        }

        /// <summary>
        /// Raised when this object is <see cref="Dispose"/>d.
        /// Note that when the call to dispose is made by <see cref="ObservableDomain.Load"/>, this event is not
        /// triggered.
        /// </summary>
        public event SafeEventHandler<ObservableDomainEventArgs> Disposed
        {
            add => _disposed.Add( value, nameof( Disposed ) );
            remove => _disposed.Remove( value );
        }

        /// <summary>
        /// Disposes this timed event.
        /// </summary>
        public virtual void Dispose()
        {
            if( !IsDisposed )
            {
                TimeManager.OnPreDisposed( this );
                Debug.Assert( ActiveIndex == 0, "Timed event has been removed from the priority queue." );
                _disposed.Raise( this, Domain.DefaultEventArgs );
                _disposed.RemoveAll();
                TimeManager.OnDisposed( this );
                TimeManager = null;
                Tag = null;
            }
        }
    }

}
