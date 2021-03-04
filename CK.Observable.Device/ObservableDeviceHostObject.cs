using CK.DeviceModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CK.Observable.Device
{
    /// <summary>
    /// Non generic abstract base class for device that is not intended to be specialized directly.
    /// Use the generic <see cref="ObservableDeviceHostObject{TSidekick}"/> as the object device device base.
    /// </summary>
    [SerializationVersion( 0 )]
    public abstract class ObservableDeviceHostObject : ObservableObject, ISidekickLocator
    {
        /// <summary>
        /// This is exposed as a typed TSidekick by ObservableDeviceHostObject{TSidekick}.
        /// </summary>
        internal ObservableDomainSidekick _sidekick;

        ObservableDomainSidekick ISidekickLocator.Sidekick => _sidekick;

        /// <summary>
        /// Contains the list of devices.
        /// This list is mutable by specialization but this should be used this care: the actual devices
        /// are handled by the <see cref="IDeviceHost"/>.
        /// See <see cref="OnDevicesChanged"/> that merges the devices from the external device host (and can be overridden if needed).
        /// </summary>
        internal protected readonly ObservableList<AvailableDeviceInfo> InternalDevices;

#pragma warning disable CS8618 // _sidekick is initialized by the first call to ApplyDevicesChanged.
        private protected ObservableDeviceHostObject()
        {
            InternalDevices = new ObservableList<AvailableDeviceInfo>();
        }

        /// <summary>
        /// Specialized deserialization constructor for specialized classes.
        /// </summary>
        /// <param name="_">Unused parameter.</param>
        protected ObservableDeviceHostObject( RevertSerialization _ ) : base( _ ) { }

        ObservableDeviceHostObject( IBinaryDeserializer r, TypeReadInfo? info )
                : base( RevertSerialization.Default )
        {
            InternalDevices = new ObservableList<AvailableDeviceInfo>();
        }
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.

        void Write( BinarySerializer w )
        {
        }

        /// <summary>
        /// Gets an observable list of devices that are managed by the device host.
        /// This list is under control of the <see cref="IDeviceHost"/>.
        /// </summary>
        public IObservableReadOnlyList<AvailableDeviceInfo> Devices => InternalDevices;

        internal void ApplyDevicesChanged( ObservableDomainSidekick sidekick, Dictionary<string, DeviceConfiguration> snapshot )
        {
            Debug.Assert( _sidekick == null || _sidekick == sidekick, "Initial call or just an update." );

            _sidekick = sidekick;
            OnDevicesChanged( snapshot );
        }

        /// <summary>
        /// Called during initialization of this host and each time the external devices changed.
        /// This method synchronizes the <see cref="Devices"/> collection.
        /// </summary>
        /// <param name="snapshot">A freely mutable snapshots of the external devices configurations.</param>
        private protected virtual void OnDevicesChanged( Dictionary<string, DeviceConfiguration> snapshot )
        {
            for( int i = 0; i < InternalDevices.Count; ++i )
            {
                var d = Devices[i];
                if( snapshot.Remove( d.Name, out var conf ) )
                {
                    d.Status = conf.Status;
                    d.ControllerKey = conf.ControllerKey;
                }
                else
                {
                    InternalDevices.RemoveAt( i-- );
                    d.Destroy();
                }
            }
            InternalDevices.AddRange( snapshot.Values.Select( c => new AvailableDeviceInfo( c.Name, c.Status, c.ControllerKey ) ) );
        }


    }
}
