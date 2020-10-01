using CK.DeviceModel;

namespace CK.Observable.Device
{

    /// <summary>
    /// Abstract base class for device.
    /// </summary>
    [SerializationVersion( 0 )]
    public abstract class ObservableObjectDevice<TSidekick> : ObservableObjectDevice, ISidekickClientObject<TSidekick> where TSidekick : ObservableDomainSidekick
    {
        /// <summary>
        /// Initializes a new observable object device.
        /// </summary>
        /// <param name="deviceName">The device name.</param>
        protected ObservableObjectDevice( string deviceName )
            : base( deviceName )
        {
        }

        /// <summary>
        /// Deserialization constructor.
        /// </summary>
        /// <param name="ctx">The deserialization context.</param>
        protected ObservableObjectDevice( IBinaryDeserializerContext ctx )
            : base( ctx )
        {
            ctx.StartReading();
        }

        void Write( BinarySerializer s )
        {
        }

    }
}
