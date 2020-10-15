using CK.DeviceModel;
using CK.DeviceModel.Command;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable.Device.Tests
{
    [SerializationVersion( 0 )]
    public class OSampleDevice : ObservableDeviceObject<OSampleDeviceSidekick>
    {

#pragma warning disable CS8618 // Non-nullable _bridgeAccess uninitialized. Consider declaring as nullable.

        public OSampleDevice( string deviceName )
            : base( deviceName )
        {
            // This ensures that the sidekicks have been instanciated.
            // This is called here since it must be called once the object has been fully initialized
            // (and there is no way to know when this construstor has terminated from the core code).
            Domain.EnsureSidekicks();
        }

        protected OSampleDevice( IBinaryDeserializerContext ctx )
            : base( ctx )
        {
            ctx.StartReading();
        }
#pragma warning restore CS8618

        void Write( BinarySerializer w )
        {
        }

        internal OSampleDeviceSidekick.IBridge _bridgeAccess;

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

        /// <summary>
        /// The message starts with the <see cref="SampleDeviceConfiguration.Message"/> and ends with the
        /// number of times the device loop ran (at <see cref="SampleDeviceConfiguration.PeriodMilliseconds"/>).
        /// </summary>
        public string? Message { get; internal set; }

        /// <summary>
        /// The CmdSend helper enables easy command sending.
        /// </summary>
        public void CmdCommandSync() => CmdSend<SampleSyncCommand>();

        public void CmdCommandAsync() => CmdSend<SampleAsyncCommand>( c => { c.UselessParameter = 3712; c.AnotherUselessParameter = "Nop"; } );
    }
}
