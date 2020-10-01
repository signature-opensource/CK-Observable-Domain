using CK.DeviceModel;

namespace CK.Observable.Device
{
    /// <summary>
    /// Base class for an observable host of devices.
    /// </summary>
    /// <typeparam name="TSidekick">The type of the sidekick.</typeparam>
    [SerializationVersion( 0 )]
    public abstract class ObservableObjectDeviceHost<TSidekick> : ObservableObjectDeviceHost
        where TSidekick : ObservableDomainSidekick
    {
        /// <summary>
        /// Initializes a new <see cref="ObservableObjectDeviceHost"/>.
        /// </summary>
        protected ObservableObjectDeviceHost()
        {
        }

        /// <summary>
        /// Deserialization constructor.
        /// </summary>
        /// <param name="ctx">The deserializer context.</param>
        protected ObservableObjectDeviceHost( IBinaryDeserializerContext ctx )
            : base( ctx )
        {
            ctx.StartReading();
        }

        void Write( BinarySerializer s )
        {
        }

    }
}
