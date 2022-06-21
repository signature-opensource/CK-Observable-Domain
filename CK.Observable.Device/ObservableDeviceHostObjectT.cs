using CK.BinarySerialization;
using CK.Core;
using CK.DeviceModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace CK.Observable.Device
{
    /// <summary>
    /// Base class for an observable host of devices.
    /// </summary>
    /// <typeparam name="TSidekick">The type of the sidekick.</typeparam>
    [SerializationVersion( 0 )]
    public abstract class ObservableDeviceHostObject<TSidekick, TDeviceObject> : ObservableDeviceHostObject, ISidekickClientObject<TSidekick>
        where TSidekick : ObservableDomainSidekick, IObservableDeviceSidekick
        where TDeviceObject : ObservableDeviceObject
    {

        ObservableDictionary<string, ODeviceInfo<TDeviceObject>> _devices;

        /// <summary>
        /// Initializes a new <see cref="ObservableDeviceHostObject"/>.
        /// </summary>
        protected ObservableDeviceHostObject()
        {
            _devices = new ObservableDictionary<string, ODeviceInfo<TDeviceObject>>();
        }

        /// <summary>
        /// Gets an observable set of <see cref="ODeviceInfo{TDeviceObject}"/> that are managed by the device host.
        /// This list is under control of the <see cref="IDeviceHost"/>.
        /// </summary>
        public IObservableReadOnlyDictionary<string, ODeviceInfo<TDeviceObject>> Devices => _devices;

        /// <summary>
        /// Specialized deserialization constructor for specialized classes.
        /// </summary>
        /// <param name="_">Unused parameter.</param>
        protected ObservableDeviceHostObject( Sliced _ ) : base( _ ) { }

        /// <summary>
        /// Deserialization constructor for specialized classes.
        /// </summary>
        /// <param name="r">The deserializer.</param>
        /// <param name="info">The information.</param>
        protected ObservableDeviceHostObject( IBinaryDeserializer d, ITypeReadInfo info )
                : base( Sliced.Instance )
        {
            _devices = d.ReadObject<ObservableDictionary<string, ODeviceInfo<TDeviceObject>>>();
        }

        public static void Write( IBinarySerializer s, in ObservableDeviceHostObject<TSidekick, TDeviceObject> o )
        {
            s.WriteObject( o._devices );
        }

        /// <summary>
        /// Gets the sidekick that manages this device host and its devices.
        /// </summary>
        protected TSidekick Sidekick => (TSidekick)_sidekick;

        protected override void OnDestroy()
        {
            _devices.Destroy();
            // Using nullable just in case EnsureDomainSidekick has not been called.
            ((IInternalObservableDeviceSidekick?)_sidekick)?.OnObjectHostDestroyed( Domain.Monitor );
            base.OnDestroy();
        }

        internal override IEnumerable<string> GetAvailableDeviceNames() => _devices.Values.Where( v => v.Status != DeviceControlStatus.MissingDevice ).Select( v => v.DeviceName );

        private protected override sealed void Initialize( IReadOnlyDictionary<string, IDevice> devices, IEnumerable<ObservableDeviceObject> existingObjects )
        {
            using var _ = Domain.Monitor.OpenTrace( $"Initializing '{GetType()}' from '{_sidekick.Host.DeviceHostName}' with {devices.Count} devices." );
            // Ensures that every existing observable objects are tracked.
            foreach( var o in existingObjects )
            {
                if( _devices.TryGetValue( o.DeviceName, out var info ) )
                {
                    info.Object = (TDeviceObject)o;
                }
                else _devices.Add( o.DeviceName, new ODeviceInfo<TDeviceObject>( (TDeviceObject)o, null ) );
            }
            // Ensures that all existing devices are tracked.
            foreach( var d in devices.Values )
            {
                if( _devices.TryGetValue( d.Name, out var info ) )
                {
                    // Don't trust the Object properties here. They may not be up to date yet.
                    info.Status = ObservableDeviceObject.ComputeStatus( d, Domain.DomainName );
                    info.ControllerKey = d.ControllerKey;
                    info.ConfigurationControllerKey = d.ExternalConfiguration.ControllerKey;
                    info.IsRunning = d.IsRunning;
                }
                else _devices.Add( d.Name, new ODeviceInfo<TDeviceObject>( d, null ) );
            }
            List<string>? toRemove = null;
            foreach( var info in _devices.Values )
            {
                if( info.Object == null && !devices.ContainsKey( info.DeviceName ) )
                {
                    if( toRemove == null ) toRemove = new List<string>();
                    toRemove.Add( info.DeviceName );
                }
            }
            if( toRemove != null )
            {
                foreach( var n in toRemove )
                {
                    _devices.Remove( n );
                }
            }
        }

        internal override void Add( ObservableDeviceObject o, IDevice? d )
        {
            if( _devices.TryGetValue( o.DeviceName, out var exists ) )
            {
                exists.Object = (TDeviceObject)o;
            }
            else
            {
                _devices[o.DeviceName] = new ODeviceInfo<TDeviceObject>( (TDeviceObject)o, d );
            }
        }

        internal override void Remove( ObservableDeviceObject o )
        {
            if( _devices.TryGetValue( o.DeviceName, out var exists ) && exists.Status != DeviceControlStatus.MissingDevice )
            {
                exists.Object = null;
            }
            else
            {
                _devices.Remove( o.DeviceName );
            }
        }

        internal override void OnDeviceLifetimeEvent( IActivityMonitor monitor, DeviceLifetimeEvent e, ObservableDeviceObject? o )
        {
            Debug.Assert( _devices.GetValueOrDefault( e.Device.Name )?.Object == o, "The instantiated Object reference is synchronized." );
            if( e.Device.IsDestroyed )
            {
                if( o == null )
                {
                    // We may receive a Destroy event for a device that was already no more in the Host.GetDevices() during
                    // initialization of this host.
                    if( _devices.Remove( e.Device.Name, out var oDevice ) )
                    {
                        oDevice.Destroy();
                    }
                }
                else
                {
                    // We have an observable Object.
                    var oDevice = _devices[e.Device.Name];
                    oDevice.IsRunning = false;
                    oDevice.ControllerKey = null;
                    oDevice.ConfigurationControllerKey = null;
                    oDevice.Status = DeviceControlStatus.MissingDevice;
                }
            }
            else
            {
                if( _devices.TryGetValue( e.Device.Name, out var exists ) )
                {
                    Debug.Assert( o == null
                                    || (o.DeviceControlStatus == ObservableDeviceObject.ComputeStatus( e.Device, Domain.DomainName )
                                        && o.IsRunning == e.Device.IsRunning), "If the Object exists, it already up to date." );
                    exists.Status = o?.DeviceControlStatus ?? ObservableDeviceObject.ComputeStatus( e.Device, Domain.DomainName );
                    exists.IsRunning = e.Device.IsRunning;
                    exists.ControllerKey = e.Device.ControllerKey;
                    exists.ConfigurationControllerKey = e.Configuration.ControllerKey;
                }
                else
                {
                    _devices.Add( e.Device.Name, new ODeviceInfo<TDeviceObject>( e.Device, (TDeviceObject?)o ) );
                }
            }
        }

    }
}
