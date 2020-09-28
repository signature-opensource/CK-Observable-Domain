using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CK.Core;
using CK.Core.Impl;
using CK.DeviceModel;
using CK.PerfectEvent;
using NUnit.Framework.Constraints;

namespace CK.Observable.Device.Tests
{
    public class SampleDevice : Device<SampleDeviceConfiguration>
    {
        Task _run;
        CancellationTokenSource _stopToken;

        string _message;
        int _period;
        int _count;
        PerfectEventSender<SampleDevice, string> _event;

        public SampleDevice( IActivityMonitor monitor, SampleDeviceConfiguration c )
            : base( monitor, c )
        {
            _period = c.PeriodMilliseconds;
            _message = c.Message;
            _event = new PerfectEventSender<SampleDevice, string>();
        }

        public PerfectEvent<SampleDevice, string> MessageChanged => _event.PerfectEvent;

        protected override Task<bool> DoStartAsync( IActivityMonitor monitor, DeviceStartedReason reason )
        {
            _stopToken = new CancellationTokenSource();
            _run = Task.Run( RunLoop );
            return Task.FromResult( true );
        }

        void RunLoop()
        {
            var monitor = new ActivityMonitor( "SampleDevice run." );
            while( !_stopToken.IsCancellationRequested )
            {
                Task.Delay( _period );
                _event.SafeRaiseAsync( monitor, this, $"{_message} - {_count++}" );
            }
            monitor.MonitorEnd();
        }

        protected override Task DoStopAsync( IActivityMonitor monitor, DeviceStoppedReason reason )
        {
            _stopToken.Cancel();
            return _run;
        }

        protected override Task<DeviceReconfiguredResult> DoReconfigureAsync( IActivityMonitor monitor, SampleDeviceConfiguration config, bool controllerKeyChanged )
        {
            if( _period != config.PeriodMilliseconds )
            {
                _period = config.PeriodMilliseconds;
                return Task.FromResult( DeviceReconfiguredResult.UpdateSucceeded );
            }
            return Task.FromResult( DeviceReconfiguredResult.None );
        }

    }
}
