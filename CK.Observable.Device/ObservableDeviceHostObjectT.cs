using CK.Core;
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
        protected ObservableDeviceHostObject( BinarySerialization.Sliced _ ) : base( _ ) { }

        /// <summary>
        /// Deserialization constructor for specialized classes.
        /// </summary>
        /// <param name="r">The deserializer.</param>
        /// <param name="info">The information.</param>
        protected ObservableDeviceHostObject( BinarySerialization.IBinaryDeserializer d, BinarySerialization.ITypeReadInfo info )
                : base( BinarySerialization.Sliced.Instance )
        {
        }
        
        public static void Write( BinarySerialization.IBinarySerializer s, in ObservableDeviceHostObject<TSidekick> o )
        {
        }

        /// <summary>
        /// Gets the sidekick that manages this device host and its devices.
        /// </summary>
        protected TSidekick Sidekick => (TSidekick)_sidekick;

    }
}
