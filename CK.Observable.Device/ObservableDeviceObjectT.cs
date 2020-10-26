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

        protected ObservableDeviceObject( RevertSerialization _ ) : base( _ ) { }

        ObservableDeviceObject( IBinaryDeserializer r, TypeReadInfo? info )
                : base( RevertSerialization.Default )
        {
        }

        void Write( BinarySerializer s )
        {
        }

    }
}
