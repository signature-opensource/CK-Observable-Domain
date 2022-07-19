using CK.DeviceModel;

namespace CK.Observable.Device
{
    /// <summary>
    /// Defines the <see cref="ObservableDeviceObject.SendDeviceControlCommand(DeviceControlAction)"/> parameter.
    /// </summary>
    public enum DeviceControlAction
    {
        /// <summary>
        /// Sets the <see cref="IDevice.ControllerKey"/> to null so that observable devices from
        /// multiple domains can control the actual device.
        /// <para>
        /// This will work only if the device is controlled by this domain's device (or is already
        /// not controlled by any device).
        /// </para>
        /// </summary>
        SafeReleaseControl,

        /// <summary>
        /// Sets the <see cref="IDevice.ControllerKey"/> to null so that observable devices from
        /// multiple domains can control the actual device.
        /// <para>
        /// This will work when the device is controlled by another domain's device as long as
        /// the other controller key is not set by the configuration (<see cref="DeviceConfiguration.ControllerKey"/>).
        /// </para>
        /// </summary>
        ReleaseControl,

        /// <summary>
        /// Same as <see cref="ReleaseControl"/> except that the device is reconfigured to forget its
        /// existing ownership: its <see cref="DeviceConfiguration.ControllerKey"/> is set to null.
        /// <para>
        /// This is a dangerous option since this implies to reapply the current configuration
        /// (but with a null ControllerKey).
        /// </para>
        /// </summary>
        ForceReleaseControl,

        /// <summary>
        /// Takes the control of the device if it is currently not controlled by any other domain's device. 
        /// </summary>
        SafeTakeControl,

        /// <summary>
        /// Takes control of the device even if it is currently controlled by another domain's device as long as
        /// the other controller key is not set by the configuration (<see cref="DeviceConfiguration.ControllerKey"/>).
        /// </summary>
        TakeControl,

        /// <summary>
        /// Takes control of the device whatever its current state and configuration is.
        /// See <see cref="DeviceControlStatus.HasOwnership"/>.
        /// <para>
        /// This is a dangerous option since this implies to reapply the current configuration
        /// (but with this domain's name as the <see cref="DeviceConfiguration.ControllerKey"/>).
        /// </para>
        /// </summary>
        TakeOwnership
    }
}
