using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable.Device.Tests
{
    /// <summary>
    /// The observable host is totally optional.
    /// </summary>
    [BinarySerialization.SerializationVersion(0)]
    public class OSampleDeviceHost : ObservableDeviceHostObject<OSampleDeviceSidekick>
    {
        OSampleDeviceHost( BinarySerialization.IBinaryDeserializer d, BinarySerialization.ITypeReadInfo info )
            : base( BinarySerialization.Sliced.Instance )
        {
        }

        public static void Write( BinarySerialization.IBinarySerializer s, in OSampleDeviceHost o )
        {
        }
    }
}
