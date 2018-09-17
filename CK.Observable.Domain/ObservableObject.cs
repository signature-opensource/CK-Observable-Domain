

using CK.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;

namespace CK.Observable
{
    [SerializationVersionAttribute(0)]
    public abstract class ObservableObject : INotifyPropertyChanged, IKnowUnifiedTypeDriver, IDisposable
    {
        protected const string ExportContentOIdName = "$i";
        protected const string ExportContentPropName = "$C";
        int _id;
        IUnifiedTypeDriver _driver;
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
            _driver = UnifiedTypeRegistry.FindDriver( GetType() );
            Debug.Assert( _driver != null );
            Domain = domain;
            _id = Domain.Register( this );
            Debug.Assert( _id >= 0 );
        }

        protected ObservableObject( IBinaryDeserializerContext d )
        {
            _driver = UnifiedTypeRegistry.FindDriver( GetType() );
            Debug.Assert( _driver != null );
            var r = d.StartReading();
            Debug.Assert( r.CurrentReadInfo.Version == 0 );
            Domain = r.Services.GetService<ObservableDomain>( throwOnNull: true );
            _id = r.ReadInt32();
            Debug.Assert( _id >= 0 );
        }

        void Write( BinarySerializer w )
        {
            w.Write( _id );
        }

        //void Export( int num, ObjectExporter e, IReadOnlyList<ExportableProperty> props )
        //{
        //    e.Target.EmitStartObject( -1, ObjectExportedKind.Object );
        //    e.ExportNamedProperty( ExportContentOIdName, OId );
        //    e.Target.EmitPropertyName( ExportContentPropName );
        //    if( props.Count == 0 )
        //    {
        //        e.Target.EmitEmptyObject( num );
        //    }
        //    else
        //    {
        //        e.Target.EmitStartObject( num, ObjectExportedKind.Object );
        //        foreach( var p in props )
        //        {
        //            e.ExportNamedProperty( p.Name, p.Value );
        //        }
        //        e.Target.EmitEndObject( num, ObjectExportedKind.Object );
        //    }
        //    e.Target.EmitEndObject( -1, ObjectExportedKind.Object );
        //}

        IUnifiedTypeDriver IKnowUnifiedTypeDriver.UnifiedTypeDriver => _driver;

        internal IUnifiedTypeDriver UnifiedTypeDriver => _driver;


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

    }

}
