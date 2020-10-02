using CK.DeviceModel;

namespace CK.Observable.Device
{
    /// <summary>
    /// Abstract base class for device.
    /// </summary>
    [SerializationVersion( 0 )]
    public abstract class ObservableDeviceObject<TSidekick> : ObservableDeviceObject, ISidekickClientObject<TSidekick> where TSidekick : ObservableDomainSidekick
    {
        /// <summary>
        /// Initializes a new observable object device.
        /// </summary>
        /// <param name="deviceName">The device name.</param>
        protected ObservableDeviceObject( string deviceName )
            : base( deviceName )
        {
        }

        /// <summary>
        /// Deserialization constructor.
        /// </summary>
        /// <param name="ctx">The deserialization context.</param>
        protected ObservableDeviceObject( IBinaryDeserializerContext ctx )
            : base( ctx )
        {
            ctx.StartReading();
        }

        void Write( BinarySerializer s )
        {
        }

    }
}
