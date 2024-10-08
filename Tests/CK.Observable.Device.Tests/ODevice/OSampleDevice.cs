using CK.Core;
using CK.DeviceModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable.Device.Tests;

[SerializationVersion( 0 )]
public class OSampleDevice : ObservableDeviceObject<OSampleDeviceSidekick, SampleDeviceConfiguration>
{
    internal bool _dirtyRaisedEventValue;
#pragma warning disable CS8618 // Non-nullable _bridgeAccess uninitialized. Consider declaring as nullable.

    public OSampleDevice( string deviceName )
        : base( deviceName, false )
    {
        // This ensures that the sidekicks have been instantiated.
        // This is called here since it must be called once the object has been fully initialized
        // (and there is no way to know when this constructor has terminated from the core code).
        Domain.EnsureSidekicks();

        LocalConfiguration.IsDirtyChanged += ( sender ) => _dirtyRaisedEventValue = (bool)sender;
    }

    OSampleDevice( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
            : base( BinarySerialization.Sliced.Instance )
    {
    }
#pragma warning restore CS8618

    public static void Write( BinarySerialization.IBinarySerializer w, in OSampleDevice o )
    {
    }

    /// <summary>
    /// An observable device object should, if possible, not directly interact with its device.
    /// However, if it must be done, the bridge can set a direct reference to itself through a
    /// specific (internal) interface like the <see cref="OSampleDeviceSidekick.IBridge"/>.
    /// <para>
    /// Note that this state reference is updated by the device internal loop.
    /// </para>
    /// </summary>
    /// <returns>The state or null if <see cref="ObservableDeviceObject.IsBoundDevice"/> is false.</returns>
    public SampleDevice.SafeDeviceState? GetSafeState() => _bridgeAccess.GetDeviceState();

    internal OSampleDeviceSidekick.IBridge _bridgeAccess;

    /// <summary>
    /// The message starts with the <see cref="SampleDeviceConfiguration.Message"/> and ends with the
    /// number of times the device loop ran (at <see cref="SampleDeviceConfiguration.PeriodMilliseconds"/>).
    /// </summary>
    public string? Message { get; internal set; }

    /// <summary>
    /// The SendDeviceCommand helper enables easy command sending.
    /// </summary>
    public void SendSimpleCommand( string? messagePrefix = null ) => SendDeviceCommand<SampleCommand>( c => c.MessagePrefix = messagePrefix );

    protected override void OnDeviceConfigurationChanged( DeviceConfiguration? previousConfiguration )
    {
        if( DeviceConfiguration != null && DeviceConfiguration is not SampleDeviceConfiguration configuration )
        {
            Throw.ArgumentException( $"{nameof( DeviceConfiguration )} must be of type {nameof( SampleDeviceConfiguration )}, but was {DeviceConfiguration?.GetType().Name ?? "<null>"}." );
        }

        // Send command during OnConfigurationChanged
        var deviceCommand = CreateDeviceCommand<SampleCommand>();
        Domain.SendBroadcastCommand( deviceCommand );
    }
}
