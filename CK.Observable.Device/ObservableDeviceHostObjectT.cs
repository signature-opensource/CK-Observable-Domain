using CK.DeviceModel;

namespace CK.Observable.Device
{
    /// <summary>
    /// Base class for an observable host of devices.
    /// </summary>
    /// <typeparam name="TSidekick">The type of the sidekick.</typeparam>
    [SerializationVersion( 0 )]
    public abstract class ObservableDeviceHostObject<TSidekick> : ObservableDeviceHostObject
        where TSidekick : ObservableDomainSidekick
    {
        /// <summary>
        /// Initializes a new <see cref="ObservableDeviceHostObject"/>.
        /// </summary>
        protected ObservableDeviceHostObject()
        {
        }

        protected ObservableDeviceHostObject( RevertSerialization _ ) : base( _ ) { }

        protected ObservableDeviceHostObject( IBinaryDeserializer r, TypeReadInfo? info )
                : base( RevertSerialization.Default )
        {
        }

        void Write( BinarySerializer s )
        {
        }

    }
}
