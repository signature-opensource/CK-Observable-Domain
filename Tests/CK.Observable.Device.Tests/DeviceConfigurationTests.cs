using CK.BinarySerialization;
using CK.Core;
using CK.DeviceModel;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Device.Tests
{
    [TestFixture]
    public class DeviceConfigurationTests
    {
        [Test]
        public async Task device_lifecycle_from_ObservableDevice_Async()
        {
            using var _ = TestHelper.Monitor.OpenInfo( nameof( device_lifecycle_from_ObservableDevice_Async ) );
            var host = new SampleDeviceHost();
            var sp = new SimpleServiceContainer();
            sp.Add( host );

            using var obs = new ObservableDomain( TestHelper.Monitor, nameof( device_lifecycle_from_ObservableDevice_Async ), false, serviceProvider: sp );

            var config = new SampleDeviceConfiguration()
            {
                Name = "n°1",
                Message = "Hello World!",
                PeriodMilliseconds = 5
            };

            OSampleDevice? device1 = null;
            await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                device1 = new OSampleDevice( "n°1" );
                device1.LocalConfiguration.Value = config;
                device1.LocalConfiguration.SendDeviceConfigureCommand();
                // We have to wait for the event to be handled.
                device1.IsBoundDevice.Should().BeFalse();
                device1.DeviceConfiguration.Should().BeNull();
            } );
            Debug.Assert( device1 != null );
            await Task.Delay( 2000 );

            obs.Read( TestHelper.Monitor, () =>
            {
                device1.IsBoundDevice.Should().BeTrue( "The ObservableDevice has created its Device!" );
                Debug.Assert( device1.DeviceConfiguration != null );
                ((SampleDeviceConfiguration)device1.DeviceConfiguration).Message.Should().Be( "Hello World!" );
                // The ObservableDevice.Configuration is a safe clone.
                device1.DeviceConfiguration.Should().NotBeSameAs( config );
                device1.DeviceConfiguration.Should().BeEquivalentTo( config );
            } );

            // Even if the ObservableDevice.Configuration is a safe clone and COULD be used to reconfigure the device,
            // this is not really a good idea since this modifies an object that is not Observable (it's a value type
            // from the domain's point of view).
            await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                config.Message = "I've been reconfigured!";
                device1.LocalConfiguration.Value = config;
                device1.LocalConfiguration.SendDeviceConfigureCommand();
            } );
            await Task.Delay( 20 );

            obs.Read( TestHelper.Monitor, () =>
            {
                ((SampleDeviceConfiguration)device1.DeviceConfiguration!).Message.Should().Be( "Hello World!" );
            } );

            // By sending the destroy command from the domain, the Device is destroyed...
            await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                device1.SendDestroyDeviceCommand();
            } );
            await Task.Delay( 20 );

            obs.Read( TestHelper.Monitor, () =>
            {
                device1.IsBoundDevice.Should().BeFalse( "The Device has been destroyed." );
                device1.DeviceConfiguration.Should().BeNull();
            } );
        }

        sealed class Root : ObservableRootObject
        {
            public readonly OSampleDeviceHost OHost;

            public Root()
            {
                OHost = new OSampleDeviceHost();
            }

            public Root( IBinaryDeserializer d, ITypeReadInfo info )
                : base( Sliced.Instance )
            {
                OHost = d.ReadObject<OSampleDeviceHost>();
            }

            public static void Write( IBinarySerializer s, in Root o )
            {
                s.WriteObject( o.OHost );
            }

        }

        [Test]
        public async Task ObservableDeviceHostObject_is_a_dashboard_with_device_and_ObservableDevice_Async()
        {
            using var _ = TestHelper.Monitor.OpenInfo( nameof( ObservableDeviceHostObject_is_a_dashboard_with_device_and_ObservableDevice_Async ) );
            var host = new SampleDeviceHost();
            var sp = new SimpleServiceContainer();
            sp.Add( host );
            var config = new SampleDeviceConfiguration()
            {
                Name = "n°1",
                Message = "Hello World!",
                PeriodMilliseconds = 5
            };
            await host.EnsureDeviceAsync( TestHelper.Monitor, config );

            using var obs = new ObservableDomain<Root>( TestHelper.Monitor, nameof( ObservableDeviceHostObject_is_a_dashboard_with_device_and_ObservableDevice_Async ), false, serviceProvider: sp );

            obs.Read( TestHelper.Monitor, () =>
            {
                obs.Root.OHost.Devices.Should().BeEmpty( "Sidekick instantiation is deferred to the first transaction." );
            } );
            obs.HasWaitingSidekicks.Should().BeTrue( "This indicates that at least one sidekick should be handled." );

            await obs.ModifyThrowAsync( TestHelper.Monitor, null );

            obs.Read( TestHelper.Monitor, () =>
            {
                obs.Root.OHost.Devices.Should().NotBeEmpty( "Sidekick instantiation done." );
            } );

        }

    }
}
