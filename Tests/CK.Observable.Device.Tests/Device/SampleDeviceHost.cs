using CK.DeviceModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable.Device.Tests
{
    public class SampleDeviceHost : DeviceHost<SampleDevice,DeviceHostConfiguration<SampleDeviceConfiguration>,SampleDeviceConfiguration>
    {
        public SampleDeviceHost( IDeviceAlwaysRunningPolicy alwaysRunningPolicy )
            : base( alwaysRunningPolicy )
        {
        }
    }
}
