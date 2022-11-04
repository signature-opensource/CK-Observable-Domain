using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable.Device.Tests
{
    /// <summary>
    /// The observable host is totally optional.
    /// </summary>
    [SerializationVersion(0)]
    public class OSampleDeviceHost : ObservableDeviceHostObject<OSampleDeviceSidekick,OSampleDevice, SampleDeviceConfiguration>
    {
        public OSampleDeviceHost()
        {
            // This ensures that the sidekicks have been instantiated.
            // This is called here since it must be called once the object has been fully initialized
            // (and there is no way to know when this constructor has terminated from the core code).
            Domain.EnsureSidekicks();
        }

        OSampleDeviceHost( BinarySerialization.IBinaryDeserializer d, BinarySerialization.ITypeReadInfo info )
            : base( BinarySerialization.Sliced.Instance )
        {
        }

        public static void Write( BinarySerialization.IBinarySerializer s, in OSampleDeviceHost o )
        {
        }
    }
}
