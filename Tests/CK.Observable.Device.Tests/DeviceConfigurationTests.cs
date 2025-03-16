using CK.BinarySerialization;
using CK.Core;
using CK.DeviceModel;
using Shouldly;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Device.Tests;

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
            device1.IsBoundDevice.ShouldBeFalse();
            device1.DeviceConfiguration.ShouldBeNull();
        } );
        Debug.Assert( device1 != null );
        await Task.Delay( 2000 );

        obs.Read( TestHelper.Monitor, () =>
        {
            device1.IsBoundDevice.ShouldBeTrue( "The ObservableDevice has created its Device!" );
            Debug.Assert( device1.DeviceConfiguration != null );
            ((SampleDeviceConfiguration)device1.DeviceConfiguration).Message.ShouldBe( "Hello World!" );
            // The ObservableDevice.Configuration is a safe clone.
            device1.DeviceConfiguration.ShouldNotBeSameAs( config );
            device1.DeviceConfiguration.ShouldBe( config );
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
            ((SampleDeviceConfiguration)device1.DeviceConfiguration!).Message.ShouldBe( "Hello World!" );
        } );

        // By sending the destroy command from the domain, the Device is destroyed...
        await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            device1.SendDestroyDeviceCommand();
        } );
        await Task.Delay( 20 );

        obs.Read( TestHelper.Monitor, () =>
        {
            device1.IsBoundDevice.ShouldBeFalse( "The Device has been destroyed." );
            device1.DeviceConfiguration.ShouldBeNull();
        } );
    }

    [SerializationVersion( 0 )]
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
            obs.Root.OHost.Devices.ShouldBeEmpty( "Sidekick instantiation is deferred to the first transaction." );
        } );
        obs.HasWaitingSidekicks.ShouldBeTrue( "This indicates that at least one sidekick should be handled." );

        await obs.ModifyThrowAsync( TestHelper.Monitor, null );

        obs.Read( TestHelper.Monitor, () =>
        {
            obs.Root.OHost.Devices.ShouldNotBeEmpty( "Sidekick instantiation done." );
        } );

    }

    [TestCase( null, true )]
    [TestCase( DeviceControlAction.SafeReleaseControl, true )]
    [TestCase( DeviceControlAction.ReleaseControl, true )]
    [TestCase( DeviceControlAction.ForceReleaseControl, true )]

    [TestCase( DeviceControlAction.TakeControl, true )]
    [TestCase( DeviceControlAction.SafeTakeControl, true )]

    [TestCase( DeviceControlAction.TakeOwnership, true )]
    [Explicit( "TBD" )]
    public async Task config_from_Observable_Async( DeviceControlAction? initialConfiAction, bool clearDeviceHost )
    {
        var deviceHost = new SampleDeviceHost();
        var services = new SimpleServiceContainer();
        services.Add( deviceHost );

        using var memory = Util.RecyclableStreamManager.GetStream();
        var expectedControlStatus = ExpectedDeviceControlStatus( initialConfiAction );

        using( TestHelper.Monitor.OpenInfo( $"First run..." ) )
        {
            var obs = new ObservableDomain<Root>( TestHelper.Monitor, "Test", false, services );
            await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                var oDevice = new OSampleDevice( "TheDevice" );
                obs.Root.OHost.Devices["TheDevice"].Object.ShouldBeSameAs( oDevice );
                oDevice.IsBoundDevice.ShouldBeFalse();
                oDevice.DeviceControlStatus.ShouldBe( DeviceControlStatus.MissingDevice );
                var c = oDevice.LocalConfiguration.Value;
                c.CheckValid( TestHelper.Monitor ).ShouldBeTrue( "An empty configuration is valid." );
                c.Status = DeviceConfigurationStatus.Runnable;
                c.PeriodMilliseconds = 3712;
                c.CheckValid( TestHelper.Monitor ).ShouldBeTrue();
                oDevice.LocalConfiguration.SendDeviceConfigureCommand( initialConfiAction );
            } );

            TestHelper.Monitor.Info( "Wait for the Device to appear in the ObservableDomain." );
            while( obs.Read( TestHelper.Monitor, () => obs.Root.OHost.Devices["TheDevice"].Status == DeviceControlStatus.MissingDevice ) )
                Thread.Sleep( 100 );

            TestHelper.Monitor.Info( "Checking status." );
            await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                var oDevice = obs.Root.OHost.Devices["TheDevice"].Object;
                Debug.Assert( oDevice != null );
                oDevice.DeviceControlStatus.ShouldBe( expectedControlStatus );
                oDevice.LocalConfiguration.IsDirty.ShouldBeFalse();
            } );
            TestHelper.Monitor.Info( "Saving and disposing domain." );
            obs.Save( TestHelper.Monitor, memory );
            obs.Dispose( TestHelper.Monitor );
            TestHelper.Monitor.CloseGroup( $"Saved in {memory.Position} bytes." );
            memory.Position = 0;
        }

        if( clearDeviceHost )
        {
            TestHelper.Monitor.Info( "clearDeviceHost is true: destroying the device." );
            await deviceHost.ClearAsync( TestHelper.Monitor, waitForDeviceDestroyed: true );
        }
        using( TestHelper.Monitor.OpenInfo( $"Second run..." ) )
        {
            var obs = new ObservableDomain<Root>( TestHelper.Monitor, "Test", null, RewindableStream.FromStream( memory ), services );
            await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                var oDevice = obs.Root.OHost.Devices["TheDevice"].Object;
                Debug.Assert( oDevice != null );
                oDevice.DeviceConfiguration.ShouldBeNull();
                oDevice.LocalConfiguration.ShouldNotBeNull();
                oDevice.DeviceControlStatus.ShouldBe( expectedControlStatus );
                //if( clearDeviceHost )
                //{
                //    oDevice.DeviceConfiguration.ShouldBeNull();
                //    oDevice.DeviceControlStatus.ShouldBe( DeviceControlStatus.MissingDevice );
                //}
                //else
                {
                    oDevice.DeviceConfiguration.ShouldNotBeNull();
                    oDevice.DeviceControlStatus.ShouldBe( expectedControlStatus );
                }
                oDevice.IsBoundDevice.ShouldBeTrue();
            } );
        }


        static DeviceControlStatus ExpectedDeviceControlStatus( DeviceControlAction? initialConfiAction ) => initialConfiAction switch
            {
                null or DeviceControlAction.SafeReleaseControl or DeviceControlAction.ReleaseControl or DeviceControlAction.ForceReleaseControl
                    => DeviceControlStatus.HasSharedControl,

                DeviceControlAction.SafeTakeControl or DeviceControlAction.TakeControl
                    => DeviceControlStatus.HasControl,

                DeviceControlAction.TakeOwnership
                    => DeviceControlStatus.HasOwnership,

                _ => Throw.NotSupportedException<DeviceControlStatus>()
            };

    }


}
