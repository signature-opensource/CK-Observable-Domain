using CK.Core;
using CK.DeviceModel;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Device.Tests
{
    [TestFixture]
    public class DeviceBridgeTests
    {
        [Test]
        public async Task sample_obervable()
        {
            var host = new SampleDeviceHost( new DefaultDeviceAlwaysRunningPolicy() );
            var sp = new SimpleServiceContainer();
            sp.Add( host );

            using var obs = new ObservableDomain(TestHelper.Monitor, nameof(sample_obervable), false, serviceProvider: sp );

            OSampleDevice? device1 = null;
            await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                device1 = new OSampleDevice( "n°1" );

                device1.HasDeviceControl.Should().BeFalse();
                device1.ConfigurationStatus.Should().BeNull();
                device1.Message.Should().BeNull();
                device1.Status.Should().BeNull();
            } );
            Debug.Assert( device1 != null );

            var config = new SampleDeviceConfiguration()
            {
                Name = "n°1",
                Message = "Hello World!",
                PeriodMilliseconds = 5
            };
            (await host.ApplyDeviceConfigurationAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateSucceeded );

            using( obs.AcquireReadLock() )
            {
                device1.ConfigurationStatus.Should().Be( DeviceConfigurationStatus.Disabled );
                device1.HasDeviceControl.Should().BeTrue( "Since there is no ControllerKey on the device, anybody can control it." );
                device1.HasExclusiveDeviceControl.Should().BeFalse();
                device1.Message.Should().BeNull();
                Debug.Assert( device1.Status != null );
                device1.Status.Value.IsRunning.Should().BeFalse();

                device1.Status.Value.IsDestroyed.Should().BeFalse();
                device1.Status.Value.HasBeenReconfigured.Should().BeFalse();
                device1.Status.Value.HasStarted.Should().BeFalse();
                device1.Status.Value.HasStopped.Should().BeFalse( "The device is not running... but it has not been stopped." );
                device1.Status.Value.StoppedReason.Should().Be( DeviceStoppedReason.None );
                device1.Status.Value.StartedReason.Should().Be( DeviceStartedReason.None );
                device1.Status.Value.ReconfiguredResult.Should().Be( DeviceReconfiguredResult.None );
            }

            config.Status = DeviceConfigurationStatus.AlwaysRunning;
            (await host.ApplyDeviceConfigurationAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.UpdateSucceeded );

            using( obs.AcquireReadLock() )
            {
                device1.ConfigurationStatus.Should().Be( DeviceConfigurationStatus.AlwaysRunning );
                device1.HasDeviceControl.Should().BeTrue();
                device1.HasExclusiveDeviceControl.Should().BeFalse();
                device1.Status.Value.IsRunning.Should().BeTrue();
                device1.Status.Value.StartedReason.Should().Be( DeviceStartedReason.StartedByAlwaysRunningConfiguration );
            }

            config.ControllerKey = obs.DomainName;
            (await host.ApplyDeviceConfigurationAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.UpdateSucceeded );
            using( obs.AcquireReadLock() )
            {
                device1.HasDeviceControl.Should().BeTrue();
                device1.HasExclusiveDeviceControl.Should().BeTrue( "This time, it is this domain that controls." );
            }

            config.ControllerKey = obs.DomainName + "No more!";
            (await host.ApplyDeviceConfigurationAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.UpdateSucceeded );

            using( obs.AcquireReadLock() )
            {
                device1.HasDeviceControl.Should().BeFalse();
                device1.HasExclusiveDeviceControl.Should().BeFalse();
            }

            await host.DestroyDeviceAsync( TestHelper.Monitor, "n°1" );

            using( obs.AcquireReadLock() )
            {
                device1.Status.Should().BeNull();
                device1.HasDeviceControl.Should().BeFalse();
                device1.HasExclusiveDeviceControl.Should().BeFalse();
            }

        }


        static bool DeviceStatusChanged = false;

        static void Device_StatusChanged( object sender )
        {
            DeviceStatusChanged = true;
        }

        [Test]
        public async Task Start_and_Stop_commands()
        {
            DeviceStatusChanged = false;

            var host = new SampleDeviceHost( new DefaultDeviceAlwaysRunningPolicy() );
            var sp = new SimpleServiceContainer();
            sp.Add( host );
            var config = new SampleDeviceConfiguration()
            {
                Name = "The device...",
                Message = "Hello World!",
                PeriodMilliseconds = 150,
                Status = DeviceConfigurationStatus.RunnableStarted
            };
            (await host.ApplyDeviceConfigurationAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );

            using var obs = new ObservableDomain(TestHelper.Monitor, nameof(Start_and_Stop_commands), true, serviceProvider: sp );


            OSampleDevice? device = null;
            await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                device = new OSampleDevice( "The device..." );
                device.StatusChanged += Device_StatusChanged;
                Debug.Assert( device.Status != null );
                device.Status.Value.IsRunning.Should().BeTrue( "ConfigurationStatus is RunnableStarted." );

            } );
            bool isRunning = true;

            DeviceStatusChanged.Should().BeFalse();
            Debug.Assert( device != null );
            Debug.Assert( device.Status != null );

            await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                device.CmdStop();
            } );

            while( isRunning )
            {
                using( obs.AcquireReadLock() )
                {
                    isRunning = device.Status.Value.IsRunning;
                }
            }
            DeviceStatusChanged.Should().BeTrue();
            DeviceStatusChanged = false;

            await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                device.CmdStart();
            } );

            while( !isRunning )
            {
                using( obs.AcquireReadLock() )
                {
                    isRunning = device.Status.Value.IsRunning;
                }
            }
            DeviceStatusChanged.Should().BeTrue();
            DeviceStatusChanged = false;

            await host.DestroyDeviceAsync( TestHelper.Monitor, "The device..." );
            await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                device.IsBoundDevice.Should().BeFalse();
                device.Status.Should().BeNull();
            } );
            DeviceStatusChanged.Should().BeTrue();
            DeviceStatusChanged = false;

            await host.ClearAsync( TestHelper.Monitor );
        }

        [Test]
        public async Task commands_are_easy_to_send()
        {
            var host = new SampleDeviceHost( new DefaultDeviceAlwaysRunningPolicy() );
            var sp = new SimpleServiceContainer();
            sp.Add( host );
            var config = new SampleDeviceConfiguration()
            {
                Name = "n°1",
                // We loop fast: the device.GetSafeState() is updated by the device loop.
                PeriodMilliseconds = 5,
                Status = DeviceConfigurationStatus.RunnableStarted
            };
            (await host.ApplyDeviceConfigurationAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );

            using var obs = new ObservableDomain(TestHelper.Monitor, nameof(commands_are_easy_to_send), true, serviceProvider: sp );

            OSampleDevice? device = null;
            await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                device = new OSampleDevice( "n°1" );
                Debug.Assert( device.Status != null );
                device.Status.Value.IsRunning.Should().BeTrue( "ConfigurationStatus is RunnableStarted." );
                device.CmdCommandSync();
            } );
            Debug.Assert( device != null );
            Debug.Assert( device.Status != null );

            System.Threading.Thread.Sleep( 20 );
            await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                var directState = device.GetSafeState();
                Debug.Assert( directState != null );
                directState.SyncCommandCount.Should().Be( 1 );
                directState.AsyncCommandCount.Should().Be( 0 );
                device.CmdCommandSync();
                device.CmdCommandAsync();
            } );

            System.Threading.Thread.Sleep( 20 );
            await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                var directState = device.GetSafeState();
                Debug.Assert( directState != null );
                directState.SyncCommandCount.Should().Be( 2 );
                directState.AsyncCommandCount.Should().Be( 1 );
            } );

            await host.ClearAsync( TestHelper.Monitor );
        }
    }
}
