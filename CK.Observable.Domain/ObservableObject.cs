

using CK.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;

namespace CK.Observable
{
    [SerializationVersionAttribute(0)]
    public abstract class ObservableObject : INotifyPropertyChanged, IDisposable
    {
        int _id;
        internal readonly ObservableDomain Domain;
        PropertyChangedEventHandler _handler;
        internal int OId => _id;

        protected ObservableObject()
            : this( ObservableDomain.GetCurrentActiveDomain() )
        {
        }


        protected ObservableObject( ObservableDomain domain )
        {
            if( domain == null ) throw new ArgumentNullException( nameof(domain) );
            Domain = domain;
            _id = Domain.Register( this );
            Debug.Assert( _id >= 0 );
        }


        protected ObservableObject( Deserializer d )
        {
            Domain = d.Domain;
            var r = d.StartReading();
            Debug.Assert( r.CurrentReadInfo.Version == 0 );
            _id = r.ReadInt32();
        }

        void Write( Serializer w )
        {
            w.Write( _id );
        }

        event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged
        {
            add { _handler += value; }
            remove { _handler -= value; }
        }

        protected IActivityMonitor DomainMonitor => Domain.DomainMonitor;

        public bool IsDisposed => _id < 0;

        protected bool IsDeserializing => Domain.IsDeserializing;

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
                if( ev != null ) _handler?.Invoke( this, ev );
            }
        }

        internal void Resurect( int id )
        {
            _id = id;
        }

        internal void RestoreInitialValue( string name, object initialValue )
        {
            var p = GetType().GetProperty( name );
            p = p.DeclaringType.GetProperty( name );
            p.SetValue( this, initialValue );
        }
    }

}
