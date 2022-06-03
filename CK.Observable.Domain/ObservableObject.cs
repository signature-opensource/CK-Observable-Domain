using CK.Core;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace CK.Observable
{
    /// <summary>
    /// Base class for all observable object.
    /// Observable objects are reference types that belong to a <see cref="ObservableDomain"/> and for
    /// which properties changes and <see cref="Destroy()"/> are tracked.
    /// </summary>
    [SerializationVersion( 1 )]
    public abstract partial class ObservableObject : INotifyPropertyChanged, IKnowMyExportDriver, IDestroyableObject, BinarySerialization.ICKSlicedSerializable
    {
        ObservableObjectId _oid;
        internal readonly ObservableDomain ActualDomain;
        internal readonly IObjectExportTypeDriver _exporter;
        ObservableEventHandler<ObservableDomainEventArgs> _destroyed;
        ObservableEventHandler<PropertyChangedEventArgs> _propertyChanged;

        /// <summary>
        /// Raised when this object is <see cref="Destroy">destroyed</see>.
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
        /// Generic property changed safe event that can be used to track any change on observable properties (by name).
        /// This uses the standard <see cref="PropertyChangedEventArgs"/> event.
        /// </summary>
        public event SafeEventHandler<PropertyChangedEventArgs> PropertyChanged
        {
            add
            {
                this.CheckDestroyed();
                _propertyChanged.Add( value, nameof( PropertyChanged ) );
            }
            remove => _propertyChanged.Remove( value );
        }

        event PropertyChangedEventHandler? INotifyPropertyChanged.PropertyChanged
        {
            add
            {
                throw new NotSupportedException( "INotifyPropertyChanged is supported only because PropertyChanged.Fody requires it. It must not be used." );
            }
            remove
            {
                throw new NotSupportedException( "INotifyPropertyChanged is supported only because PropertyChanged.Fody requires it. It must not be used." );
            }
        }

        /// <summary>
        /// Constructor for specialized instance.
        /// The current domain is retrieved automatically: it is the last one on the current thread
        /// that has started a transaction (see <see cref="ObservableDomain.BeginTransaction"/>).
        /// </summary>
        protected ObservableObject()
            : this( ObservableDomain.GetCurrentActiveDomain() )
        {
        }

        /// <summary>
        /// Constructor for specialized instance with an explicit <see cref="ObservableDomain"/>.
        /// </summary>
        /// <param name="domain">The domain to which this object belong.</param>
        ObservableObject( ObservableDomain domain )
        {
            Throw.CheckNotNullArgument( domain );
            ActualDomain = domain;
            _exporter = ActualDomain._exporters.FindDriver( GetType() );
            _oid = ActualDomain.Register( this );
        }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        protected ObservableObject( BinarySerialization.Sliced _ ) { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        ObservableObject( BinarySerialization.IBinaryDeserializer d, BinarySerialization.ITypeReadInfo info )
        {
            _oid = new ObservableObjectId( d.Reader );
            if( !IsDestroyed )
            {
                // This enables the Observable object to be serializable/deserializable outside a Domain
                // (for instance to use BinarySerializer.IdempotenceCheck): we really register the deserialized object
                // if and only if the available Domain service is the one being deserialized.
                var domain = d.Context.Services.GetService<ObservableDomain>( throwOnNull: true );
                if( (ActualDomain = domain) == ObservableDomain.CurrentThreadDomain )
                {
                    domain.SideEffectsRegister( this );
                }
                _exporter = domain._exporters.FindDriver( GetType() );
                _destroyed = new ObservableEventHandler<ObservableDomainEventArgs>( d );
                _propertyChanged = new ObservableEventHandler<PropertyChangedEventArgs>( d );
            }
        }


        public static void Write( BinarySerialization.IBinarySerializer s, in ObservableObject o )
        {
            o._oid.Write( s.Writer );
            if( !o.IsDestroyed )
            {
                o._destroyed.Write( s );
                o._propertyChanged.Write( s );
            }
        }

        /// <inheritdoc />
        public bool IsDestroyed => _oid == ObservableObjectId.Destroyed;

        /// <summary>
        /// Gets the unique identifier of this observable object.
        /// This identifier is composed of its internal index and a uniquifier (see <see cref="ObservableObjectId"/> struct).
        /// </summary>
        public ObservableObjectId OId => _oid;

        /// <summary>
        /// Gets a safe view on the domain to which this object belongs.
        /// </summary>
        /// <remarks>
        /// Useful properties and methods (like the <see cref="DomainView.Monitor"/> or <see cref="DomainView.SendCommand"/> )
        /// are exposed by this accessor so that the interface of the observable object is not polluted by infrastructure concerns.
        /// </remarks>
        protected DomainView Domain => new DomainView( this, ActualDomain );

        internal virtual ObjectExportedKind ExportedKind => ObjectExportedKind.Object;

        IObjectExportTypeDriver IKnowMyExportDriver.ExportDriver => _exporter;

        /// <summary>
        /// Destroys this object (can be called multiple times).
        /// Further attempts to interact with this object will throw <see cref="ObjectDestroyedException"/>.
        /// </summary>
        /// <remarks>
        /// When this object is not already destroyed, first <see cref="OnUnload"/> is called and then <see cref="OnDestroy"/>.
        /// </remarks>
        public void Destroy()
        {
            if( _oid.IsValid )
            {
                if( this is ObservableRootObject
                    && (!ObservableRootObject.AllowRootObjectDestroying || ActualDomain.AllRoots.IndexOf( x => x == this ) >= 0) )
                {
                    Throw.InvalidOperationException( "ObservableRootObject cannot be disposed." );
                }
                ActualDomain.CheckBeforeDestroy( this );
                OnUnload();
                OnDestroy();
                ActualDomain.Unregister( this );
                _oid = ObservableObjectId.Destroyed;
            }
        }

        /// <summary>
        /// Called before this object is destroyed.
        /// Override it to destroy other objects managed by this <see cref="ObservableObject"/> or to remove this
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

        /// <summary>
        /// This is called by the <see cref="ObservableDomain.Load(IActivityMonitor, System.IO.Stream, bool, System.Text.Encoding?, int, bool?)"/>
        /// or <see cref="ObservableDomain.DoDispose(IActivityMonitor)"/> and by <see cref="ObservableDomain.GarbageCollectAsync"/>.
        /// </summary>
        /// <param name="gc">True when called by the garbage collector: this object is unregistered.</param>
        internal void Unload( bool gc )
        {
            Debug.Assert( !IsDestroyed );
            OnUnload();
            if( gc ) ActualDomain.Unregister( this );
            _oid = ObservableObjectId.Destroyed;
        }

        /// <summary>
        /// Called when this object is unloaded: either because the <see cref="ObservableDomain"/> is disposed
        /// or <see cref="ObservableDomain.Load(IActivityMonitor, BinarySerialization.RewindableStream, int, bool?)"/>
        /// has been called or <see cref="Destroy"/> is being called (this is called prior to call <see cref="OnDestroy"/>).
        /// <para>
        /// This base is an empty implementation (we have nothing to do at this level). This must be overridden whenever
        /// external resources are used, such as opened files for instance.
        /// </para>
        /// </summary>
        protected internal virtual void OnUnload()
        {
        }

        /// <summary>
        /// Automatically called by (currently) Fody.
        /// The before value is unused (we don't track no-change).
        /// </summary>
        /// <param name="propertyName">The name of the changed property.</param>
        /// <param name="before">The property previous value.</param>
        /// <param name="after">The new property value.</param>
        protected void OnPropertyChanged( string propertyName, object? before, object? after ) => OnPropertyChanged( propertyName, after );

        /// <summary>
        /// Must be called (or is automatically called if Fody is used).
        /// This method captures the change and routes it to the domain. 
        /// </summary>
        /// <param name="propertyName">The name of the changed property.</param>
        /// <param name="value">The new property value.</param>
        protected virtual void OnPropertyChanged( string propertyName, object? value )
        {
            this.CheckDestroyed();
            var ev = ActualDomain.OnPropertyChanged( this, propertyName, value );
            if( ev != null )
            {
                // Is this really useful?
                // Is this even used?
                // 
                // The idea was that for any property:
                //
                //   public string Prop { get; set; }
                //
                // A Changed handler like this:
                //
                //     ObservableEventHandler _propChanged; // (name here DOES matter! '_' + camelCased property name + "Changed" )
                //
                //     public event SafeEventHandler&lt;MyEventArgs&gt; OnPChange // (name here doesn't matter)
                //     {
                //         add => _propChanged.Add( value );
                //         remove => _propChanged.Remove( value );
                //     }
                //
                // The event is automatically raised when the Prop changes.
                //
                // Code below is awful... Even if it can be optimized (at the price of some memory), this is terrible...
                //
                {
                    // Forbids previously (in initial ObservableDomain versions) handled EventHandler: this is too dangerous.
                    // See https://stackoverflow.com/questions/14885325/eventinfo-getraisemethod-always-null
                    FieldInfo fNamedEv = GetType().GetField( propertyName + "Changed", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance );
                    if( fNamedEv != null )
                    {
                        throw new Exception( $"{propertyName}Changed event must be a SafeEventHandler (not a {fNamedEv.FieldType.Name})." );
                    }
                }
                {
                    var fieldName = "_" + Char.ToLowerInvariant( propertyName[0] ) + propertyName.Substring( 1 ) + "Changed";

                    Type t = GetType();
                    FieldInfo? fNamedEv;
                    while( (fNamedEv = t.GetField( fieldName, BindingFlags.NonPublic | BindingFlags.Instance )) == null
                            && (t = t.BaseType) != null ) ;

                    if( fNamedEv != null )
                    {
                        if( fNamedEv.FieldType == typeof( ObservableEventHandler ) )
                        {
                            var handler = (ObservableEventHandler)fNamedEv.GetValue( this );
                            if( handler.HasHandlers ) handler.Raise( this );
                        }
                        else if( fNamedEv.FieldType == typeof( ObservableEventHandler<ObservableDomainEventArgs> ) )
                        {
                            var handler = (ObservableEventHandler<ObservableDomainEventArgs>)fNamedEv.GetValue( this );
                            if( handler.HasHandlers ) handler.Raise( this, ActualDomain.DefaultEventArgs );
                        }
                        else if( fNamedEv.FieldType == typeof( ObservableEventHandler<EventMonitoredArgs> ) )
                        {
                            var handler = (ObservableEventHandler<EventMonitoredArgs>)fNamedEv.GetValue( this );
                            if( handler.HasHandlers ) handler.Raise( this, ActualDomain.DefaultEventArgs );
                        }
                        else if( fNamedEv.FieldType == typeof( ObservableEventHandler<EventArgs> ) )
                        {
                            var handler = (ObservableEventHandler<EventArgs>)fNamedEv.GetValue( this );
                            if( handler.HasHandlers ) handler.Raise( this, ev );
                        }
                        else
                        {
                            throw new Exception( $"{propertyName}Changed implementation {fieldName} must be a ObservableEventHandler, a ObservableEventHandler<EventMonitoredArgs> or a ObservableEventHandler<ObservableDomainEventArgs>." );
                        }
                    }
                }
                // OnPropertyChanged (by name).
                if( _propertyChanged.HasHandlers ) _propertyChanged.Raise( this, ev );
            }
        }
        


    }

}
