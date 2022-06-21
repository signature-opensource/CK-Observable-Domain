using CK.DeviceModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable.Device.Tests
{
    public class SampleCommand : DeviceCommand<SampleDeviceHost>
    {
        public string? MessagePrefix { get; set; } 
    }
}
