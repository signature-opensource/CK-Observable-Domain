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
    /// Base behavior for timed event management.
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
        ObservableEventHandler<ObservableTimedEventArgs> _handlers;

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
            Name = r.ReadNullableString();
            _disposed = new ObservableEventHandler<ObservableDomainEventArgs>( r );
            _handlers = new ObservableEventHandler<ObservableTimedEventArgs>( r );
            Tag = r.ReadObject();

            if( ActiveIndex != 0 ) TimeManager.OnLoadedActive( this );
        }

        void Write( BinarySerializer w )
        {
            Debug.Assert( !IsDisposed );
            w.Write( ActiveIndex );
            w.Write( ExpectedDueTimeUtc );
            w.WriteNullableString( Name );
            _disposed.Write( w );
            _handlers.Write( w );
            w.WriteObject( Tag );
        }

        /// <summary>
        /// Gets whether this timed event is active.
        /// There must be at least one <see cref="Elapsed"/> registered callback for this to be true.
        /// </summary>
        public bool IsActive => _handlers.HasHandlers && GetIsActive();

        internal abstract bool GetIsActive();

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

        /// <summary>
        /// Gets or sets an optional name for this timed object.
        /// Default to null.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The timed event.
        /// </summary>
        public event SafeEventHandler<ObservableTimedEventArgs> Elapsed
        {
            add
            {
                this.CheckDisposed();
                _handlers.Add( value, nameof( Elapsed ) );
                TimeManager.OnChanged( this );
            }
            remove
            {
                this.CheckDisposed();
                if( _handlers.Remove( value ) ) TimeManager.OnChanged( this );
            }
        }

        internal void DoRaise( IActivityMonitor monitor, DateTime current, bool throwException )
        {
            Debug.Assert( !IsDisposed );
            if( _handlers.HasHandlers )
            {
                var ev = new ObservableTimedEventArgs( this, current, ExpectedDueTimeUtc );
                using( monitor.OpenDebug( $"Raising {ToString()} (Delta: {ev.DeltaMilliSeconds} ms)." ) )
                {
                    if( throwException ) _handlers.Raise( this, ev );
                    else
                    {
                        try
                        {
                            _handlers.Raise( this, ev );
                        }
                        catch( Exception ex )
                        {
                            monitor.Warn( $"Error while raising Timed event. It is ignored.", ex );
                        }
                    }
                }
            }
        }

        private protected virtual void OnRaising( IActivityMonitor monitor, bool throwException, ObservableTimedEventArgs ev )
        {
        }

        internal abstract void OnAfterRaiseUnchanged( DateTime current, IActivityMonitor m );

        /// <summary>
        /// This applies to reminders.
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
        public void Dispose()
        {
            if( !IsDisposed )
            {
                TimeManager.Domain.CheckBeforeDispose( this );
                _disposed.Raise( this, Domain.DefaultEventArgs );
                _disposed.RemoveAll();
                TimeManager.OnDisposed( this );
                TimeManager = null;
                _handlers.RemoveAll();
                Tag = null;
            }
        }
    }

}
