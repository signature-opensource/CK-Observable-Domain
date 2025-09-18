using CK.BinarySerialization;
using CK.Core;
using CK.DeviceModel;
using Shouldly;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;
using CK.IO.DeviceModel;
using CK.IO.ObservableDevice;

namespace CK.Observable.Device.Tests;

[TestFixture]
public class DeviceBridgeTests
{
    [TestCase( "BeforeObservableDevice" )]
    [TestCase( "AfterObservableDevice" )]
    [TestCase( "AfterDevice" )]
    [Timeout( 2 * 1000 )]
    public async Task sample_observable_Async( string createHostStep )
    {
        using var _ = TestHelper.Monitor.OpenInfo( nameof( sample_observable_Async ) );
        var host = new SampleDeviceHost();
        var sp = new SimpleServiceContainer();
        sp.Add( host );

        using var obs = new ObservableDomain(TestHelper.Monitor, nameof(sample_observable_Async), false, serviceProvider: sp );

        OSampleDevice? device1 = null;
        OSampleDeviceHost? oHost = null;
        await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            if( createHostStep == "BeforeObservableDevice" ) oHost = new OSampleDeviceHost();

            device1 = new OSampleDevice( "n°1" );
            device1.LocalConfiguration.Value.Name.ShouldBe( "n°1" );

            if( createHostStep == "AfterObservableDevice" ) oHost = new OSampleDeviceHost();

            if( oHost != null )
            {
                oHost.Devices.Count.ShouldBe( 1 );
                oHost.Devices["n°1"].DeviceName.ShouldBe( "n°1" );
                oHost.Devices["n°1"].Object.ShouldBe( device1 );
                oHost.Devices["n°1"].IsRunning.ShouldBe( false );
                oHost.Devices["n°1"].Status.ShouldBe( DeviceControlStatus.MissingDevice );
            }
            device1.IsBoundDevice.ShouldBeFalse();
            device1.DeviceControlStatus.ShouldBe( DeviceControlStatus.MissingDevice );
            device1.DeviceConfiguration.ShouldBeNull();
            device1.Message.ShouldBeNull();
            device1.IsRunning.ShouldBeNull();
        } );
        Debug.Assert( device1 != null );

        var config = new SampleDeviceConfiguration()
        {
            Name = "n°1",
            Message = "Hello World!",
            PeriodMilliseconds = 5
        };
        (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).ShouldBe( DeviceApplyConfigurationResult.CreateSucceeded );

        await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            device1.IsBoundDevice.ShouldBeTrue();
            Debug.Assert( device1.DeviceConfiguration != null );
            device1.DeviceConfiguration.Status.ShouldBe( DeviceConfigurationStatus.Disabled );
            device1.DeviceControlStatus.ShouldBe( DeviceControlStatus.HasSharedControl, "Since there is no ControllerKey on the device, anybody can control it." );
            device1.Message.ShouldBeNull();
            device1.IsRunning.ShouldNotBeNull().ShouldBeFalse();

            if( oHost == null ) oHost = new OSampleDeviceHost();
            oHost.Devices.Count.ShouldBe( 1 );
            oHost.Devices["n°1"].DeviceName.ShouldBe( "n°1" );
            oHost.Devices["n°1"].Object.ShouldBe( device1 );
            oHost.Devices["n°1"].IsRunning.ShouldBe( false );
            oHost.Devices["n°1"].Status.ShouldBe( DeviceControlStatus.HasSharedControl );
        } );

        config.Status = DeviceConfigurationStatus.AlwaysRunning;
        (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).ShouldBe( DeviceApplyConfigurationResult.UpdateSucceeded );

        obs.Read( TestHelper.Monitor, () =>
        {
            device1.DeviceConfiguration!.Status.ShouldBe( DeviceConfigurationStatus.AlwaysRunning );
            device1.IsRunning.ShouldNotBeNull().ShouldBeTrue();
            oHost!.Devices["n°1"].IsRunning.ShouldBeTrue();
        } );

        config.ControllerKey = obs.DomainName;
        (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).ShouldBe( DeviceApplyConfigurationResult.UpdateSucceeded );
        obs.Read( TestHelper.Monitor, () =>
        {
            device1.DeviceControlStatus.ShouldBe( DeviceControlStatus.HasOwnership );
            oHost!.Devices["n°1"].Status.ShouldBe( DeviceControlStatus.HasOwnership );
        } );

        config.ControllerKey = obs.DomainName + "No more!";
        (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).ShouldBe( DeviceApplyConfigurationResult.UpdateSucceeded );

        obs.Read( TestHelper.Monitor, () =>
        {
            device1.DeviceControlStatus.ShouldBe( DeviceControlStatus.OutOfControlByConfiguration );
            oHost!.Devices["n°1"].Status.ShouldBe( DeviceControlStatus.OutOfControlByConfiguration );
        } );

        await host.Find( "n°1" )!.DestroyAsync( TestHelper.Monitor );

        await Task.Delay( 150 );

        obs.Read( TestHelper.Monitor, () =>
        {
            device1.DeviceConfiguration.ShouldBeNull();
            device1.IsRunning.ShouldBeNull();
            device1.DeviceControlStatus.ShouldBe( DeviceControlStatus.MissingDevice );

            Throw.DebugAssert( oHost != null );
            oHost.Devices["n°1"].DeviceName.ShouldBe( "n°1" );
            oHost.Devices["n°1"].Object.ShouldBe( device1 );
            oHost.Devices["n°1"].IsRunning.ShouldBeFalse();
            oHost.Devices["n°1"].Status.ShouldBe( DeviceControlStatus.MissingDevice );
        } );

        TestHelper.Monitor.Info( "Disposing Domain..." );
        obs.Dispose( TestHelper.Monitor );
        TestHelper.Monitor.Info( "...Domain disposed, clearing host..." );
        await host.ClearAsync( TestHelper.Monitor, waitForDeviceDestroyed: true );
        TestHelper.Monitor.Info( "Host cleared." );
    }


    static bool DeviceIsRunningChanged = false;

    static void Device_IsRunningChanged( object sender )
    {
        DeviceIsRunningChanged = true;
    }

    [Test]
    [Timeout( 2 * 1000 )]
    public async Task Start_and_Stop_commands_Async()
    {
        using var gLog = TestHelper.Monitor.OpenInfo( nameof( Start_and_Stop_commands_Async ) );
        DeviceIsRunningChanged = false;

        var host = new SampleDeviceHost();
        var sp = new SimpleServiceContainer();
        sp.Add( host );
        var config = new SampleDeviceConfiguration()
        {
            Name = "The device...",
            Message = "Hello World!",
            PeriodMilliseconds = 150,
            Status = DeviceConfigurationStatus.RunnableStarted
        };
        (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).ShouldBe( DeviceApplyConfigurationResult.CreateAndStartSucceeded );

        using var obs = new ObservableDomain(TestHelper.Monitor, nameof(Start_and_Stop_commands_Async), true, serviceProvider: sp );

        OSampleDevice? device = null;
        await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            device = new OSampleDevice( "The device..." );
            device.IsRunningChanged += Device_IsRunningChanged;
            Debug.Assert( device.IsRunning != null );
            device.IsRunning.ShouldNotBeNull().ShouldBeTrue( "ConfigurationStatus is RunnableStarted." );
            device.DeviceControlStatus.ShouldNotBe( DeviceControlStatus.MissingDevice );
            device.DeviceConfiguration.ShouldNotBeNull();
        } );
        bool isRunning = true;

        DeviceIsRunningChanged.ShouldBeFalse();
        Debug.Assert( device != null );
        Debug.Assert( device.IsRunning != null );

        await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            device.SendStopDeviceCommand();
        } );

        while( isRunning )
        {
            await Task.Delay( 100 );
            obs.Read( TestHelper.Monitor, () =>
            {
                isRunning = device.IsRunning.Value;
            } );
        }
        DeviceIsRunningChanged.ShouldBeTrue();
        DeviceIsRunningChanged = false;

        await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            device.SendStartDeviceCommand();
        } );

        while( !isRunning )
        {
            await Task.Delay( 100 );
            isRunning = obs.Read( TestHelper.Monitor, () => device.IsRunning.Value );
        }
        DeviceIsRunningChanged.ShouldBeTrue();
        DeviceIsRunningChanged = false;

        await host.Find( "The device..." )!.DestroyAsync( TestHelper.Monitor );
        await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            device.IsBoundDevice.ShouldBeFalse();
            device.IsRunning.ShouldBeNull();
            device.DeviceControlStatus.ShouldBe( DeviceControlStatus.MissingDevice );
            device.DeviceConfiguration.ShouldBeNull();
        }, waitForDomainPostActionsCompletion: true );
        DeviceIsRunningChanged.ShouldBeTrue();
        DeviceIsRunningChanged = false;

        TestHelper.Monitor.Info( "Disposing Domain..." );
        obs.Dispose( TestHelper.Monitor );
        TestHelper.Monitor.Info( "...Domain disposed, clearing host..." );
        await host.ClearAsync( TestHelper.Monitor, waitForDeviceDestroyed: true );
        TestHelper.Monitor.Info( "Host cleared." );
    }

    [Test]
    [Timeout( 10000 )]
    public async Task commands_are_easy_to_send_Async()
    {
        using var gLog = TestHelper.Monitor.OpenInfo( nameof( commands_are_easy_to_send_Async ) );
        var host = new SampleDeviceHost();
        var sp = new SimpleServiceContainer();
        sp.Add( host );
        var config = new SampleDeviceConfiguration()
        {
            Name = "n°1",
            // We loop fast: the device.GetSafeState() is updated by the device loop.
            PeriodMilliseconds = 250,
            Status = DeviceConfigurationStatus.RunnableStarted
        };
        (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).ShouldBe( DeviceApplyConfigurationResult.CreateAndStartSucceeded );

        using var obs = new ObservableDomain(TestHelper.Monitor, nameof(commands_are_easy_to_send_Async), true, serviceProvider: sp );

        OSampleDevice? device = null;
        await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            device = new OSampleDevice( "n°1" );
            Debug.Assert( device.IsRunning == true, "ConfigurationStatus is RunnableStarted." );
            device.SendSimpleCommand();
        }, waitForDomainPostActionsCompletion: true );
        Throw.DebugAssert( device != null );
        Throw.DebugAssert( device.IsRunning != null );

        await Task.Delay( 1000 );
        await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            var directState = device.GetSafeState();
            Throw.DebugAssert( directState != null );
            // The OnConfigurationChanged sent a SampleCommad.
            directState.SyncCommandCount.ShouldBe( 2 );
            device.SendSimpleCommand();
        }, waitForDomainPostActionsCompletion: true );

        await Task.Delay( 2500 );
        await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            var directState = device.GetSafeState();
            Throw.DebugAssert( directState != null );
            directState.SyncCommandCount.ShouldBe( 3 );
        }, waitForDomainPostActionsCompletion: true );

        TestHelper.Monitor.Info( "Disposing Domain..." );
        obs.Dispose( TestHelper.Monitor );
        TestHelper.Monitor.Info( "...Domain disposed, clearing host..." );
        await host.ClearAsync( TestHelper.Monitor, waitForDeviceDestroyed: true );
        TestHelper.Monitor.Info( "Host cleared." );
    }

    [Ignore("This is instable. Obs MUST be refactored!")]
    [Test]
    public async Task bridges_rebind_to_their_Device_when_reloaded_Async()
    {
        using var gLog = TestHelper.Monitor.OpenInfo( nameof( bridges_rebind_to_their_Device_when_reloaded_Async ) );
        // The device is available and running.
        var host = new SampleDeviceHost();
        var sp = new SimpleServiceContainer();
        sp.Add( host );
        var config = new SampleDeviceConfiguration()
        {
            Name = "n°1",
            PeriodMilliseconds = 5,
            Status = DeviceConfigurationStatus.RunnableStarted
        };
        (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).ShouldBe( DeviceApplyConfigurationResult.CreateAndStartSucceeded );

        var obs = new ObservableDomain( TestHelper.Monitor, nameof( bridges_rebind_to_their_Device_when_reloaded_Async ), true, serviceProvider: sp );
        try
        {
            // The bridge relay the event: the message is updated.
            OSampleDevice? device = null;
            await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                device = new OSampleDevice( "n°1" );
                device.DeviceControlStatus.ShouldBe( DeviceControlStatus.HasSharedControl );
                device.IsRunning.ShouldNotBeNull().ShouldBeTrue( "ConfigurationStatus is RunnableStarted." );
                device.SendSimpleCommand( "NEXT" );
            }, waitForDomainPostActionsCompletion: true );
            Debug.Assert( device != null );

            await Task.Delay( 300 );
            obs.Read( TestHelper.Monitor, () =>
            {
                device.Message.ShouldStartWith( "NEXT", Case.Sensitive, "The device Message has been updated by the bridge." );
            } );

            // Unloading/Reloading the domain.
            using( TestHelper.Monitor.OpenInfo( "Serializing/Deserializing with a fake transaction. Sidekicks are not instantiated." ) )
            {
                using var s = new MemoryStream();
                if( !obs.Save( TestHelper.Monitor, s, true ) ) throw new Exception( "Failed to save." );
                s.Position = 0;
                if( !obs.Load( TestHelper.Monitor, RewindableStream.FromStream( s ) ) ) throw new Exception( "Reload failed." );
            }
            obs.HasWaitingSidekicks.ShouldBeTrue();
            await obs.ModifyThrowAsync( TestHelper.Monitor, null );
            obs.HasWaitingSidekicks.ShouldBeFalse();

            device = obs.AllObjects.Items.OfType<OSampleDevice>().Single();
            await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                device.Message.ShouldStartWith( "NEXT" );
                device.SendSimpleCommand( "NEXT again" );
            } );
            int loopRequiredCount = 0;
            while( !obs.Read( TestHelper.Monitor, () => device.Message!.StartsWith( "NEXT again" ) ) )
            {
                ++loopRequiredCount;
                 await Task.Delay( 100 );
            }
            TestHelper.Monitor.Info( $"loopRequiredCount = {loopRequiredCount}." );
        }
        finally
        {
            TestHelper.Monitor.Info( "Disposing Domain..." );
            obs.Dispose( TestHelper.Monitor );
            TestHelper.Monitor.Info( "...Domain disposed, clearing host..." );
            await host.ClearAsync( TestHelper.Monitor, waitForDeviceDestroyed: true );
            TestHelper.Monitor.Info( "Host cleared." );
        }
    }


    [Test]
    public async Task unbound_devices_serialization_Async()
    {
        using var gLog = TestHelper.Monitor.OpenInfo( nameof( unbound_devices_serialization_Async ) );
        bool error = false;
        using( TestHelper.Monitor.OnError( () => error = true ) )
        {
            // There is no device.
            var host = new SampleDeviceHost();
            var sp = new SimpleServiceContainer();
            sp.Add( host );

            using var obs = new ObservableDomain( TestHelper.Monitor, nameof( unbound_devices_serialization_Async ), false, serviceProvider: sp );

            await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                var d1 = new OSampleDevice( "n°1" );
                d1.IsRunning.ShouldBeNull();
                d1.IsBoundDevice.ShouldBeFalse();
                var d2 = new OSampleDevice( "n°2" );
                d2.IsRunning.ShouldBeNull();
                d2.IsBoundDevice.ShouldBeFalse();
            } );
            ObservableDomain.IdempotenceSerializationCheck( TestHelper.Monitor, obs );
        }
        error.ShouldBeFalse( "There should be no error." );
    }

}
