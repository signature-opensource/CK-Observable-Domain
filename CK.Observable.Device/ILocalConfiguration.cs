using CK.DeviceModel;
using CK.IO.ObservableDevice;

namespace CK.Observable.Device;

/// <summary>
/// Encapsulates the local configuration of a device.
/// </summary>
/// <typeparam name="TConfig"></typeparam>
public interface ILocalConfiguration<TConfig> where TConfig : DeviceConfiguration
{
    /// <summary>
    /// Gets or sets the local configuration, not necessarily the same as <see cref="ObservableDeviceObject.DeviceConfiguration"/>,
    /// use <see cref="IsDirty"/> to check if this local configuration is the same as the DeviceConfiguration.
    /// </summary>
    TConfig Value { get; set; }

    /// <summary>
    /// Gets whether this <see cref="Value"/> differs from <see cref="ObservableDeviceObject.DeviceConfiguration"/>.
    /// </summary>
    bool IsDirty { get; }

    /// <summary>
    /// Explicitly checks whether the local <see cref="Value"/> is the same as the current <see cref="ObservableDeviceObject.DeviceConfiguration"/>
    /// and raises <see cref="IsDirtyChanged"/> if <see cref="IsDirty"/> has changed.
    /// </summary>
    bool UpdateIsDirty();

    /// <summary>
    /// Raised whenever <see cref="IsDirty"/> has changed.
    /// </summary>
    public event SafeEventHandler IsDirtyChanged;

    /// <summary>
    /// Applies this local <see cref="Value"/> to the device by sending a <see cref="ConfigureDeviceCommand{THost, TConfiguration}"/>
    /// to the device.
    /// Whatever the <paramref name="action"/> is, the device is created if it's currently <see cref="DeviceControlStatus.MissingDevice"/>.
    /// </summary>
    /// <param name="action">Optionally requests a change of the <see cref="ObservableDeviceObject.DeviceControlStatus"/>.</param>
    void SendDeviceConfigureCommand( DeviceControlAction? action = null );
}
