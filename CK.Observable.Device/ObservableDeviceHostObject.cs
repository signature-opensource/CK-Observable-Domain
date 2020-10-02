using CK.DeviceModel;

namespace CK.Observable.Device
{
    /// <summary>
    /// Non generic abstract base class for device that is not intended to be specialized directly.
    /// Use the generic <see cref="ObservableDeviceHostObject{TSidekick}"/> as the object device device base.
    /// </summary>
    [SerializationVersion( 0 )]
    public abstract class ObservableDeviceHostObject : ObservableObject
    {
        /// <summary>
        /// Contains the list of devices.
        /// This list is mutable by specialiation but this should be used this care: the actual devices
        /// are handled by the <see cref="IDeviceHost"/>.
        /// </summary>
        internal protected readonly ObservableList<AvailableDeviceInfo> InternalDevices;

        private protected ObservableDeviceHostObject()
        {
            InternalDevices = new ObservableList<AvailableDeviceInfo>();
        }

        private protected ObservableDeviceHostObject( IBinaryDeserializerContext ctx )
            : base( ctx )
        {
            ctx.StartReading();
            InternalDevices = new ObservableList<AvailableDeviceInfo>();
        }

        void Write( BinarySerializer s )
        {
        }

        /// <summary>
        /// Gets an observable list of devices that are managed by the device host.
        /// This list is under control of the <see cref="IDeviceHost"/>.
        /// </summary>
        public IObservableReadOnlyList<AvailableDeviceInfo> Devices => InternalDevices;

    }
}
