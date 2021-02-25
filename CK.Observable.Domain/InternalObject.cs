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
    /// as <see cref="IDestroyableObject"/>, can implement events and event handler (<see cref="SafeEventHandler"/>
    /// and <see cref="SafeEventHandler{TEventArgs}"/>).
    /// </summary>
    [NotExportable]
    [SerializationVersion( 0 )]
    public abstract class InternalObject : IDestroyableObject
    {
        internal ObservableDomain ActualDomain;
        internal InternalObject Next;
        internal InternalObject Prev;
        ObservableEventHandler<ObservableDomainEventArgs> _destroyed;

        /// <summary>
        /// Raised when this object is <see cref="Destroy()">destroyed</see>.
        /// </summary>
        public event SafeEventHandler<ObservableDomainEventArgs> Destroyed
        {
            add
            {
                this.CheckDestroyed();
                _destroyed.Add( value, nameof( Destroyed ) );
            }
            remove => _destroyed.Remove( value );
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

        protected InternalObject( RevertSerialization _ ) { }

        InternalObject( IBinaryDeserializer r, TypeReadInfo? info )
        {
            Debug.Assert( info != null && info.Version == 0 );
            if( r.ReadBoolean() )
            {
                // This enables the Internal object to be serializable/deserializable outside a Domain
                // (for instance to use BinarySerializer.IdempotenceCheck): the domain registers it.
                ActualDomain = r.Services.GetService<ObservableDomain>( throwOnNull: true );
                _destroyed = new ObservableEventHandler<ObservableDomainEventArgs>( r );
            }
            else
            {
                Debug.Assert( IsDestroyed );
            }
        }

        void Write( BinarySerializer w )
        {
            if( IsDestroyed ) w.Write( false );
            else
            {
                w.Write( true );
                _destroyed.Write( w );
            }
        }

        /// <summary>
        /// Gets a safe view on the domain to which this internal object belongs.
        /// </summary>
        /// <remarks>
        /// Useful properties and methods (like the <see cref="DomainView.Monitor"/> or <see cref="DomainView.SendCommand"/> )
        /// are exposed by this accessor so that the interface of the observable object is not polluted by infrastructure concerns.
        /// </remarks>
        protected DomainView Domain => new DomainView( this, ActualDomain ?? throw new ObjectDestroyedException( GetType().Name ) );

        /// <summary>
        /// Gets whether this object has been disposed.
        /// </summary>
        public bool IsDestroyed => ActualDomain == null;

        /// <summary>
        /// Destroys this object (can be called multiple times).
        /// </summary>
        public void Destroy()
        {
            if( ActualDomain != null )
            {
                ActualDomain.CheckBeforeDestroy( this );
                OnUnload();
                OnDestroy();
                ActualDomain.Unregister( this );
                ActualDomain = null;
            }
        }

        /// <summary>
        /// Called before this object is destroyed.
        /// Override it to destroy other objects managed by this <see cref="InternalObject"/> or to remove this
        /// object from any known containers.
        /// <para>
        /// Please make sure to call this base implementation since, at this level, it raises the <see cref="Destroyed"/> event
        /// (the base method should typically be called after having impacted other objects).
        /// </para>
        /// </summary>
        protected internal virtual void OnDestroy()
        {
            _destroyed.Raise( this, ActualDomain.DefaultEventArgs );
        }

        internal void Unload()
        {
            if( ActualDomain != null )
            {
                OnUnload();
                ActualDomain = null;
            }
        }

        /// <summary>
        /// Called when this object is unloaded: either because the <see cref="ObservableDomain"/> is disposed
        /// or <see cref="ObservableDomain.Load(IActivityMonitor, System.IO.Stream, bool, System.Text.Encoding, int, bool)"/>
        /// has been called or <see cref="Destroy"/> is being called (this is called prior to call <see cref="OnDestroy"/>).
        /// <para>
        /// This base is an empty implementation (we have nothing to do at this level). This must be overridden whenever
        /// external resources are used, such as opened files for instance.
        /// </para>
        /// </summary>
        protected internal virtual void OnUnload()
        {
        }

    }

}
