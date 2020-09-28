using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable.Device.Tests
{
    public class OSampleDeviceSidekick : ObservableDeviceSidekick<SampleDeviceHost, OSampleDevice, OSampleDeviceHost>
    {
        public OSampleDeviceSidekick( ObservableDomain domain, SampleDeviceHost host )
            : base( domain, host )
        {
        }

        protected override Bridge CreateBridge( IActivityMonitor monitor, OSampleDevice o )
        {
            throw new NotImplementedException();
        }

        protected override bool ExecuteCommand( IActivityMonitor monitor, in SidekickCommand command )
        {
            return false;
        }
    }
}
