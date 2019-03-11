

using CK.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;

namespace CK.Observable
{
    [SerializationVersionAttribute(0)]
    public abstract class ObservableObject : INotifyPropertyChanged, IDisposable, IKnowMyExportDriver
    {
        int _id;
        internal readonly ObservableDomain Domain;
        PropertyChangedEventHandler _handler;
        internal int OId => _id;
        internal readonly IObjectExportTypeDriver _exporter;

        protected ObservableObject()
            : this( ObservableDomain.GetCurrentActiveDomain() )
        {
        }

        protected ObservableObject( ObservableDomain domain )
        {
            if( domain == null ) throw new ArgumentNullException( nameof(domain) );
            Domain = domain;
            _exporter = Domain._exporters.FindDriver( GetType() );
            _id = Domain.Register( this );
            Debug.Assert( _id >= 0 );
        }

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

        protected IActivityMonitor DomainMonitor => Domain.Monitor;

        [NotExportable]
        public bool IsDisposed => _id < 0;

        protected bool IsDeserializing => Domain.IsDeserializing;

        internal virtual ObjectExportedKind ExportedKind => ObjectExportedKind.Object;

        IObjectExportTypeDriver IKnowMyExportDriver.ExportDriver => _exporter;

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
        /// </summary>
        protected virtual void OnDisposed()
        {
        }

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
