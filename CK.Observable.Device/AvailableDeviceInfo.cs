using CK.DeviceModel;

namespace CK.Observable.Device
{
    /// <summary>
    /// Captures a simplied configuration of devices: <see cref="ObservableObjectDeviceHost.Devices"/>.
    /// This is not serializable since this list is under control of the device host and sidekick.
    /// </summary>
    public class AvailableDeviceInfo : ObservableObject
    {
        internal AvailableDeviceInfo( string n, DeviceConfigurationStatus s, string? key )
        {
            Name = n;
            Status = s;
            ControllerKey = key;
        }

        /// <summary>
        /// Gets the name of the device.
        /// This is a unique key for a device in its host.
        /// </summary>
        public string Name { get; internal set; }

        /// <summary>
        /// Gets the <see cref="DeviceConfigurationStatus"/>.
        /// </summary>
        public DeviceConfigurationStatus Status { get; internal set; }

        /// <summary>
        /// Gets the configured controller key.
        /// When not null this locks the <see cref="IDevice.ControllerKey"/> to this value.
        /// </summary>
        public string? ControllerKey { get; internal set; }

    }
}
