using CK.Core;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;

namespace CK.Observable
{
    /// <summary>
    /// Base class for all internal object.
    /// Internal objects are like <see cref="ObservableObject"/> but are not exportable (only serializable) and,
    /// as <see cref="IDestroyable"/>, can implement events and event handler (<see cref="SafeEventHandler"/>
    /// and <see cref="SafeEventHandler{TEventArgs}"/>).
    /// </summary>
    [NotExportable]
    [SerializationVersion( 0 )]
    public abstract class InternalObject : IDestroyableObject, BinarySerialization.ICKSlicedSerializable
    {
        internal ObservableDomain? ActualDomain;
        internal InternalObject? Next;
        internal InternalObject? Prev;
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
            Throw.CheckNotNullArgument( domain );
            ActualDomain = domain;
            ActualDomain.Register( this );
        }

        protected InternalObject( BinarySerialization.Sliced _ ) { }

        InternalObject( BinarySerialization.IBinaryDeserializer d, BinarySerialization.ITypeReadInfo info )
        {
            if( d.Reader.ReadBoolean() )
            {
                // This enables the Internal object to be serializable/deserializable outside a Domain
                // (for instance to use BinarySerializer.IdempotenceCheck): the domain registers it.
                ActualDomain = d.Context.Services.GetRequiredService<ObservableDomain>();
                _destroyed = new ObservableEventHandler<ObservableDomainEventArgs>( d );
                // We don't call Register here: this is called by the domain deserializer method.
            }
            else
            {
                Debug.Assert( IsDestroyed );
            }
        }

        public static void Write( BinarySerialization.IBinarySerializer s, in InternalObject o )
        {
            if( o.IsDestroyed ) s.Writer.Write( false );
            else
            {
                s.Writer.Write( true );
                o._destroyed.Write( s );
            }
        }

        /// <summary>
        /// Gets a safe view on the domain to which this internal object belongs.
        /// </summary>
        /// <remarks>
        /// Useful properties and methods (like the <see cref="DomainView.Monitor"/> or <see cref="DomainView.SendCommand"/> )
        /// are exposed by this accessor so that the interface of the observable object is not polluted by infrastructure concerns.
        /// </remarks>
        protected DomainView Domain
        {
            get
            {
                if( ActualDomain == null ) DestroyableObjectExtensions.ThrowObjectDestroyedException( GetType().Name );
                return new DomainView( this, ActualDomain! );
            }
        }

        /// <summary>
        /// Gets whether this object has been disposed.
        /// </summary>
        public bool IsDestroyed => ActualDomain == null;

        /// <summary>
        /// Destroys this object (can be called multiple times).
        /// Further attempts to interact with this object will throw <see cref="ObjectDestroyedException"/>.
        /// </summary>
        /// <remarks>
        /// When this object is not already destroyed, first <see cref="OnUnload"/> is called and then <see cref="OnDestroy"/>.
        /// </remarks>
        public void Destroy()
        {
            if( ActualDomain != null )
            {
                ActualDomain.CheckBeforeDestroy( this );
                OnUnload();
                OnDestroy();
                ActualDomain.Unregister( this );
                ActualDomain = null!;
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
            Debug.Assert( ActualDomain != null );
            _destroyed.Raise( this, ActualDomain.DefaultEventArgs );
        }

        internal void Unload( bool gc )
        {
            if( ActualDomain != null )
            {
                OnUnload();
                if( gc ) ActualDomain.Unregister( this );
                ActualDomain = null;
            }
        }

        /// <summary>
        /// Called when this object is unloaded: either because the <see cref="ObservableDomain"/> is disposed
        /// or <see cref="ObservableDomain.Load(IActivityMonitor, System.IO.Stream, bool, System.Text.Encoding?, int, bool?)"/>
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
