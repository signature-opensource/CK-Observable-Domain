using CK.Core;
using CK.DeviceModel;
using CK.Observable;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace CK.Observable.Device
{
    /// <summary>
    /// Abstract base class for a sidekick that interfaces a <see cref="IDeviceHost"/>.
    /// </summary>
    /// <typeparam name="THost"></typeparam>
    public abstract partial class ObservableDeviceSidekick<THost,TObjectDevice,TObjectDeviceHost> : ObservableDomainSidekick, IInternalObservableDeviceSidekick<THost>
        where THost : IDeviceHost
        where TObjectDevice : ObservableObjectDevice<THost>
        where TObjectDeviceHost: ObservableObjectDeviceHost<THost>
    {
        readonly Dictionary<string, Bridge> _objects;
        TObjectDeviceHost? _objectHost;
        Bridge? _firstUnbound;

        /// <summary>
        /// Initializes a new <see cref="ObservableDeviceSidekick{THost}"/>.
        /// </summary>
        /// <param name="domain">The observable domain.</param>
        /// <param name="host">The host.</param>
        protected ObservableDeviceSidekick( ObservableDomain domain, THost host )
            : base( domain )
        {
            Host = host;
            _objects = new Dictionary<string, Bridge>();
            host.DevicesChanged.Async += OnDevicesChangedAsync;
        }

        /// <summary>
        /// Gets the device host (Device model).
        /// </summary>
        protected THost Host { get; }

        /// <summary>
        /// Gets the object device host if the observable object has been instantiated, null otherwise.
        /// </summary>
        protected TObjectDeviceHost? ObjectHost => _objectHost;

        Task OnDevicesChangedAsync( IActivityMonitor monitor, IDeviceHost sender, EventArgs e )
        {
            Debug.Assert( ReferenceEquals( Host, sender ) );

            return Domain.ModifyAsync( monitor, () =>
            {
                if( _objectHost != null ) UpdateObjectHost();
                if( _firstUnbound != null )
                {
                    Bridge f = _firstUnbound;
                    for( ; ; )
                    {
                        Debug.Assert( f.Device == null );
                        var d = Host.Find( f.Object.DeviceName );
                        var next = f._nextUnbound; 
                        if( d != null ) f.SetDevice( monitor, d );
                        if( next == null ) break;
                        f = next;
                    }
                }
            } );
        }

        protected override void RegisterClientObject( IActivityMonitor monitor, IDisposableObject o )
        {
            if( o is TObjectDevice device )
            {
                if( _objects.TryGetValue( device.DeviceName, out var bridge ) )
                {
                    throw new Exception( $"Duplicate device error: A device named '{device.DeviceName}' already in the domain (index {bridge.Object.OId.Index})." );
                }
                // We don't unsubsribe to the Disposed event since a sidekick lives longer (and
                // ObservableDelegate skips sidekicks while serializing.
                o.Disposed += OnObjectDeviceDisposed;
                bridge = CreateBridge( monitor, device );
                _objects.Add( device.DeviceName, bridge );
                bridge.Initialize( monitor, this, Host.Find( device.DeviceName ) );
            }
            else if( o is TObjectDeviceHost host )
            {
                if( _objectHost != null )
                {
                    throw new Exception( $"There must be at most one device host object in a ObservableDomain. Object at index {_objectHost.OId.Index} is already registered." );
                }
                _objectHost = host;
                OnObjectHostAppeared( monitor );
                _objectHost.Disposed += OnObjectHostDisposed;
                UpdateObjectHost();
            }
        }

        void OnObjectHostDisposed( object sender, ObservableDomainEventArgs e )
        {
            _objectHost = null;
            OnObjectHostDisappeared( e.Monitor );
        }

        void OnObjectDeviceDisposed( object sender, ObservableDomainEventArgs e )
        {
            var o = (TObjectDevice)sender;
            _objects.Remove( o.DeviceName, out var bridge );
            Debug.Assert( bridge != null );
            bridge.OnDestroy( e.Monitor, true );
        }

        void AddUnbound( Bridge b )
        {
            b._nextUnbound = _firstUnbound;
            _firstUnbound = b;
        }

        void RemoveUnbound( Bridge b )
        {
            Debug.Assert( _firstUnbound != null );
            Bridge? p = null;
            Bridge f = _firstUnbound;
            for( ; ;)
            {
                if( f == b )
                {
                    if( p == null ) _firstUnbound = b._nextUnbound;
                    else p._nextUnbound = b._nextUnbound;
                    b._nextUnbound = null;
                    break;
                }
                Debug.Assert( f._nextUnbound != null );
                f = f._nextUnbound;
            }
        }

        void UpdateObjectHost()
        {
            Debug.Assert( _objectHost != null );
            // Takes a snapshot: the hosted devices list may change concurrently (when called from RegisterClientObject).
            // This can be optimized: here the intermediate list is concretized for nothing.
            var configs = Host.DeviceConfigurations.ToDictionary( c => c.Name );
            for( int i = 0; i < _objectHost.Devices.Count; ++i )
            {
                var d = _objectHost.Devices[i];
                if( configs.Remove( d.Name, out var conf ) )
                {
                    d.Status = conf.Status;
                    d.ControllerKey = conf.ControllerKey;
                }
                else 
                {
                    _objectHost.InternalDevices.RemoveAt( i-- );
                    d.Dispose();
                }
            }
            _objectHost.InternalDevices.AddRange( configs.Values.Select( c => new AvailableDeviceInfo( c.Name, c.Status, c.ControllerKey ) ) );
        }

        /// <inheritdoc />
        protected sealed override void Dispose( IActivityMonitor monitor )
        {
            Host.DevicesChanged.Async -= OnDevicesChangedAsync;
            foreach( var b in _objects.Values )
            {
                b.OnDestroy( monitor, false );
            }
            OnDispose( monitor );
        }

        /// <summary>
        /// Optional extension point called when the <see cref="Host"/> object exists.
        /// This default implementation does nothing.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        protected virtual void OnObjectHostAppeared( IActivityMonitor monitor )
        {
        }

        /// <summary>
        /// Optional extension point called when the <see cref="Host"/> object is disposed.
        /// This default implementation does nothing.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        protected virtual void OnObjectHostDisappeared( IActivityMonitor monitor )
        {
        }

        /// <inheritdoc cref="Dispose(IActivityMonitor)"/>.
        protected virtual void OnDispose( IActivityMonitor monitor )
        {
        }
    }

    /// <summary>
    /// Internal marker of sidekick: <see cref="ObservableObjectDevice{THost}"/> and <see cref="ObservableObjectDeviceHost{THost}"/>
    /// use this to register themselves.
    /// </summary>
    interface IInternalObservableDeviceSidekick<THost>
            where THost : IDeviceHost
    {
    }

}
