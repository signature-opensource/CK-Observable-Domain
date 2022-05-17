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
        [Test]
        [Timeout( 2 * 1000 )]
        public async Task sample_obervable()
        {
            using var _ = TestHelper.Monitor.OpenInfo( nameof( sample_obervable ) );
            var host = new SampleDeviceHost();
            var sp = new SimpleServiceContainer();
            sp.Add( host );

            using var obs = new ObservableDomain(TestHelper.Monitor, nameof(sample_obervable), false, serviceProvider: sp );

            OSampleDevice? device1 = null;
            await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                device1 = new OSampleDevice( "n°1" );

                device1.HasDeviceControl.Should().BeFalse();
                device1.Configuration.Should().BeNull();
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
            (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateSucceeded );

            using( obs.AcquireReadLock() )
            {
                Debug.Assert( device1.Configuration != null );
                device1.Configuration.Status.Should().Be( DeviceConfigurationStatus.Disabled );
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
            (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.UpdateSucceeded );

            using( obs.AcquireReadLock() )
            {
                device1.Configuration.Status.Should().Be( DeviceConfigurationStatus.AlwaysRunning );
                device1.HasDeviceControl.Should().BeTrue();
                device1.HasExclusiveDeviceControl.Should().BeFalse();
                device1.Status.Value.IsRunning.Should().BeTrue();
                device1.Status.Value.StartedReason.Should().Be( DeviceStartedReason.StartedByAlwaysRunningConfiguration );
            }

            config.ControllerKey = obs.DomainName;
            (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.UpdateSucceeded );
            using( obs.AcquireReadLock() )
            {
                device1.HasDeviceControl.Should().BeTrue();
                device1.HasExclusiveDeviceControl.Should().BeTrue( "This time, it is this domain that controls." );
            }

            config.ControllerKey = obs.DomainName + "No more!";
            (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.UpdateSucceeded );

            using( obs.AcquireReadLock() )
            {
                device1.HasDeviceControl.Should().BeFalse();
                device1.HasExclusiveDeviceControl.Should().BeFalse();
            }

            await host.Find( "n°1" )!.DestroyAsync( TestHelper.Monitor );

            using( obs.AcquireReadLock() )
            {
                device1.Configuration.Should().BeNull();
                device1.Status.Should().BeNull();
                device1.HasDeviceControl.Should().BeFalse();
                device1.HasExclusiveDeviceControl.Should().BeFalse();
            }

            TestHelper.Monitor.Info( "Disposing Domain..." );
            obs.Dispose( TestHelper.Monitor );
            TestHelper.Monitor.Info( "...Domain disposed, clearing host..." );
            await host.ClearAsync( TestHelper.Monitor, waitForDeviceDestroyed: true );
            TestHelper.Monitor.Info( "Host cleared." );
        }


        static bool DeviceStatusChanged = false;

        static void Device_StatusChanged( object sender )
        {
            DeviceStatusChanged = true;
        }

        [Test]
        [Timeout( 2 * 1000 )]
        public async Task Start_and_Stop_commands()
        {
            using var gLog = TestHelper.Monitor.OpenInfo( nameof( Start_and_Stop_commands ) );
            DeviceStatusChanged = false;

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
                device.SendStopDeviceCommand();
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
                device.SendStartDeviceCommand();
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

            await host.Find( "The device..." )!.DestroyAsync( TestHelper.Monitor );
            await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                device.IsBoundDevice.Should().BeFalse();
                device.Status.Should().BeNull();
            } );
            DeviceStatusChanged.Should().BeTrue();
            DeviceStatusChanged = false;

            TestHelper.Monitor.Info( "Disposing Domain..." );
            obs.Dispose( TestHelper.Monitor );
            TestHelper.Monitor.Info( "...Domain disposed, clearing host..." );
            await host.ClearAsync( TestHelper.Monitor, waitForDeviceDestroyed: true );
            TestHelper.Monitor.Info( "Host cleared." );
        }

        [Test]
        [Timeout( 2*1000 )]
        public async Task commands_are_easy_to_send()
        {
            using var gLog = TestHelper.Monitor.OpenInfo( nameof( commands_are_easy_to_send ) );
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

            using var obs = new ObservableDomain(TestHelper.Monitor, nameof(commands_are_easy_to_send), true, serviceProvider: sp );

            OSampleDevice? device = null;
            await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                device = new OSampleDevice( "n°1" );
                Debug.Assert( device.Status != null );
                device.Status.Value.IsRunning.Should().BeTrue( "ConfigurationStatus is RunnableStarted." );
                device.SendSimpleCommand();
            } );
            Debug.Assert( device != null );
            Debug.Assert( device.Status != null );

            System.Threading.Thread.Sleep( 20 );
            await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                var directState = device.GetSafeState();
                Debug.Assert( directState != null );
                directState.SyncCommandCount.Should().Be( 1 );
                device.SendSimpleCommand();
            } );

            System.Threading.Thread.Sleep( 20 );
            await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                var directState = device.GetSafeState();
                Debug.Assert( directState != null );
                directState.SyncCommandCount.Should().Be( 2 );
            } );

            TestHelper.Monitor.Info( "Disposing Domain..." );
            obs.Dispose( TestHelper.Monitor );
            TestHelper.Monitor.Info( "...Domain disposed, clearing host..." );
            await host.ClearAsync( TestHelper.Monitor, waitForDeviceDestroyed: true );
            TestHelper.Monitor.Info( "Host cleared." );
        }


        [Test]
        [Timeout( 2 * 1000 )]
        public async Task bridges_rebind_to_their_Device_when_reloaded()
        {
            using var gLog = TestHelper.Monitor.OpenInfo( nameof( bridges_rebind_to_their_Device_when_reloaded ) );
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

            using var obs = new ObservableDomain( TestHelper.Monitor, nameof( bridges_rebind_to_their_Device_when_reloaded ), true, serviceProvider: sp );

            // The bridge relay the event: the message is updated.
            OSampleDevice? device = null;
            await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                device = new OSampleDevice( "n°1" );
                Debug.Assert( device.Status != null );
                device.Status.Value.IsRunning.Should().BeTrue( "ConfigurationStatus is RunnableStarted." );
                device.SendSimpleCommand( "NEXT" );
            } );
            Debug.Assert( device != null );
            Debug.Assert( device.Status != null );

            System.Threading.Thread.Sleep( 90 );
            using( obs.AcquireReadLock() )
            {
                device.Message.Should().StartWith( "NEXT", "The device Message has been updated by the bridge." );
            }

            // Unloading/Reloading the domain.
            using( TestHelper.Monitor.OpenInfo( "Serializing/Deserializing." ) )
            {
                using var s = new MemoryStream();
                if( !obs.Save( TestHelper.Monitor, s, true ) ) throw new Exception( "Failed to save." );
                s.Position = 0;
                if( !obs.Load( TestHelper.Monitor, RewindableStream.FromStream( s ) ) ) throw new Exception( "Reload failed." );
            }

            device = obs.AllObjects.OfType<OSampleDevice>().Single();
            await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                device.Message.Should().StartWith( "NEXT" );
                device.SendSimpleCommand( "NEXT again" );
            } );
            System.Threading.Thread.Sleep( 40 );
            using( obs.AcquireReadLock() )
            {
                device.Message.Should().StartWith( "NEXT again" );
            }

            TestHelper.Monitor.Info( "Disposing Domain..." );
            obs.Dispose( TestHelper.Monitor );
            TestHelper.Monitor.Info( "...Domain disposed, clearing host..." );
            await host.ClearAsync( TestHelper.Monitor, waitForDeviceDestroyed: true );
            TestHelper.Monitor.Info( "Host cleared." );
        }


        [Test]
        public async Task unbound_devices_serialization()
        {
            using var gLog = TestHelper.Monitor.OpenInfo( nameof( unbound_devices_serialization ) );
            bool error = false;
            using( TestHelper.Monitor.OnError( () => error = true ) )
            {
                // There is no device.
                var host = new SampleDeviceHost();
                var sp = new SimpleServiceContainer();
                sp.Add( host );

                using var obs = new ObservableDomain( TestHelper.Monitor, nameof( unbound_devices_serialization ), false, serviceProvider: sp );

                await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    var d1 = new OSampleDevice( "n°1" );
                    d1.Status.Should().BeNull();
                    d1.IsBoundDevice.Should().BeFalse();
                    var d2 = new OSampleDevice( "n°2" );
                    d2.Status.Should().BeNull();
                    d2.IsBoundDevice.Should().BeFalse();
                } );
                ObservableDomain.IdempotenceSerializationCheck( TestHelper.Monitor, obs );
            }
            error.Should().BeFalse( "There should be no error." );
        }

    }
}
