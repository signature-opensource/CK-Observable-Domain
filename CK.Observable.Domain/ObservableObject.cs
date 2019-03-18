

using CK.Core;
using System;
using System.Collections.Generic;
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
    [SerializationVersion(0)]
    public abstract class ObservableObject : INotifyPropertyChanged, IDisposable, IKnowMyExportDriver
    {
        int _id;
        internal readonly ObservableDomain Domain;
        PropertyChangedEventHandler _handler;
        internal int OId => _id;
        internal readonly IObjectExportTypeDriver _exporter;

        /// <summary>
        /// Raised when this object is <see cref="Dispose"/>d by <see cref="OnDisposed"/>.
        /// </summary>
        public event EventHandler Disposed;

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
            if( domain == null ) throw new ArgumentNullException( nameof(domain) );
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
        /// Gives access to the 
        /// </summary>
        protected IActivityMonitor DomainMonitor => Domain.Monitor;

        /// <summary>
        /// Gets whether this object has been disposed.
        /// </summary>
        [NotExportable]
        public bool IsDisposed => _id < 0;

        /// <summary>
        /// Gets whether the odomain is being deserialized.
        /// </summary>
        protected bool IsDeserializing => Domain.IsDeserializing;

        internal virtual ObjectExportedKind ExportedKind => ObjectExportedKind.Object;

        IObjectExportTypeDriver IKnowMyExportDriver.ExportDriver => _exporter;

        /// <summary>
        /// Disposes this object (if not already disposed).
        /// </summary>
        public void Dispose()
        {
            if( _id >= 0 )
            {
                OnDisposed();
                Domain.Unregister( this );
                _id = -1;
            }
        }

        /// <summary>
        /// Called before this object is disposed.
        /// Implementation at this level raises the <see cref="Disposed"/> event: it must be called
        /// by overrides.
        /// </summary>
        protected virtual void OnDisposed()
        {
            Disposed?.Invoke( this, EventArgs.Empty );
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
                    FieldInfo fNamedEv = GetType().GetField( propertyName + "Changed", BindingFlags.NonPublic | BindingFlags.Instance );
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
