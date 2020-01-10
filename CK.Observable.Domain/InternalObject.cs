using CK.Core;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;

namespace CK.Observable
{
    /// <summary>
    /// Base class for all internal object.
    /// Internal objects are like <see cref="ObservableObject"/> but are not exportable (only serializable) and,
    /// as <see cref="IDisposableObject"/>, can implement events and event handler (<see cref="SafeEventHandler"/>
    /// and <see cref="SafeEventHandler{TEventArgs}"/>).
    /// </summary>
    [SerializationVersion( 0 )]
    public abstract class InternalObject : IDisposableObject
    {
        internal ObservableDomain Domain;
        internal InternalObject Next;
        internal InternalObject Prev;
        ObservableEventHandler<ObservableDomainEventArgs> _disposed;

        /// <summary>
        /// Raised when this object is <see cref="Dispose"/>d by <see cref="Dispose(bool)"/>.
        /// Note that when the call to dispose is made by <see cref="ObservableDomain.Load"/>, this event is not
        /// triggered to avoid a useless (and potentially dangerous) snowball effect: eventually ALL <see cref="InternalObject.Dispose(bool)"/>
        /// will be called during a reload.
        /// </summary>
        public event SafeEventHandler<ObservableDomainEventArgs> Disposed
        {
            add
            {
                this.CheckDisposed();
                _disposed.Add( value, nameof( Disposed ) );
            }
            remove => _disposed.Remove( value );
        }

        /// <summary>
        /// Constructor for specialized instance.
        /// The current domain is retrieved automatically: it is the last one on the current thread
        /// that has started a transaction (see <see cref="ObservableDomain.BeginTransaction"/>).
        /// </summary>
        protected InternalObject()
            : this( ObservableDomain.GetCurrentActiveDomain() )
        {
        }

        /// <summary>
        /// Constructor for specialized instance with an explicit <see cref="ObservableDomain"/>.
        /// </summary>
        /// <param name="domain">The domain to which this object belong.</param>
        protected InternalObject( ObservableDomain domain )
        {
            if( domain == null ) throw new ArgumentNullException( nameof( domain ) );
            Domain = domain;
            Domain.Register( this );
        }

        /// <summary>
        /// Deserialization constructor.
        /// </summary>
        /// <param name="d">The deserialization context.</param>
        protected InternalObject( IBinaryDeserializerContext d )
        {
            var r = d.StartReading();
            Debug.Assert( r.CurrentReadInfo.Version == 0 );
            Domain = r.Services.GetService<ObservableDomain>( throwOnNull: true );
            _disposed = new ObservableEventHandler<ObservableDomainEventArgs>( r );
            Domain.Register( this );
        }

        void Write( BinarySerializer w )
        {
            _disposed.Write( w );
        }

        /// <summary>
        /// Gives access to the monitor to use.
        /// </summary>
        protected IActivityMonitor Monitor => Domain?.CurrentMonitor;

        /// <summary>
        /// Sends a command to the external world. Commands are enlisted
        /// into <see cref="TransactionResult.Commands"/> (when the transaction succeeds)
        /// and can be processed by any <see cref="IObservableDomainClient"/>.
        /// </summary>
        /// <param name="command">Any command description.</param>
        protected void SendCommand( object command )
        {
            Domain.SendCommand( this, command );
        }

        /// <summary>
        /// Helper that raises a standard event from this internal object with a reusable <see cref="ObservableDomainEventArgs"/> instance.
        /// </summary>
        protected void RaiseStandardDomainEvent( ObservableEventHandler<ObservableDomainEventArgs> h ) => h.Raise( this, Domain.DefaultEventArgs );

        /// <summary>
        /// Helper that raises a standard event from this internal object with a reusable <see cref="EventMonitoredArgs"/> instance (that is the
        /// shared <see cref="ObservableDomainEventArgs"/> instance).
        /// </summary>
        protected void RaiseStandardDomainEvent( ObservableEventHandler<EventMonitoredArgs> h ) => h.Raise( this, Domain.DefaultEventArgs );

        /// <summary>
        /// Uses a pooled <see cref="ObservableReminder"/> to call the specified callback at the given time with the
        /// associated <see cref="ObservableTimedEventBase.Tag"/> object.
        /// </summary>
        /// <param name="dueTimeUtc">The due time. Must be in Utc and not <see cref="Util.UtcMinValue"/> or <see cref="Util.UtcMaxValue"/>.</param>
        /// <param name="callback">The callback method. Must not be null.</param>
        /// <param name="tag">Optional tag that will be available on event argument's <see cref="ObservableReminderEventArgs.Reminder"/>.</param>
        protected void Remind( DateTime dueTimeUtc, SafeEventHandler<ObservableReminderEventArgs> callback, object tag = null )
        {
            Domain.TimeManager.Remind( dueTimeUtc, callback, tag );
        }

        /// <summary>
        /// Gets whether this object has been disposed.
        /// </summary>
        public bool IsDisposed => Domain == null;

        /// <summary>
        /// Gets whether the domain is being deserialized.
        /// </summary>
        protected bool IsDeserializing => Domain?.IsDeserializing ?? false;

        /// <summary>
        /// Disposes this object (can be called multiple times).
        /// </summary>
        public void Dispose()
        {
            if( Domain != null )
            {
                Domain.CheckBeforeDispose( this );
                Dispose( true );
                Domain.Unregister( this );
                Domain = null;
            }
        }

        /// <summary>
        /// Called before this object is disposed.
        /// Override to dispose other objects managed by this <see cref="ObservableObject"/>, making sure to call base.
        /// Implementation at this level raises the <see cref="Disposed"/> event:
        /// it must be called by overrides.
        /// <para>
        /// Note that the Disposed event is raised only for explicit object disposing: a <see cref="ObservableDomain.Load"/> doesn't trigger the event.
        /// </para>
        /// </summary>
        /// <param name="shouldDisposeObjects">
        /// True when other <see cref="ObservableObject"/> instances managed by this object should be disposed, which is most of the time.
        /// False when managed <see cref="ObservableObject"/> instances will be disposed automatically (eg. during a reload).
        /// When False, the <see cref="Disposed"/> event is not raised.
        /// </param>
        protected internal virtual void Dispose( bool shouldDisposeObjects )
        {
            if( shouldDisposeObjects )
            {
                RaiseStandardDomainEvent( _disposed );
            }
            else
            {
                Domain = null;
            }
        }

    }

}
