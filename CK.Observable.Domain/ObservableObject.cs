using CK.Core;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;

namespace CK.Observable
{
    /// <summary>
    /// Base class for all observable object.
    /// Observable objects are reference types that belong to a <see cref="ObservableDomain"/> and for
    /// which properties changes and <see cref="Dispose"/> are tracked.
    /// </summary>
    [SerializationVersion( 0 )]
    public abstract class ObservableObject : INotifyPropertyChanged, IDisposableObject, IKnowMyExportDriver
    {
        int _id;
        internal readonly ObservableDomain Domain;
        PropertyChangedEventHandler _handler;
        internal int OId => _id;
        internal readonly IObjectExportTypeDriver _exporter;
        readonly ObservableEventHandler<EventMonitoredArgs> _disposed;

        /// <summary>
        /// Raised when this object is <see cref="Dispose"/>d by <see cref="OnDisposed"/>.
        /// Note that when the call to dispose is made by <see cref="ObservableDomain.Load"/>, this event is not
        /// triggered to avoid a useless (and potentialy dangerous) snowball effect: eventually ALL <see cref="ObservableObject.OnDisposed(bool)"/>
        /// will be called during a reload.
        /// </summary>
        public event EventHandler<EventMonitoredArgs> Disposed
        {
            add
            {
                if( IsDisposed ) throw new ObjectDisposedException( ToString() );
                _disposed.Add( value, nameof( Disposed ) );
            }
            remove => _disposed.Remove( value );
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
            _id = Domain.Register( this );
            Debug.Assert( _id >= 0 );
        }

        /// <summary>
        /// Deserialization constructor.
        /// </summary>
        /// <param name="d">The deserialization context.</param>
        protected ObservableObject( IBinaryDeserializerContext d )
        {
            var r = d.StartReading();
            Debug.Assert( r.CurrentReadInfo.Version == 0 );
            Domain = r.Services.GetService<ObservableDomain>( throwOnNull: true );
            _exporter = Domain._exporters.FindDriver( GetType() );
            _id = r.ReadInt32();
            Debug.Assert( _id >= 0 );
        }

        void Write( BinarySerializer w )
        {
            w.Write( _id );
        }

        event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged
        {
            add { _handler += value; }
            remove { _handler -= value; }
        }

        /// <summary>
        /// Gives access to the monitor to use.
        /// </summary>
        protected IActivityMonitor Monitor => Domain.CurrentMonitor;

        /// <summary>
        /// Gets whether this object has been disposed.
        /// </summary>
        [NotExportable]
        public bool IsDisposed => _id < 0;

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
            if( _id >= 0 )
            {
                Domain.CheckBeforeDispose( this );
                OnDisposed( new EventMonitoredArgs( Domain.CurrentMonitor ), false );
                Domain.Unregister( this );
                _id = -1;
            }
        }

        /// <summary>
        /// Sends a command to the external world. Commands are enlisted
        /// into <see cref="TransactionResult.Commands"/> (when the transaction succeeds)
        /// and can be processed by any <see cref="IObservableDomainClient"/>.
        /// </summary>
        /// <param name="command">Any command description.</param>
        public void SendCommand( object command )
        {
            Domain.SendCommand( this, command );
        }

        /// <summary>
        /// Called before this object is disposed.
        /// Implementation at this level raises the <see cref="Disposed"/> event: it must be called
        /// by overrides.
        /// <para>
        /// Note that the Disposed event is raised only for explicit object disposing: a <see cref="ObservableDomain.Load"/> doesn't trigger the event.
        /// </para>
        /// </summary>
        /// <param name="reusableArgs">
        /// The event arguments that exposes the monitor to use (that is the same as this <see cref="Monitor"/> protected property).
        /// </param>
        /// <param name="isReloading">
        /// True when this dispose is due to a domain reload. (When true the <see cref="Disposed"/> event is not raised.)
        /// </param>
        protected internal virtual void OnDisposed( EventMonitoredArgs reusableArgs, bool isReloading )
        {
            if( isReloading )
            {
                _id = -1;
            }
            else
            {
                _disposed.Raise( reusableArgs.Monitor, this, reusableArgs, nameof(Disposed) );
            }
        }

        /// <summary>
        /// Automatically called by (currently) Fody.
        /// This method captures the change and routes it to the domain. 
        /// </summary>
        /// <param name="propertyName">The name of the changed property.</param>
        /// <param name="before">The property previous value.</param>
        /// <param name="after">The new property value.</param>
        public virtual void OnPropertyChanged( string propertyName, object before, object after )
        {
            if( !IsDisposed )
            {
                var ev = Domain.OnPropertyChanged( this, propertyName, before, after );
                if( ev != null )
                {
                    // Handles public event EventHandler <<propertyName>>Changed;
                    // See https://stackoverflow.com/questions/14885325/eventinfo-getraisemethod-always-null
                    FieldInfo fNamedEv = GetType().GetField( propertyName + "Changed", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance );
                    if( fNamedEv != null )
                    {
                        if( fNamedEv.FieldType != typeof( EventHandler ) )
                        {
                            throw new Exception( $"Changed event must be typed as mere EventHandler (PropertyName = {propertyName})." );
                        }
                        ((EventHandler)fNamedEv.GetValue( this ))?.Invoke( this, EventArgs.Empty );
                    }
                    // OnPropertyChanged (by name).
                    _handler?.Invoke( this, ev );
                }
            }
        }


    }

}
