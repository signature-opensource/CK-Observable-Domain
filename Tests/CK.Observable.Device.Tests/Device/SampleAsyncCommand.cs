using CK.DeviceModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable.Device.Tests
{
    public class SampleAsyncCommand : AsyncDeviceCommand<SampleDeviceHost>
    {
        public int UselessParameter { get; set; }

        public string? AnotherUselessParameter { get; set; }
    }
}
