using CK.DeviceModel;
using System.Collections.Generic;

namespace CK.Observable.Device
{
    /// <summary>
    /// Base class for an observable host of devices.
    /// </summary>
    /// <typeparam name="TSidekick">The type of the sidekick.</typeparam>
    [SerializationVersion( 0 )]
    public abstract class ObservableDeviceHostObject<TSidekick> : ObservableDeviceHostObject, ISidekickClientObject<TSidekick>
        where TSidekick : ObservableDomainSidekick
    {
        /// <summary>
        /// Initializes a new <see cref="ObservableDeviceHostObject"/>.
        /// </summary>
        protected ObservableDeviceHostObject()
        {
        }

        /// <summary>
        /// Specialized deserialization constructor for specialized classes.
        /// </summary>
        /// <param name="_">Unused parameter.</param>
        protected ObservableDeviceHostObject( RevertSerialization _ ) : base( _ ) { }

        /// <summary>
        /// Deserialization constructor for specialized classes.
        /// </summary>
        /// <param name="r">The deserializer.</param>
        /// <param name="info">The information.</param>
        protected ObservableDeviceHostObject( IBinaryDeserializer r, TypeReadInfo? info )
                : base( RevertSerialization.Default )
        {
        }

        void Write( BinarySerializer s )
        {
        }

        /// <summary>
        /// Gets the sidekick that manages this device host and its devices.
        /// </summary>
        protected TSidekick Sidekick => (TSidekick)_sidekick;

    }
}
