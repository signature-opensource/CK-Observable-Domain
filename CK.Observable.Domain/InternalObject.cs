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
        internal ObservableDomain ActualDomain;
        internal InternalObject Next;
        internal InternalObject Prev;
        ObservableEventHandler<ObservableDomainEventArgs> _disposed;

        /// <summary>
        /// Raised when this object is <see cref="Dispose()"/>d by <see cref="Dispose(bool)"/>.
        /// Note that when the call to dispose is made by <see cref="ObservableDomain.DoLoad(IActivityMonitor, BinaryDeserializer, string, bool)"/>,
        /// this event is not triggered to avoid a useless (and potentially dangerous) snowball effect: eventually ALL <see cref="InternalObject.Dispose(bool)"/>
        /// (with a false parameter) will be called during a reload.
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
            ActualDomain = domain;
            ActualDomain.Register( this );
        }

        /// <summary>
        /// Deserialization constructor.
        /// </summary>
        /// <param name="d">The deserialization context.</param>
        protected InternalObject( IBinaryDeserializerContext d )
        {
            var (r,info) = d.StartReading();
            Debug.Assert( info.Version == 0 );
            // This enables the Internal object to be serailizable/deserializable outside a Domain
            // (for instance to use BinarySerializer.IdempotenceCheck): we really register the deserialized object
            // if and only if the available Domain service is the one being deserialized.
            ActualDomain = r.Services.GetService<ObservableDomain>( throwOnNull: true );
            if( ActualDomain == ObservableDomain.CurrentThreadDomain )
            {
                ActualDomain.Register( this );
            }
            _disposed = new ObservableEventHandler<ObservableDomainEventArgs>( r );
        }

        void Write( BinarySerializer w )
        {
            _disposed.Write( w );
        }

        /// <summary>
        /// Gets a safe view on the domain to which this internal object belongs.
        /// </summary>
        /// <remarks>
        /// Useful properties and methods (like the <see cref="DomainView.Monitor"/> or <see cref="DomainView.SendCommand"/> )
        /// are exposed by this accessor so that the interface of the observable object is not polluted by infrastructure concerns.
        /// </remarks>
        protected DomainView Domain => new DomainView( this, ActualDomain ?? throw new ObjectDisposedException( GetType().Name ) );

        /// <summary>
        /// Gets whether this object has been disposed.
        /// </summary>
        public bool IsDisposed => ActualDomain == null;

        /// <summary>
        /// Disposes this object (can be called multiple times).
        /// </summary>
        public void Dispose()
        {
            if( ActualDomain != null )
            {
                ActualDomain.CheckBeforeDispose( this );
                Dispose( true );
                ActualDomain.Unregister( this );
                ActualDomain = null;
            }
        }

        /// <summary>
        /// Called before this object is disposed.
        /// Override it to dispose other objects managed by this <see cref="ObservableObject"/> or to unregister whatever should be unregistered, but
        /// always be sure to call the base class.
        /// Implementation at this level raises the <see cref="Disposed"/> event: it must be called by overrides.
        /// <para>
        /// Note that the Disposed event is raised only for explicit object disposing:
        /// a <see cref="ObservableDomain.Load(IActivityMonitor, System.IO.Stream, bool, System.Text.Encoding, int, bool)"/> doesn't trigger the event.
        /// </para>
        /// </summary>
        /// <param name="shouldDisposeObjects">
        /// True when other <see cref="ObservableObject"/> instances managed by this object should be disposed, which is most of the time.
        /// False when managed <see cref="ObservableObject"/> instances will be disposed automatically (eg. during a reload).
        /// When False, the <see cref="Disposed"/> event is not raised.
        /// </param>
        protected internal virtual void Dispose( bool shouldDisposeObjects )
        {
            // Since this method id protected and can be called by external code,
            // we (safely) check that we are not already disposed.
            if( shouldDisposeObjects && ActualDomain != null )
            {
                _disposed.Raise( this, ActualDomain.DefaultEventArgs );
            }
            else
            {
                ActualDomain = null;
            }
        }

    }

}
