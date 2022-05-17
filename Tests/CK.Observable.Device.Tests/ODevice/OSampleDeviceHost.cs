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
    public class OSampleDeviceHost : ObservableDeviceHostObject<OSampleDeviceSidekick>
    {
        public OSampleDeviceHost()
        {
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
