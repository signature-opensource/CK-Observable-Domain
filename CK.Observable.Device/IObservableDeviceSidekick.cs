using CK.DeviceModel;

namespace CK.Observable.Device
{
    public interface IObservableDeviceSidekick
    {
        /// <summary>
        /// Gets the device host from the DeviceModel.
        /// </summary>
        IDeviceHost Host { get; }

        /// <summary>
        /// Gets the object device host if the observable host object has been instantiated, null otherwise.
        /// </summary>
        ObservableDeviceHostObject? ObjectHost { get; }

        /// <summary>
        /// Tries to find an instantiated <see cref="ObservableDeviceObject"/> (of type managed by this sidekick)
        /// by its name or return null.
        /// <para>
        /// The observable may not be bound to an actual device.
        /// </para>
        /// </summary>
        /// <param name="deviceName">The device name to find.</param>
        /// <returns>The observable device or null if not found.</returns>
        ObservableDeviceObject? FindObservableDeviceObject( string deviceName );
    }

}
