using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Observable.Device.Tests
{
    public class OSampleDeviceSidekick : ObservableDeviceSidekick<SampleDeviceHost, OSampleDevice, OSampleDeviceHost>
    {
        public OSampleDeviceSidekick( IObservableDomainSidekickManager manager, SampleDeviceHost host )
            : base( manager, host )
        {
        }

        protected override DeviceBridge CreateBridge( IActivityMonitor monitor, OSampleDevice o ) => new SampleBridge( o );

        /// <summary>
        /// The specialized bridge class below cannot be made internal or public (because the Bridge{,} base class is protected)
        /// and this is on purpose.
        /// When and if an ObservableObject must interact directly with its device, a specific interface like this one
        /// can be created simply set onto the observable object from the bridge constructor.
        /// </summary>
        internal interface IBridge
        {
            SampleDevice.SafeDeviceState? GetDeviceState();
        }

        class SampleBridge : PassiveBridge<OSampleDeviceSidekick, SampleDevice>, IBridge
        {
            public SampleBridge( OSampleDevice o )
                : base( o )
            {
                o._bridgeAccess = this;
            }

            /// <inheritdoc />
            protected override void OnDeviceAppeared( IActivityMonitor monitor )
            {
                Debug.Assert( Device != null );
                Object.Message = Device.DangerousCurrentMessage;
                Device.MessageChanged.Async += OnMessageChangedAsync;
            }

            Task OnMessageChangedAsync( IActivityMonitor monitor, SampleDevice sender, string e, CancellationToken cancellationToken )
            {  
                return Sidekick.Domain.ModifyThrowAsync( monitor, () =>
                {
                    Object.Message = e;
                } );
            }

            /// <inheritdoc />
            protected override void OnDeviceDisappearing( IActivityMonitor monitor )
            {
                Debug.Assert( Device != null );
                Device.MessageChanged.Async -= OnMessageChangedAsync;
                Object.Message = null;
            }

            protected override void OnObjectDisappeared( IActivityMonitor monitor, bool isObjectDestroyed )
            {
                if( Device != null )
                {
                    Device.MessageChanged.Async -= OnMessageChangedAsync;
                }
            }

            SampleDevice.SafeDeviceState? IBridge.GetDeviceState() => Device?.State;
        }
    }
}
