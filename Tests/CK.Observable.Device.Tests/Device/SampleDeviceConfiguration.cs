using CK.DeviceModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable.Device.Tests
{
    public class SampleDeviceConfiguration : DeviceConfiguration
    {
        public SampleDeviceConfiguration()
        {
            Message = "Hello!";
        }

        public SampleDeviceConfiguration( SampleDeviceConfiguration other )
            : base( other )
        {
            PeriodMilliseconds = other.PeriodMilliseconds;
            Message = other.Message;
        }

        public int PeriodMilliseconds { get; set; }

        public string Message { get; set; }
    }
}
