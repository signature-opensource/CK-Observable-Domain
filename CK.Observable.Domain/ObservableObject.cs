using CK.Core;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace CK.Observable
{
    /// <summary>
    /// Base class for all observable object.
    /// Observable objects are reference types that belong to a <see cref="ObservableDomain"/> and for
    /// which properties changes and <see cref="Dispose()"/> are tracked.
    /// </summary>
    /// <remarks>
    /// </remarks>
    [SerializationVersion( 1 )]
    public abstract partial class ObservableObject : INotifyPropertyChanged, IDisposableObject, IKnowMyExportDriver
    {
        ObservableObjectId _oid;
        internal readonly ObservableDomain Domain;
        internal readonly IObjectExportTypeDriver _exporter;
        ObservableEventHandler<ObservableDomainEventArgs> _disposed;
        ObservableEventHandler<PropertyChangedEventArgs> _propertyChanged;

        /// <summary>
        /// Raised when this object is <see cref="Dispose()"/>d by the <see cref="Dispose(bool)"/> overload.
        /// Note that when the call to dispose is made by <see cref="ObservableDomain.Load(IActivityMonitor, System.IO.Stream, bool, System.Text.Encoding, int, bool)"/>, this event is not
        /// triggered to avoid a useless (and potentially dangerous) snowball effect: eventually ALL <see cref="ObservableObject.Dispose(bool)"/>
        /// will be called during a reload.
        /// </summary>
        public event SafeEventHandler<ObservableDomainEventArgs> Disposed
        {
            add
            {
                if( IsDisposed ) throw new ObjectDisposedException( ToString() );
                _disposed.Add( value, nameof( Disposed ) );
            }
            remove => _disposed.Remove( value );
        }

        /// <summary>
        /// Generic property changed safe event that can be used to track any change on observable properties (by name).
        /// This uses the standard <see cref="PropertyChangedEventArgs"/> event.
        /// </summary>
        public event SafeEventHandler<PropertyChangedEventArgs> PropertyChanged
        {
            add
            {
                if( IsDisposed ) throw new ObjectDisposedException( ToString() );
                _propertyChanged.Add( value, nameof( PropertyChanged ) );
            }
            remove => _propertyChanged.Remove( value );
        }

        event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged
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
        protected ObservableObject( ObservableDomain domain )
        {
            if( domain == null ) throw new ArgumentNullException( nameof( domain ) );
            Domain = domain;
            _exporter = Domain._exporters.FindDriver( GetType() );
            _oid = Domain.Register( this );
        }

        /// <summary>
        /// Deserialization constructor.
        /// </summary>
        /// <param name="d">The deserialization context.</param>
        protected ObservableObject( IBinaryDeserializerContext d )
        {
            var r = d.StartReading();
            Domain = r.Services.GetService<ObservableDomain>( throwOnNull: true );
            _exporter = Domain._exporters.FindDriver( GetType() );
            _oid = new ObservableObjectId( r );
            _disposed = new ObservableEventHandler<ObservableDomainEventArgs>( r );
            _propertyChanged = new ObservableEventHandler<PropertyChangedEventArgs>( r );
        }

        void Write( BinarySerializer w )
        {
            _oid.Write( w );
            _disposed.Write( w );
            _propertyChanged.Write( w );
        }

        /// <summary>
        /// Gives access to the monitor to use.
        /// </summary>
        protected IActivityMonitor Monitor => Domain.CurrentMonitor;

        /// <summary>
        /// Gets whether this object has been disposed.
        /// </summary>
        [NotExportable]
        public bool IsDisposed => _oid == ObservableObjectId.Disposed;

        /// <summary>
        /// Gets the unique identifier of this observable object.
        /// This identifier is composed of its internal index and a uniquifier (see <see cref="ObservableObjectId"/> struct).
        /// </summary>
        public ObservableObjectId OId => _oid;

        /// <summary>
        /// Gets whether the domain is being deserialized.
        /// </summary>
        protected bool IsDeserializing => Domain.IsDeserializing;

        internal virtual ObjectExportedKind ExportedKind => ObjectExportedKind.Object;

        IObjectExportTypeDriver IKnowMyExportDriver.ExportDriver => _exporter;

        /// <summary>
        /// Disposes this object (can be called multiple times).
        /// </summary>
        public void Dispose()
        {
            if( _oid.IsValid )
            {
                Domain.CheckBeforeDispose( this );
                Dispose( true );
                Domain.Unregister( this );
                _oid = ObservableObjectId.Disposed;
            }
        }

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
        /// Helper that raises a standard event from this object with a reusable <see cref="ObservableDomainEventArgs"/> instance.
        /// </summary>
        protected void RaiseStandardDomainEvent( ObservableEventHandler<ObservableDomainEventArgs> h ) => h.Raise( this, Domain.DefaultEventArgs );

        /// <summary>
        /// Helper that raises a standard event from this object with a reusable <see cref="EventMonitoredArgs"/> instance (that is the
        /// shared <see cref="ObservableDomainEventArgs"/> instance).
        /// </summary>
        protected void RaiseStandardDomainEvent( ObservableEventHandler<EventMonitoredArgs> h ) => h.Raise( this, Domain.DefaultEventArgs );

        /// <summary>
        /// Uses a pooled <see cref="ObservableReminder"/> to call the specified callback at the given time with the
        /// associated <see cref="ObservableTimedEventBase.Tag"/> object.
        /// </summary>
        /// <param name="dueTimeUtc">The due time. Must be in Utc and not <see cref="Util.UtcMinValue"/> or <see cref="Util.UtcMaxValue"/>.</param>
        /// <param name="callback">The callback method. Must not be null.</param>
        /// <param name="tag">Optional tag that will be available on event argument's: <see cref="ObservableTimedEventBase.Tag"/>.</param>
        protected void Remind( DateTime dueTimeUtc, SafeEventHandler<ObservableReminderEventArgs> callback, object? tag = null )
        {
            Domain.TimeManager.Remind( dueTimeUtc, callback, tag );
        }

        /// <summary>
        /// Called before this object is disposed.
        /// Override it to dispose other objects managed by this <see cref="ObservableObject"/> or to remove this object from any
        /// knwon containers for instance.
        /// <para>
        /// Please make sure to call base implementation since, at this level, it raises
        /// the <see cref="Disposed"/> event.
        /// </para>
        /// <para>
        /// Note that the Disposed event is raised only for explicit object disposing, ie. when <paramref name="shouldCleanup"/> is true:
        /// a <see cref="ObservableDomain.Load(IActivityMonitor, System.IO.Stream, bool, System.Text.Encoding, int, bool)"/> doesn't trigger
        /// the event (shouldCleanup is false).
        /// </para>
        /// </summary>
        /// <param name="shouldCleanup">
        /// True when other <see cref="ObservableObject"/> instances managed by this object should be disposed, which is most of the time.
        /// False when managed <see cref="ObservableObject"/> instances will be disposed automatically (eg. during a reload).
        /// When False, the <see cref="Disposed"/> event is not raised.
        /// </param>
        protected internal virtual void Dispose( bool shouldCleanup )
        {
            if( shouldCleanup )
            {
                RaiseStandardDomainEvent( _disposed );
            }
            else
            {
                _oid = ObservableObjectId.Disposed;
            }
        }

        /// <summary>
        /// Automatically called by (currently) Fody.
        /// This method captures the change and routes it to the domain. 
        /// </summary>
        /// <param name="propertyName">The name of the changed property.</param>
        /// <param name="before">The property previous value.</param>
        /// <param name="after">The new property value.</param>
        protected virtual void OnPropertyChanged( string propertyName, object before, object after )
        {
            this.CheckDisposed();
            var ev = Domain.OnPropertyChanged( this, propertyName, before, after );
            if( ev != null )
            {
                // Handles public event EventHandler [propertyName]Changed;
                // See https://stackoverflow.com/questions/14885325/eventinfo-getraisemethod-always-null
                {
                    FieldInfo fNamedEv = GetType().GetField( propertyName + "Changed", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance );
                    if( fNamedEv != null )
                    {
                        if( fNamedEv.FieldType != typeof( EventHandler ) )
                        {
                            throw new Exception( $"{propertyName}Changed event must be a mere EventHandler (not a {fNamedEv.FieldType.Name})." );
                        }
                        ((EventHandler)fNamedEv.GetValue( this ))?.Invoke( this, EventArgs.Empty );
                    }
                }
                {
                    var fieldName = "_" + Char.ToLowerInvariant( propertyName[0] ) + propertyName.Substring( 1 ) + "Changed";
                    FieldInfo fNamedEv = GetType().GetField( fieldName, BindingFlags.NonPublic | BindingFlags.Instance );
                    if( fNamedEv != null )
                    {
                        if( fNamedEv.FieldType == typeof( ObservableEventHandler ) )
                        {
                            var handler = (ObservableEventHandler)fNamedEv.GetValue( this );
                            if( handler.HasHandlers ) handler.Raise( this, ev );
                        }
                        else if( fNamedEv.FieldType == typeof( ObservableEventHandler<ObservableDomainEventArgs> ) )
                        {
                            var handler = (ObservableEventHandler<ObservableDomainEventArgs>)fNamedEv.GetValue( this );
                            if( handler.HasHandlers ) RaiseStandardDomainEvent( handler );
                        }
                        else if( fNamedEv.FieldType == typeof( ObservableEventHandler<EventMonitoredArgs> ) )
                        {
                            var handler = (ObservableEventHandler<EventMonitoredArgs>)fNamedEv.GetValue( this );
                            if( handler.HasHandlers ) RaiseStandardDomainEvent( handler );
                        }
                        else if( fNamedEv.FieldType == typeof( ObservableEventHandler<EventArgs> ) )
                        {
                            var handler = (ObservableEventHandler<EventArgs>)fNamedEv.GetValue( this );
                            if( handler.HasHandlers ) handler.Raise( this, ev );
                        }
                        else
                        {
                            throw new Exception( $"{propertyName}Changed implementation _{propertyName.ToLowerInvariant()}Changed must be a ObservableEventHandler, a ObservableEventHandler<EventMonitoredArgs> or a ObservableEventHandler<ObservableDomainEventArgs>." );
                        }
                    }
                }
                // OnPropertyChanged (by name).
                if( _propertyChanged.HasHandlers ) _propertyChanged.Raise( this, ev );
            }
        }
        


    }

}
