using CK.Core;
using CK.DeviceModel;

namespace CK.Observable.Device
{
    /// <summary>
    /// Abstract base class for device.
    /// </summary>
    [SerializationVersion( 0 )]
    public abstract class ObservableDeviceObject<TSidekick,TConfig> : ObservableDeviceObject, ISidekickClientObject<TSidekick>
        where TSidekick : ObservableDomainSidekick
        where TConfig : DeviceConfiguration
    {
        /// <summary>
        /// Initializes a new observable object device.
        /// </summary>
        /// <param name="deviceName">The device name.</param>
        protected ObservableDeviceObject( string deviceName )
            : base( deviceName )
        {
        }

        protected ObservableDeviceObject( BinarySerialization.Sliced _ ) : base( _ ) { }

        ObservableDeviceObject( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
                : base( BinarySerialization.Sliced.Instance )
        {
        }

        public static void Write( BinarySerialization.IBinarySerializer s, in ObservableDeviceObject<TSidekick,TConfig> o )
        {
        }

    }
}
