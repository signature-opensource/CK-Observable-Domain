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
using System.Threading.Tasks;
using System.Xml;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Device.Tests
{
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
                device1.LocalConfiguration.Value.Name.Should().Be( "n°1" );

                if( createHostStep == "AfterObservableDevice" ) oHost = new OSampleDeviceHost();

                if( oHost != null )
                {
                    oHost.Devices.Should().HaveCount( 1 );
                    oHost.Devices["n°1"].DeviceName.Should().Be( "n°1" );
                    oHost.Devices["n°1"].Object.Should().Be( device1 );
                    oHost.Devices["n°1"].IsRunning.Should().Be( false );
                    oHost.Devices["n°1"].Status.Should().Be( DeviceControlStatus.MissingDevice );
                }
                device1.IsBoundDevice.Should().BeFalse();
                device1.DeviceControlStatus.Should().Be( DeviceControlStatus.MissingDevice );
                device1.DeviceConfiguration.Should().BeNull();
                device1.Message.Should().BeNull();
                device1.IsRunning.Should().BeNull();
            } );
            Debug.Assert( device1 != null );

            var config = new SampleDeviceConfiguration()
            {
                Name = "n°1",
                Message = "Hello World!",
                PeriodMilliseconds = 5
            };
            (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateSucceeded );

            await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                device1.IsBoundDevice.Should().BeTrue();
                Debug.Assert( device1.DeviceConfiguration != null );
                device1.DeviceConfiguration.Status.Should().Be( DeviceConfigurationStatus.Disabled );
                device1.DeviceControlStatus.Should().Be( DeviceControlStatus.HasSharedControl, "Since there is no ControllerKey on the device, anybody can control it." );
                device1.Message.Should().BeNull();
                device1.IsRunning.Should().BeFalse();

                if( oHost == null ) oHost = new OSampleDeviceHost();
                oHost.Devices.Should().HaveCount( 1 );
                oHost.Devices["n°1"].DeviceName.Should().Be( "n°1" );
                oHost.Devices["n°1"].Object.Should().Be( device1 );
                oHost.Devices["n°1"].IsRunning.Should().Be( false );
                oHost.Devices["n°1"].Status.Should().Be( DeviceControlStatus.HasSharedControl );
            } ); 

            config.Status = DeviceConfigurationStatus.AlwaysRunning;
            (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.UpdateSucceeded );

            obs.Read( TestHelper.Monitor, () =>
            {
                device1.DeviceConfiguration!.Status.Should().Be( DeviceConfigurationStatus.AlwaysRunning );
                device1.IsRunning.Should().BeTrue();
                oHost!.Devices["n°1"].IsRunning.Should().BeTrue();
            } );

            config.ControllerKey = obs.DomainName;
            (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.UpdateSucceeded );
            obs.Read( TestHelper.Monitor, () =>
            {
                device1.DeviceControlStatus.Should().Be( DeviceControlStatus.HasOwnership );
                oHost!.Devices["n°1"].Status.Should().Be( DeviceControlStatus.HasOwnership );
            } );

            config.ControllerKey = obs.DomainName + "No more!";
            (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.UpdateSucceeded );

            obs.Read( TestHelper.Monitor, () =>
            {
                device1.DeviceControlStatus.Should().Be( DeviceControlStatus.OutOfControlByConfiguration );
                oHost!.Devices["n°1"].Status.Should().Be( DeviceControlStatus.OutOfControlByConfiguration );
            } );

            await host.Find( "n°1" )!.DestroyAsync( TestHelper.Monitor );

            await Task.Delay( 150 );

            obs.Read( TestHelper.Monitor, () =>
            {
                device1.DeviceConfiguration.Should().BeNull();
                device1.IsRunning.Should().BeNull();
                device1.DeviceControlStatus.Should().Be( DeviceControlStatus.MissingDevice );

                Throw.DebugAssert( oHost != null );
                oHost.Devices["n°1"].DeviceName.Should().Be( "n°1" );
                oHost.Devices["n°1"].Object.Should().Be( device1 );
                oHost.Devices["n°1"].IsRunning.Should().BeFalse();
                oHost.Devices["n°1"].Status.Should().Be( DeviceControlStatus.MissingDevice );
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
            (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );

            using var obs = new ObservableDomain(TestHelper.Monitor, nameof(Start_and_Stop_commands_Async), true, serviceProvider: sp );


            OSampleDevice? device = null;
            await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                device = new OSampleDevice( "The device..." );
                device.IsRunningChanged += Device_IsRunningChanged;
                Debug.Assert( device.IsRunning != null );
                device.IsRunning.Should().BeTrue( "ConfigurationStatus is RunnableStarted." );
                device.DeviceControlStatus.Should().NotBe( DeviceControlStatus.MissingDevice );
                device.DeviceConfiguration.Should().NotBeNull();
            } );
            bool isRunning = true;

            DeviceIsRunningChanged.Should().BeFalse();
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
            DeviceIsRunningChanged.Should().BeTrue();
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
            DeviceIsRunningChanged.Should().BeTrue();
            DeviceIsRunningChanged = false;

            await host.Find( "The device..." )!.DestroyAsync( TestHelper.Monitor );
            await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                device.IsBoundDevice.Should().BeFalse();
                device.IsRunning.Should().BeNull();
                device.DeviceControlStatus.Should().Be( DeviceControlStatus.MissingDevice );
                device.DeviceConfiguration.Should().BeNull();
            }, waitForDomainPostActionsCompletion: true );
            DeviceIsRunningChanged.Should().BeTrue();
            DeviceIsRunningChanged = false;

            TestHelper.Monitor.Info( "Disposing Domain..." );
            obs.Dispose( TestHelper.Monitor );
            TestHelper.Monitor.Info( "...Domain disposed, clearing host..." );
            await host.ClearAsync( TestHelper.Monitor, waitForDeviceDestroyed: true );
            TestHelper.Monitor.Info( "Host cleared." );
        }

        [Test]
        [Timeout( 2*1000 )]
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
                PeriodMilliseconds = 5,
                Status = DeviceConfigurationStatus.RunnableStarted
            };
            (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );

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

            await Task.Delay( 100 );
            await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                var directState = device.GetSafeState();
                Throw.DebugAssert( directState != null );
                // The OnConfigurationChanged sent a SampleCommad.
                directState.SyncCommandCount.Should().Be( 2 );
                device.SendSimpleCommand();
            }, waitForDomainPostActionsCompletion: true );

            await Task.Delay( 250 );
            await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                var directState = device.GetSafeState();
                Throw.DebugAssert( directState != null );
                directState.SyncCommandCount.Should().Be( 3 );
            }, waitForDomainPostActionsCompletion: true );

            TestHelper.Monitor.Info( "Disposing Domain..." );
            obs.Dispose( TestHelper.Monitor );
            TestHelper.Monitor.Info( "...Domain disposed, clearing host..." );
            await host.ClearAsync( TestHelper.Monitor, waitForDeviceDestroyed: true );
            TestHelper.Monitor.Info( "Host cleared." );
        }


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
            (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );

            var obs = new ObservableDomain( TestHelper.Monitor, nameof( bridges_rebind_to_their_Device_when_reloaded_Async ), true, serviceProvider: sp );
            try
            {
                // The bridge relay the event: the message is updated.
                OSampleDevice? device = null;
                await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    device = new OSampleDevice( "n°1" );
                    device.DeviceControlStatus.Should().Be( DeviceControlStatus.HasSharedControl );
                    device.IsRunning.Should().BeTrue( "ConfigurationStatus is RunnableStarted." );
                    device.SendSimpleCommand( "NEXT" );
                }, waitForDomainPostActionsCompletion: true );
                Debug.Assert( device != null );

                await Task.Delay( 300 );
                obs.Read( TestHelper.Monitor, () =>
                {
                    device.Message.Should().StartWith( "NEXT", "The device Message has been updated by the bridge." );
                } );

                // Unloading/Reloading the domain.
                using( TestHelper.Monitor.OpenInfo( "Serializing/Deserializing with a fake transaction. Sidekicks are not instantiated." ) )
                {
                    using var s = new MemoryStream();
                    if( !obs.Save( TestHelper.Monitor, s, true ) ) throw new Exception( "Failed to save." );
                    s.Position = 0;
                    if( !obs.Load( TestHelper.Monitor, RewindableStream.FromStream( s ) ) ) throw new Exception( "Reload failed." );
                }
                obs.HasWaitingSidekicks.Should().BeTrue();
                await obs.ModifyThrowAsync( TestHelper.Monitor, null );
                obs.HasWaitingSidekicks.Should().BeFalse();

                device = obs.AllObjects.Items.OfType<OSampleDevice>().Single();
                await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    device.Message.Should().StartWith( "NEXT" );
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
                    d1.IsRunning.Should().BeNull();
                    d1.IsBoundDevice.Should().BeFalse();
                    var d2 = new OSampleDevice( "n°2" );
                    d2.IsRunning.Should().BeNull();
                    d2.IsBoundDevice.Should().BeFalse();
                } );
                ObservableDomain.IdempotenceSerializationCheck( TestHelper.Monitor, obs );
            }
            error.Should().BeFalse( "There should be no error." );
        }

    }
}
