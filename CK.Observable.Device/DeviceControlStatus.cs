using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CK.DeviceModel;

namespace CK.Observable.Device;

/// <summary>
/// Defines the level of ownership of a <see cref="ObservableDeviceObject"/> on the actual device.
/// </summary>
public enum DeviceControlStatus
{
    /// <summary>
    /// The actual device doesn't exist (<see cref="ObservableDeviceObject.IsBoundDevice"/> is false).
    /// </summary>
    MissingDevice,

    /// <summary>
    /// The actual device is controlled by another domain.
    /// <para>
    /// Commands send by this domain's <see cref="ObservableDeviceObject"/> will
    /// be systematically ignored and set on error with a <see cref="InvalidControllerKeyException"/>.
    /// </para>
    /// </summary>
    OutOfControl,

    /// <summary>
    /// The actual device is controlled by another domain through the <see cref="DeviceConfiguration.ControllerKey"/>.
    /// Taking back the control requires to send an unsafe configuration command to the device.
    /// <para>
    /// Commands send by this domain's <see cref="ObservableDeviceObject"/> will
    /// be systematically ignored and set on error with a <see cref="InvalidControllerKeyException"/>.
    /// </para>
    /// </summary>
    OutOfControlByConfiguration,

    /// <summary>
    /// The actual device has no <see cref="IDevice.ControllerKey"/> (it is null).
    /// <para>
    /// The device accepts commands from this domain but also from any other domain.
    /// </para>
    /// </summary>
    HasSharedControl,

    /// <summary>
    /// The actual device <see cref="IDevice.ControllerKey"/> matches this domain's name.
    /// <para>
    /// The device accepts commands only from this domain.
    /// </para>
    /// </summary>
    HasControl,

    /// <summary>
    /// The actual device <see cref="IDevice.ControllerKey"/> is set by its <see cref="DeviceConfiguration.ControllerKey"/>
    /// and matches this domain's name.
    /// <para>
    /// This is the strongest control level since it is driven by the device's configuration:
    /// <list type="bullet">
    /// <item>
    ///     This domain's <see cref="ObservableDeviceObject"/> drives the life cycle of the device,
    ///     destroying the observable object destroys the device.
    /// </item>
    /// <item>
    ///     Other domain cannot take control of the device (except of course by reconfiguring it).
    /// </item>
    /// </list>
    /// </para>
    /// </summary>
    HasOwnership

}
