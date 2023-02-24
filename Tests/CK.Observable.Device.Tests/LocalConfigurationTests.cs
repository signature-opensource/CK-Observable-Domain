using CK.BinarySerialization;
using CK.Core;
using CK.DeviceModel;
using FluentAssertions;
using NUnit.Framework;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Device.Tests
{
    public class LocalConfigurationTests
    {
        [SerializationVersion( 0 )]
        public class Root : ObservableRootObject
        {
            public readonly OSampleDeviceHost Host;
            public readonly OSampleDevice Default;

            public Root()
            {
                Host = new OSampleDeviceHost();
                Default = new OSampleDevice( "Default" );
            }

            public Root( IBinaryDeserializer d, ITypeReadInfo info )
                : base( Sliced.Instance )
            {
                Host = d.ReadObject<OSampleDeviceHost>();
                Default = d.ReadObject<OSampleDevice>();
            }

            public static void Write( IBinarySerializer s, in Root o )
            {
                s.WriteObject( o.Host );
                s.WriteObject( o.Default );
            }
        }

        [Test]
        public async Task check_equality_should_be_the_same_Async()
        {
            var sc = new SimpleServiceContainer();
            var config = new SampleDeviceConfiguration()
            {
                Name = "Default",
                Status = DeviceConfigurationStatus.AlwaysRunning,
                PeriodMilliseconds = 100,
                Message = "Not used here."
            };
            var host = new SampleDeviceHost();

            sc.Add( host );

            (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );
            var device = host["Default"];
            Debug.Assert( device != null );
            await device.WaitForSynchronizationAsync( considerDeferredCommands: false );

            var d = new ObservableDomain<Root>( TestHelper.Monitor, "Test", startTimer: false, sc );

            await d.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                d.Root.Default.DeviceConfiguration.Should().NotBeNull();
                d.Root.Default.LocalConfiguration.Value.Should().NotBeNull();
                d.Root.Default.LocalConfiguration.IsDirty.Should().BeFalse();

            } );

        }

        [Test]
        public async Task apply_local_config_Async()
        {
            var config = new SampleDeviceConfiguration()
            {
                Name = "Default",
                Status = DeviceConfigurationStatus.AlwaysRunning,
                PeriodMilliseconds = 100,
                Message = "Not used here."
            };
            var host = new SampleDeviceHost();

            (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );
            var device = host["Default"];
            Debug.Assert( device != null );
            await device.WaitForSynchronizationAsync( considerDeferredCommands: false );

            var sc = new SimpleServiceContainer();
            sc.Add( host );

            using var d1 = new ObservableDomain<Root>( TestHelper.Monitor, "Domain 1", startTimer: false, sc );
            using var d2 = new ObservableDomain<Root>( TestHelper.Monitor, "Domain 2", startTimer: false, sc );

            d1.HasWaitingSidekicks.Should().BeTrue();
            await d1.ModifyThrowAsync( TestHelper.Monitor, null );

            d2.HasWaitingSidekicks.Should().BeTrue();
            await d2.ModifyThrowAsync( TestHelper.Monitor, null );

            CheckStatus( d1, DeviceControlStatus.HasSharedControl );
            CheckStatus( d2, DeviceControlStatus.HasSharedControl );

            await d1.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                d1.Root.Default.LocalConfiguration.Value.Message = "WillChange";
                d1.Root.Default.LocalConfiguration.Value.PeriodMilliseconds = 2000;

            } );

            await ApplyLocalConfigAsync( d1, DeviceControlAction.TakeControl, device );
            CheckStatus( d1, DeviceControlStatus.HasControl );
            CheckStatus( d2, DeviceControlStatus.OutOfControl );
            device.ExternalConfiguration.Message.Should().Be( "WillChange" );

            await d2.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                d2.Root.Default.LocalConfiguration.Value.Message = "WillChange2";
                d2.Root.Default.LocalConfiguration.Value.PeriodMilliseconds = 4000;

            } );

            await ApplyLocalConfigAsync( d2, DeviceControlAction.SafeTakeControl, device );
            CheckStatus( d1, DeviceControlStatus.HasControl );
            CheckStatus( d2, DeviceControlStatus.OutOfControl );
            device.ExternalConfiguration.Message.Should().NotBe( "WillChange2" );

            await ApplyLocalConfigAsync( d2, DeviceControlAction.SafeReleaseControl, device );
            CheckStatus( d1, DeviceControlStatus.HasControl );
            CheckStatus( d2, DeviceControlStatus.OutOfControl );
            device.ExternalConfiguration.Message.Should().NotBe( "WillChange2" );

            await ApplyLocalConfigAsync( d1, DeviceControlAction.SafeReleaseControl, device );
            CheckStatus( d1, DeviceControlStatus.HasSharedControl );
            CheckStatus( d2, DeviceControlStatus.HasSharedControl );
            device.ExternalConfiguration.Message.Should().NotBe( "WillChange2" );

            await ApplyLocalConfigAsync( d2, DeviceControlAction.SafeTakeControl, device );
            CheckStatus( d1, DeviceControlStatus.OutOfControl );
            CheckStatus( d2, DeviceControlStatus.HasControl );
            device.ExternalConfiguration.Message.Should().Be( "WillChange2" );

            await ApplyLocalConfigAsync( d1, DeviceControlAction.ReleaseControl, device );
            CheckStatus( d1, DeviceControlStatus.HasSharedControl );
            CheckStatus( d2, DeviceControlStatus.HasSharedControl );
            device.ExternalConfiguration.Message.Should().Be( "WillChange" );

            await ApplyLocalConfigAsync( d2, DeviceControlAction.TakeControl, device );
            CheckStatus( d1, DeviceControlStatus.OutOfControl );
            CheckStatus( d2, DeviceControlStatus.HasControl );
            device.ExternalConfiguration.Message.Should().Be( "WillChange2" );

            await ApplyLocalConfigAsync( d1, DeviceControlAction.TakeControl, device );
            CheckStatus( d1, DeviceControlStatus.HasControl );
            CheckStatus( d2, DeviceControlStatus.OutOfControl );
            device.ExternalConfiguration.Message.Should().Be( "WillChange" );

            void CheckStatus( ObservableDomain<Root> d, DeviceControlStatus s )
            {
                d.Read( TestHelper.Monitor, () =>
                {
                    d.Root.Default.DeviceControlStatus.Should().Be( s );
                    d.Root.Host.Devices["Default"].Status.Should().Be( s );
                } );
            }

            async Task ApplyLocalConfigAsync( ObservableDomain<Root> d, DeviceControlAction dca, SampleDevice device )
            {
                await d.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    d.Root.Default.LocalConfiguration.SendDeviceConfigureCommand( dca );
                } );

                await Task.Delay( 100 );

                await device.WaitForSynchronizationAsync( false );

            }
        }

        [Test]
        public async Task apply_local_config_ownership_Async()
        {

            var config = new SampleDeviceConfiguration()
            {
                Name = "Default",
                ControllerKey = "Domain 1",
                Status = DeviceConfigurationStatus.AlwaysRunning,
                PeriodMilliseconds = 100,
                Message = "Not used here."
            };

            var host = new SampleDeviceHost();

            (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );
            var device = host["Default"];
            Debug.Assert( device != null );
            await device.WaitForSynchronizationAsync( considerDeferredCommands: false );

            var sc = new SimpleServiceContainer();
            sc.Add( host );

            using var d1 = new ObservableDomain<Root>( TestHelper.Monitor, "Domain 1", startTimer: false, sc );
            using var d2 = new ObservableDomain<Root>( TestHelper.Monitor, "Domain 2", startTimer: false, sc );

            d1.HasWaitingSidekicks.Should().BeTrue();
            await d1.ModifyThrowAsync( TestHelper.Monitor, null );

            d2.HasWaitingSidekicks.Should().BeTrue();
            await d2.ModifyThrowAsync( TestHelper.Monitor, null );

            CheckStatus( d1, DeviceControlStatus.HasOwnership );
            CheckStatus( d2, DeviceControlStatus.OutOfControlByConfiguration );

            await d2.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                d2.Root.Default.LocalConfiguration.Value.Message = "WillChange";
                d2.Root.Default.LocalConfiguration.Value.PeriodMilliseconds = 2000;
            } );

            await ApplyLocalConfigAsync( d2, DeviceControlAction.TakeControl, device );
            CheckStatus( d1, DeviceControlStatus.HasOwnership );
            CheckStatus( d2, DeviceControlStatus.OutOfControlByConfiguration );
            device.ControllerKey.Should().Be( "Domain 1" );
            device.ExternalConfiguration.Message.Should().NotBe( "WillChange" );

            await ApplyLocalConfigAsync( d2, DeviceControlAction.TakeOwnership, device );
            CheckStatus( d1, DeviceControlStatus.OutOfControlByConfiguration );
            CheckStatus( d2, DeviceControlStatus.HasOwnership );
            device.ControllerKey.Should().Be( "Domain 2" );
            device.ExternalConfiguration.Message.Should().Be( "WillChange" );

            await ApplyLocalConfigAsync( d2, DeviceControlAction.ForceReleaseControl, device );
            CheckStatus( d1, DeviceControlStatus.HasSharedControl );
            CheckStatus( d2, DeviceControlStatus.HasSharedControl );
            device.ControllerKey.Should().BeNull();
            device.ExternalConfiguration.Message.Should().Be( "WillChange" );

            void CheckStatus( ObservableDomain<Root> d, DeviceControlStatus s )
            {
                d.Read( TestHelper.Monitor, () =>
                {
                    d.Root.Default.DeviceControlStatus.Should().Be( s );
                    d.Root.Host.Devices["Default"].Status.Should().Be( s );
                } );
            }

            async Task ApplyLocalConfigAsync( ObservableDomain<Root> d, DeviceControlAction dca, SampleDevice device )
            {
                await d.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    d.Root.Default.LocalConfiguration.SendDeviceConfigureCommand( dca );
                } );
                // Since the reconfiguration currently goes through the host that sends an InternalConfigureDeviceCommand
                // to the device, waiting here for the synchronization is not enough: we unfortunately have
                // to wait for the InternalConfigureDeviceCommand to reach the command queue.
                await Task.Delay( 100 );
                await device.WaitForSynchronizationAsync( false );

            }

        }

        [Test]
        public async Task apply_local_config_null_Async()
        {
            var config = new SampleDeviceConfiguration()
            {
                Name = "Default",
                ControllerKey = "Domain 1",
                Status = DeviceConfigurationStatus.AlwaysRunning,
                PeriodMilliseconds = 100,
                Message = "Not used here."
            };
            var host = new SampleDeviceHost();

            (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );
            var device = host["Default"];
            Debug.Assert( device != null );
            await device.WaitForSynchronizationAsync( considerDeferredCommands: false );

            var sc = new SimpleServiceContainer();
            sc.Add( host );

            using var d1 = new ObservableDomain<Root>( TestHelper.Monitor, "Domain 1", startTimer: false, sc );
            using var d2 = new ObservableDomain<Root>( TestHelper.Monitor, "Domain 2", startTimer: false, sc );

            d1.HasWaitingSidekicks.Should().BeTrue();
            await d1.ModifyThrowAsync( TestHelper.Monitor, null );

            d2.HasWaitingSidekicks.Should().BeTrue();
            await d2.ModifyThrowAsync( TestHelper.Monitor, null );

            CheckStatus( d1, DeviceControlStatus.HasOwnership );
            CheckStatus( d2, DeviceControlStatus.OutOfControlByConfiguration );

            await d1.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                d1.Root.Default.LocalConfiguration.Value.Message = "WillChange";
                d1.Root.Default.LocalConfiguration.Value.PeriodMilliseconds = 2000;
            } );

            await ApplyLocalConfigAsync( d1, null, device );
            CheckStatus( d1, DeviceControlStatus.HasOwnership );
            CheckStatus( d2, DeviceControlStatus.OutOfControlByConfiguration );
            device.ExternalConfiguration.Message.Should().Be( "WillChange" );

            await ApplyLocalConfigAsync( d2, DeviceControlAction.TakeOwnership, device );
            CheckStatus( d1, DeviceControlStatus.OutOfControlByConfiguration );
            CheckStatus( d2, DeviceControlStatus.HasOwnership );

            await d1.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                d1.Root.Default.LocalConfiguration.Value.Message = "WontChange";
                d1.Root.Default.LocalConfiguration.Value.PeriodMilliseconds = 4000;
            } );

            await ApplyLocalConfigAsync( d1, null, device );
            CheckStatus( d1, DeviceControlStatus.OutOfControlByConfiguration );
            CheckStatus( d2, DeviceControlStatus.HasOwnership );
            device.ExternalConfiguration.Message.Should().NotBe( "WontChange" );
            device.ExternalConfiguration.Message.Should().Be( "Not used here." );

            void CheckStatus( ObservableDomain<Root> d, DeviceControlStatus s )
            {
                d.Read( TestHelper.Monitor, () =>
                {
                    d.Root.Default.DeviceControlStatus.Should().Be( s );
                    d.Root.Host.Devices["Default"].Status.Should().Be( s );
                } );
            }

            async Task ApplyLocalConfigAsync( ObservableDomain<Root> d, DeviceControlAction? dca, SampleDevice device )
            {
                await d.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    d.Root.Default.LocalConfiguration.SendDeviceConfigureCommand( dca );
                } );
                // Since the reconfiguration currently goes through the host that sends an InternalConfigureDeviceCommand
                // to the device, waiting here for the synchronization is not enough: we unfortunately have
                // to wait for the InternalConfigureDeviceCommand to reach the command queue.
                await Task.Delay( 100 );
                await device.WaitForSynchronizationAsync( false );

            }

        }

        [TestCase( DeviceControlAction.TakeOwnership, DeviceControlStatus.HasOwnership, DeviceControlStatus.OutOfControlByConfiguration )]
        [TestCase( DeviceControlAction.TakeControl, DeviceControlStatus.HasControl, DeviceControlStatus.OutOfControl )]
        [TestCase( DeviceControlAction.SafeTakeControl, DeviceControlStatus.HasControl, DeviceControlStatus.OutOfControl )]
        [TestCase( null, DeviceControlStatus.HasSharedControl, DeviceControlStatus.HasSharedControl )]
        public async Task apply_local_config_with_missing_device_Async(
            DeviceControlAction? applyOnDomain1,
            DeviceControlStatus statusExceptedOnDomain1,
            DeviceControlStatus statusExceptedOnDomain2 )
        {
            var host = new SampleDeviceHost();
            var sp = new SimpleServiceContainer();
            sp.Add( host );

            using var d1 = new ObservableDomain<Root>( TestHelper.Monitor, "Domain 1", startTimer: false, sp );
            using var d2 = new ObservableDomain<Root>( TestHelper.Monitor, "Domain 2", startTimer: false, sp );

            d1.HasWaitingSidekicks.Should().BeTrue();
            await d1.ModifyThrowAsync( TestHelper.Monitor, null );

            d2.HasWaitingSidekicks.Should().BeTrue();
            await d2.ModifyThrowAsync( TestHelper.Monitor, null );


            await d1.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                d1.Root.Default.IsBoundDevice.Should().BeFalse();
                d1.Root.Default.DeviceControlStatus.Should().Be( DeviceControlStatus.MissingDevice );
                d1.Root.Default.DeviceConfiguration.Should().BeNull();
                d1.Root.Default.Message.Should().BeNull();
                d1.Root.Default.IsRunning.Should().BeNull();

                d1.Root.Default.LocalConfiguration.Value.Name = "Default";
                d1.Root.Default.LocalConfiguration.Value.Status = DeviceConfigurationStatus.AlwaysRunning;
                d1.Root.Default.LocalConfiguration.Value.Message = "WillChange";
                d1.Root.Default.LocalConfiguration.Value.PeriodMilliseconds = 2000;

            } );

            await ApplyLocalConfigAsync( d1, applyOnDomain1 );
            var device = host["Default"];
            Debug.Assert( device != null );
            await device.WaitForSynchronizationAsync( false );
            CheckStatus( d1, statusExceptedOnDomain1 );
            CheckStatus( d2, statusExceptedOnDomain2 );

            void CheckStatus( ObservableDomain<Root> d, DeviceControlStatus s )
            {
                d.Read( TestHelper.Monitor, () =>
                {
                    d.Root.Default.DeviceControlStatus.Should().Be( s );
                    d.Root.Host.Devices["Default"].Status.Should().Be( s );
                } );
            }

            async Task ApplyLocalConfigAsync( ObservableDomain<Root> d, DeviceControlAction? dca )
            {
                await d.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    d.Root.Default.LocalConfiguration.SendDeviceConfigureCommand( dca );
                } );
                // Since the reconfiguration currently goes through the host that sends an InternalConfigureDeviceCommand
                // to the device, waiting here for the synchronization is not enough: we unfortunately have
                // to wait for the InternalConfigureDeviceCommand to reach the command queue.
                await Task.Delay( 2000 );
                //await device.WaitForSynchronizationAsync( false );

            }

        }


        [Test]
        public async Task check_equality_should_not_be_the_same_when_change_config_Async()
        {
            var sc = new SimpleServiceContainer();
            var config = new SampleDeviceConfiguration()
            {
                Name = "Default",
                Status = DeviceConfigurationStatus.AlwaysRunning,
                PeriodMilliseconds = 100,
                Message = "Not used here."
            };
            var host = new SampleDeviceHost();

            sc.Add( host );

            (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );
            var device = host["Default"];
            Debug.Assert( device != null );
            await device.WaitForSynchronizationAsync( considerDeferredCommands: false );


            var d = new ObservableDomain<Root>( TestHelper.Monitor, "Test", startTimer: false, sc );

            await d.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                d.Root.Default.DeviceConfiguration.Should().NotBeNull();
                d.Root.Default.LocalConfiguration.Value.Should().NotBeNull();

                d.Root.Default.LocalConfiguration.CheckDirty().Should().BeFalse();
                d.Root.Default.LocalConfiguration.IsDirty.Should().BeFalse();

                d.Root.Default.LocalConfiguration.Value.Message = "WillChange2";
                d.Root.Default.LocalConfiguration.Value.PeriodMilliseconds = 4000;

                d.Root.Default.LocalConfiguration.CheckDirty().Should().BeTrue();
                d.Root.Default.LocalConfiguration.IsDirty.Should().BeTrue();

            } );

        }

        [Test]
        public async Task check_idempotence_Async()
        {
            using var gLog = TestHelper.Monitor.OpenInfo( nameof( check_idempotence_Async ) );
            bool error = false;
            using( TestHelper.Monitor.OnError( () => error = true ) )
            {
                // There is no device.
                var host = new SampleDeviceHost();
                var sp = new SimpleServiceContainer();
                sp.Add( host );

                using var obs = new ObservableDomain( TestHelper.Monitor, nameof( check_idempotence_Async ), false, serviceProvider: sp );

                await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    var d1 = new OSampleDevice( "n°1" );
                    d1.LocalConfiguration.Value.Message = "Number1";
                    d1.LocalConfiguration.Value.PeriodMilliseconds = 2;

                    var d2 = new OSampleDevice( "n°2" );
                    d1.LocalConfiguration.Value.Message = "Number2";
                    d1.LocalConfiguration.Value.PeriodMilliseconds = 2;

                } );
                ObservableDomain.IdempotenceSerializationCheck( TestHelper.Monitor, obs );
            }
            error.Should().BeFalse( "There should be no error." );
        }

        [Test]
        public async Task check_idempotence_with_EnsureDevice_Async()
        {
            using var gLog = TestHelper.Monitor.OpenInfo( nameof( check_idempotence_Async ) );
            bool error = false;
            using( TestHelper.Monitor.OnError( () => error = true ) )
            {
                // There is no device.
                var host = new SampleDeviceHost();
                var sp = new SimpleServiceContainer();
                sp.Add( host );

                await host.EnsureDeviceAsync( TestHelper.Monitor, new SampleDeviceConfiguration()
                {
                    Name = "n°1"
                } );

                using var obs = new ObservableDomain( TestHelper.Monitor, nameof( check_idempotence_Async ), false, serviceProvider: sp );

                await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
                {
                    var d1 = new OSampleDevice( "n°1" );
                    var d2 = new OSampleDevice( "n°2" );
                } );
                ObservableDomain.IdempotenceSerializationCheck( TestHelper.Monitor, obs );
            }
            error.Should().BeFalse( "There should be no error." );
        }

        [Test]
        public async Task check_dirty_event_should_raised_Async()
        {
            var sc = new SimpleServiceContainer();
            var config = new SampleDeviceConfiguration()
            {
                Name = "Default",
                Status = DeviceConfigurationStatus.AlwaysRunning,
                PeriodMilliseconds = 100,
                Message = "Not used here."
            };
            var host = new SampleDeviceHost();

            sc.Add( host );

            (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );
            var device = host["Default"];
            Debug.Assert( device != null );
            await device.WaitForSynchronizationAsync( considerDeferredCommands: false );

            var d = new ObservableDomain<Root>( TestHelper.Monitor, "Test", startTimer: false, sc );


            await d.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                d.Root.Default.DeviceConfiguration.Should().NotBeNull();
                d.Root.Default.LocalConfiguration.Value.Should().NotBeNull();

                d.Root.Default.LocalConfiguration.IsDirty.Should().Be( d.Root.Default._dirtyRaisedEventValue );

                d.Root.Default.LocalConfiguration.Value.Message = "WillChange2";
                d.Root.Default.LocalConfiguration.Value.PeriodMilliseconds = 4000;

                d.Root.Default.LocalConfiguration.IsDirty.Should().Be( d.Root.Default._dirtyRaisedEventValue );

                d.Root.Default.LocalConfiguration.CheckDirty().Should().BeTrue();


            } );

        }
    }


}
