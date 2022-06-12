using CK.BinarySerialization;
using CK.Core;
using CK.DeviceModel;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Device.Tests
{
    [TestFixture]
    public class DeviceControlTests
    {
        [SerializationVersion(0)]
        public class Root : ObservableRootObject
        {
            public readonly OSampleDeviceHost Host;
            public readonly OSampleDevice TheOne;

            public Root()
            {
                if( Random.Shared.Next( 2 ) == 0 )
                {
                    Host = new OSampleDeviceHost();
                    TheOne = new OSampleDevice( "TheOne" );
                }
                else
                {
                    TheOne = new OSampleDevice( "TheOne" );
                    Host = new OSampleDeviceHost();
                }
            }

            public Root( IBinaryDeserializer d, ITypeReadInfo info )
                : base( Sliced.Instance )
            {
                Host = d.ReadObject<OSampleDeviceHost>();
                TheOne = d.ReadObject<OSampleDevice>();
            }

            public static void Write( IBinarySerializer s, in Root o )
            {
                s.WriteObject( o.Host );
                s.WriteObject( o.TheOne );
            }
        }

        [SetUp]
        public void SetDebugDevice()
        {
            ActivityMonitor.Tags.AddFilter( IDeviceHost.DeviceModel, new LogClamper( LogFilter.Debug, false ) );
        }

        [TearDown]
        public void ClearDebugDevice()
        {
            ActivityMonitor.Tags.RemoveFilter( IDeviceHost.DeviceModel );
        }

        [Test]
        public async Task playing_with_control_Async()
        {
            using var _ = TestHelper.Monitor.OpenInfo( nameof( playing_with_control_Async ) );

            var host = new SampleDeviceHost();
            await host.EnsureDeviceAsync( TestHelper.Monitor, new SampleDeviceConfiguration()
            {
                Name = "TheOne",
                Status = DeviceConfigurationStatus.Runnable,
                PeriodMilliseconds = Timeout.Infinite,
                Message = "Not used here."
            } );
            var device = host["TheOne"];
            Debug.Assert( device != null );

            var sp = new SimpleServiceContainer();
            sp.Add( host );

            using var d1 = new ObservableDomain<Root>( TestHelper.Monitor, "Domain n°1", false, serviceProvider: sp );
            using var d2 = new ObservableDomain<Root>( TestHelper.Monitor, "Domain n°2", false, serviceProvider: sp );

            d1.HasWaitingSidekicks.Should().BeTrue();
            await d1.ModifyThrowAsync( TestHelper.Monitor, null );

            d2.HasWaitingSidekicks.Should().BeTrue();
            await d2.ModifyThrowAsync( TestHelper.Monitor, null );

            // Nobody controls the device.
            CheckStatus( d1, DeviceControlStatus.HasSharedControl );
            CheckStatus( d2, DeviceControlStatus.HasSharedControl );

            await SetControlAsync( "d1 takes control.", d1, DeviceControlAction.TakeControl );
            CheckStatus( d1, DeviceControlStatus.HasControl );
            CheckStatus( d2, DeviceControlStatus.OutOfControl );

            await SetControlAsync( "d2 tries to take control: SafeTakeControl is not enough since d1 controls the device.", d2, DeviceControlAction.SafeTakeControl );
            CheckStatus( d1, DeviceControlStatus.HasControl );
            CheckStatus( d2, DeviceControlStatus.OutOfControl );

            await SetControlAsync( "d2 tries to release control: SafeReleaseControl is not enough since d1 controls the device.", d2, DeviceControlAction.SafeReleaseControl );
            CheckStatus( d1, DeviceControlStatus.HasControl );
            CheckStatus( d2, DeviceControlStatus.OutOfControl );

            await SetControlAsync( "d1 can safely release its own control.", d1, DeviceControlAction.SafeReleaseControl );
            CheckStatus( d1, DeviceControlStatus.HasSharedControl );
            CheckStatus( d2, DeviceControlStatus.HasSharedControl );

            await SetControlAsync( "Since the device is not controlled by anyone, d2 can control.", d2, DeviceControlAction.SafeTakeControl );
            CheckStatus( d1, DeviceControlStatus.OutOfControl );
            CheckStatus( d2, DeviceControlStatus.HasControl );

            await SetControlAsync( "d1 can release the control, even if d2 controls it with ReleaseControl.", d1, DeviceControlAction.ReleaseControl );
            CheckStatus( d1, DeviceControlStatus.HasSharedControl );
            CheckStatus( d2, DeviceControlStatus.HasSharedControl );

            await SetControlAsync( "d2 takes control back via TakeControl.", d2, DeviceControlAction.TakeControl );
            CheckStatus( d1, DeviceControlStatus.OutOfControl );
            CheckStatus( d2, DeviceControlStatus.HasControl );

            await SetControlAsync( "And d1 takes control back.", d1, DeviceControlAction.TakeControl );
            CheckStatus( d1, DeviceControlStatus.HasControl );
            CheckStatus( d2, DeviceControlStatus.OutOfControl );

            static void CheckStatus( ObservableDomain<Root> d, DeviceControlStatus s )
            {
                using( d.AcquireReadLock() )
                {
                    d.Root.TheOne.DeviceControlStatus.Should().Be( s );
                    d.Root.Host.Devices["TheOne"].Status.Should().Be( s );
                }
            }

            async Task SetControlAsync( string action, ObservableDomain<Root> d, DeviceControlAction c )
            {
                using( TestHelper.Monitor.OpenInfo( action ) )
                {
                    await d.ModifyThrowAsync( TestHelper.Monitor, () => d.Root.TheOne.SendDeviceControlCommand( c ) );
                    await device.WaitForSynchronizationAsync( false );
                }
            }
        }

        [Test]
        public async Task playing_with_ownership_Async()
        {
            using var _ = TestHelper.Monitor.OpenInfo( nameof( playing_with_ownership_Async ) );

            var host = new SampleDeviceHost();
            await host.EnsureDeviceAsync( TestHelper.Monitor, new SampleDeviceConfiguration()
            {
                Name = "TheOne",
                Status = DeviceConfigurationStatus.Runnable,
                // Initial ownership is for d1.
                ControllerKey = "Domain n°1",
                PeriodMilliseconds = Timeout.Infinite,
                Message = "Not used here."
            } );
            var device = host["TheOne"];
            Debug.Assert( device != null );

            var sp = new SimpleServiceContainer();
            sp.Add( host );

            using( var d1 = new ObservableDomain<Root>( TestHelper.Monitor, "Domain n°1", false, serviceProvider: sp ) )
            using( var d2 = new ObservableDomain<Root>( TestHelper.Monitor, "Domain n°2", false, serviceProvider: sp ) )
            {

                d1.HasWaitingSidekicks.Should().BeTrue();
                await d1.ModifyThrowAsync( TestHelper.Monitor, null );

                d2.HasWaitingSidekicks.Should().BeTrue();
                await d2.ModifyThrowAsync( TestHelper.Monitor, null );

                CheckStatus( d1, DeviceControlStatus.HasOwnership );
                CheckStatus( d2, DeviceControlStatus.OutOfControlByConfiguration );

                await SetControlAsync( "d2 takes control: this doesn't work.", d2, DeviceControlAction.TakeControl );
                CheckStatus( d1, DeviceControlStatus.HasOwnership );
                CheckStatus( d2, DeviceControlStatus.OutOfControlByConfiguration );

                await SetControlAsync( "But d2 can take back the ownership.", d2, DeviceControlAction.TakeOwnership );
                CheckStatus( d1, DeviceControlStatus.OutOfControlByConfiguration );
                CheckStatus( d2, DeviceControlStatus.HasOwnership );

                await SetControlAsync( "And d1 can ForceReleaseControl to free the device from any ownership.", d2, DeviceControlAction.ForceReleaseControl );
                CheckStatus( d1, DeviceControlStatus.HasSharedControl );
                CheckStatus( d2, DeviceControlStatus.HasSharedControl );
            }

            static void CheckStatus( ObservableDomain<Root> d, DeviceControlStatus s )
            {
                using( d.AcquireReadLock() )
                {
                    d.Root.TheOne.DeviceControlStatus.Should().Be( s );
                    d.Root.Host.Devices["TheOne"].Status.Should().Be( s );
                }
            }

            async Task SetControlAsync( string action, ObservableDomain<Root> d, DeviceControlAction c )
            {
                using( TestHelper.Monitor.OpenInfo( action ) )
                {
                    await d.ModifyThrowAsync( TestHelper.Monitor, () => d.Root.TheOne.SendDeviceControlCommand( c ) );
                    // Since the reconfiguration currently goes through the host that sends an InternalConfigureDeviceCommand
                    // to the device, waiting here for the synchronization is not enough: we unfortunately have
                    // to wait for the InternalConfigureDeviceCommand to reach the command queue.
                    await Task.Delay( 100 );
                    await device.WaitForSynchronizationAsync( false );
                }
            }
        }


    }
}
