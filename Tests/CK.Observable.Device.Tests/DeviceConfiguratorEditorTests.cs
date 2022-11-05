using CK.BinarySerialization;
using CK.Core;
using CK.DeviceModel;
using FluentAssertions;
using NUnit.Framework;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Device.Tests
{
    public class DeviceConfiguratorEditorTests
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

        //[Test]
        //public async Task check_equality_should_be_the_same_Async()
        //{
        //    var sc = new SimpleServiceContainer();
        //    var config = new SampleDeviceConfiguration()
        //    {
        //        Name = "Default",
        //        Status = DeviceConfigurationStatus.AlwaysRunning,
        //        PeriodMilliseconds = 100,
        //        Message = "Not used here."
        //    };
        //    var host = new SampleDeviceHost();

        //    sc.Add( host );

        //    (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );
        //    var device = host["Default"];
        //    Debug.Assert( device != null );
        //    await device.WaitForSynchronizationAsync( considerDeferredCommands: false );

        //    var d = new ObservableDomain<Root>( TestHelper.Monitor, "Test", startTimer: false, sc );

        //    await d.ModifyThrowAsync( TestHelper.Monitor, () =>
        //    {
        //        d.Root.Default.Configuration.Should().NotBeNull();
        //        d.Root.Default.DeviceConfigurationEditor.Should().NotBeNull();
        //        d.Root.Default.DeviceConfigurationEditor.IsSame().Should().BeTrue();

        //    } );

        //}

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
                d1.Root.Default.DeviceConfigurationEditor.Local.Message = "WillChange";

                //THIS IS NOT NORMAL, PROBLEM WITH INITIALIZATION CONFIG/EVENT CONFIG CHANGD
                //d1.Root.Default.DeviceConfigurationEditor.Local.Name = "Default";

            } );

            await ApplyLocalConfigAsync( d1, DeviceControlAction.TakeControl, device );
            CheckStatus( d1, DeviceControlStatus.HasControl );
            CheckStatus( d2, DeviceControlStatus.OutOfControl );
            device.ExternalConfiguration.Message.Should().Be( "WillChange" );

            await d2.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                d2.Root.Default.DeviceConfigurationEditor.Local.Message = "WillChange2";
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
                    d.Root.Default.DeviceConfigurationEditor.ApplyLocalConfig( dca );
                } );

                await Task.Delay( 100 );

                await device.WaitForSynchronizationAsync( false );

            }
        }
        //[Test]
        //public async Task apply_local_config_ownership_Async()
        //{
        //    var sc = new SimpleServiceContainer();
        //    var config = new SampleDeviceConfiguration()
        //    {
        //        Name = "Default",
        //        ControllerKey = "Domain 1",
        //        Status = DeviceConfigurationStatus.AlwaysRunning,
        //        PeriodMilliseconds = 100,
        //        Message = "Not used here."
        //    };
        //    var host = new SampleDeviceHost();
         

        //    (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );
        //    var device = host["Default"];
        //    Debug.Assert( device != null );
        //    await device.WaitForSynchronizationAsync( considerDeferredCommands: false );

        //    var sc = new SimpleServiceContainer();
        //    sc.Add( host );

        //    using var d1 = new ObservableDomain<Root>( TestHelper.Monitor, "Domain 1", startTimer: false, sc );
        //    using var d2 = new ObservableDomain<Root>( TestHelper.Monitor, "Domain 2", startTimer: false, sc );

        //    d1.HasWaitingSidekicks.Should().BeTrue();
        //    await d1.ModifyThrowAsync( TestHelper.Monitor, null );

        //    d2.HasWaitingSidekicks.Should().BeTrue();
        //    await d2.ModifyThrowAsync( TestHelper.Monitor, null );

        //    CheckStatus( d1, DeviceControlStatus.HasOwnership );
        //    CheckStatus( d2, DeviceControlStatus.OutOfControlByConfiguration );

        //    await d2.ModifyThrowAsync( TestHelper.Monitor, () =>
        //    {
        //        d2.Root.Default.DeviceConfigurationEditor.Local.Strips[0].Mappings[0].DeviceName = "WillChange";
        //    } );

        //    await ApplyLocalConfigAsync( d2, DeviceControlAction.TakeControl, device );
        //    CheckStatus( d1, DeviceControlStatus.HasOwnership );
        //    CheckStatus( d2, DeviceControlStatus.OutOfControlByConfiguration );
        //    device.ControllerKey.Should().Be( "Domain 1" );
        //    device.ExternalConfiguration.Strips[0].Mappings.Where( m => m.DeviceName == "WillChange" ).Should().HaveCount( 0 );

        //    await ApplyLocalConfigAsync( d2, DeviceControlAction.TakeOwnership, device );
        //    CheckStatus( d1, DeviceControlStatus.OutOfControlByConfiguration );
        //    CheckStatus( d2, DeviceControlStatus.HasOwnership );
        //    device.ControllerKey.Should().Be( "Domain 2" );
        //    device.ExternalConfiguration.Strips[0].Mappings.Where( m => m.DeviceName == "WillChange" ).Should().HaveCount( 1 );

        //    await ApplyLocalConfigAsync( d2, DeviceControlAction.ForceReleaseControl, device );
        //    CheckStatus( d1, DeviceControlStatus.HasSharedControl );
        //    CheckStatus( d2, DeviceControlStatus.HasSharedControl );
        //    device.ControllerKey.Should().BeNull();
        //    device.ExternalConfiguration.Strips[0].Mappings.Where( m => m.DeviceName == "WillChange" ).Should().HaveCount( 1 );

        //    void CheckStatus( ObservableDomain<Root> d, DeviceControlStatus s )
        //    {
        //        d.Read( TestHelper.Monitor, () =>
        //        {
        //            d.Root.Default.DeviceControlStatus.Should().Be( s );
        //            d.Root.Host.Devices["Default"].Status.Should().Be( s );
        //        } );
        //    }

        //    async Task ApplyLocalConfigAsync( ObservableDomain<Root> d, DeviceControlAction dca, LEDStripDevice device )
        //    {
        //        await d.ModifyThrowAsync( TestHelper.Monitor, () =>
        //        {
        //            d.Root.Default.DeviceConfigurationEditor.ApplyLocalConfig( dca );
        //        } );
        //        // Since the reconfiguration currently goes through the host that sends an InternalConfigureDeviceCommand
        //        // to the device, waiting here for the synchronization is not enough: we unfortunately have
        //        // to wait for the InternalConfigureDeviceCommand to reach the command queue.
        //        await Task.Delay( 100 );
        //        await device.WaitForSynchronizationAsync( false );

        //    }

        //}

        //[Test]
        //public async Task apply_local_config_null_Async()
        //{
        //    var sc = new SimpleServiceContainer();
        //    var config = new SampleDeviceConfiguration()
        //    {
        //        Name = "Default",
        //        ControllerKey = "Domain 1",
        //        Status = DeviceConfigurationStatus.AlwaysRunning,
        //        PeriodMilliseconds = 100,
        //        Message = "Not used here."
        //    };
        //    var host = new SampleDeviceHost();

        //    (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );
        //    var device = host["Default"];
        //    Debug.Assert( device != null );
        //    await device.WaitForSynchronizationAsync( considerDeferredCommands: false );

        //    var sc = new SimpleServiceContainer();
        //    sc.Add( host );

        //    using var d1 = new ObservableDomain<Root>( TestHelper.Monitor, "Domain 1", startTimer: false, sc );
        //    using var d2 = new ObservableDomain<Root>( TestHelper.Monitor, "Domain 2", startTimer: false, sc );

        //    d1.HasWaitingSidekicks.Should().BeTrue();
        //    await d1.ModifyThrowAsync( TestHelper.Monitor, null );

        //    d2.HasWaitingSidekicks.Should().BeTrue();
        //    await d2.ModifyThrowAsync( TestHelper.Monitor, null );

        //    CheckStatus( d1, DeviceControlStatus.HasOwnership );
        //    CheckStatus( d2, DeviceControlStatus.OutOfControlByConfiguration );

        //    await d1.ModifyThrowAsync( TestHelper.Monitor, () =>
        //    {
        //        d1.Root.Default.DeviceConfigurationEditor.Local.Strips[0].Mappings[0].DeviceName = "WillChange";
        //    } );

        //    await ApplyLocalConfigAsync( d1, null, device );
        //    CheckStatus( d1, DeviceControlStatus.HasOwnership );
        //    CheckStatus( d2, DeviceControlStatus.OutOfControlByConfiguration );
        //    device.ExternalConfiguration.Strips[0].Mappings.Where( m => m.DeviceName == "WillChange" ).Should().HaveCount( 1 );

        //    await ApplyLocalConfigAsync( d2, DeviceControlAction.TakeOwnership, device );
        //    CheckStatus( d1, DeviceControlStatus.OutOfControlByConfiguration );
        //    CheckStatus( d2, DeviceControlStatus.HasOwnership );

        //    await d1.ModifyThrowAsync( TestHelper.Monitor, () =>
        //    {
        //        d1.Root.Default.DeviceConfigurationEditor.Local.Strips[0].Mappings[0].DeviceName = "WontChange";
        //    } );

        //    await ApplyLocalConfigAsync( d1, null, device );
        //    CheckStatus( d1, DeviceControlStatus.OutOfControlByConfiguration );
        //    CheckStatus( d2, DeviceControlStatus.HasOwnership );
        //    device.ExternalConfiguration.Strips[0].Mappings.Where( m => m.DeviceName == "WontChange" ).Should().HaveCount( 0 );

        //    void CheckStatus( ObservableDomain<Root> d, DeviceControlStatus s )
        //    {
        //        d.Read( TestHelper.Monitor, () =>
        //        {
        //            d.Root.Default.DeviceControlStatus.Should().Be( s );
        //            d.Root.Host.Devices["Default"].Status.Should().Be( s );
        //        } );
        //    }

        //    async Task ApplyLocalConfigAsync( ObservableDomain<Root> d, DeviceControlAction? dca, LEDStripDevice device )
        //    {
        //        await d.ModifyThrowAsync( TestHelper.Monitor, () =>
        //        {
        //            d.Root.Default.DeviceConfigurationEditor.ApplyLocalConfig( dca );
        //        } );
        //        // Since the reconfiguration currently goes through the host that sends an InternalConfigureDeviceCommand
        //        // to the device, waiting here for the synchronization is not enough: we unfortunately have
        //        // to wait for the InternalConfigureDeviceCommand to reach the command queue.
        //        await Task.Delay( 100 );
        //        await device.WaitForSynchronizationAsync( false );

        //    }

        //}


        //[Test]
        //public async Task check_equality_should_not_be_the_same_when_add_mapping_or_strip_Async()
        //{
        //    var sc = new SimpleServiceContainer();
        //    var config = new SampleDeviceConfiguration()
        //    {
        //        Name = "Default",
        //        Status = DeviceConfigurationStatus.AlwaysRunning,
        //        PeriodMilliseconds = 100,
        //        Message = "Not used here."
        //    };
        //    var host = new SampleDeviceHost();

        //    sc.Add( host );

        //    (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );
        //    var device = host["Default"];
        //    Debug.Assert( device != null );
        //    await device.WaitForSynchronizationAsync( considerDeferredCommands: false );


        //    var d = new ObservableDomain<Root>( TestHelper.Monitor, "Test", startTimer: false, sc );

        //    await d.ModifyThrowAsync( TestHelper.Monitor, () =>
        //    {
        //        d.Root.Default.Configuration.Should().NotBeNull();
        //        d.Root.Default.DeviceConfigurationEditor.Should().NotBeNull();

        //        d.Root.Default.DeviceConfigurationEditor.IsSame().Should().BeTrue();

        //        d.Root.Default.DeviceConfigurationEditor.Local.Strips[0].Mappings.Add( new LEDLineMapping
        //        {
        //            DeviceName = "DLeft",
        //            SwitchId = 2,
        //            LineId = 2,
        //            Index = 4,
        //            Count = 4
        //        } );

        //        d.Root.Default.DeviceConfigurationEditor.IsSame().Should().BeFalse();

        //    } );

        //}

        //[Test]
        //public async Task check_equality_should_not_be_the_same_when_modify_existing_mapping_or_strip_Async()
        //{
        //    var sc = new SimpleServiceContainer();
        //    var config = new SampleDeviceConfiguration()
        //    {
        //        Name = "Default",
        //        Status = DeviceConfigurationStatus.AlwaysRunning,
        //        PeriodMilliseconds = 100,
        //        Message = "Not used here."
        //    };
        //    var host = new SampleDeviceHost();

        //    sc.Add( host );

        //    (await host.EnsureDeviceAsync( TestHelper.Monitor, config )).Should().Be( DeviceApplyConfigurationResult.CreateAndStartSucceeded );
        //    var device = host["Default"];
        //    Debug.Assert( device != null );
        //    await device.WaitForSynchronizationAsync( considerDeferredCommands: false );


        //    var d = new ObservableDomain<Root>( TestHelper.Monitor, "Test", startTimer: false, sc );

        //    await d.ModifyThrowAsync( TestHelper.Monitor, () =>
        //    {

        //        d.Root.Default.Configuration.Should().NotBeNull();
        //        d.Root.Default.DeviceConfigurationEditor.Should().NotBeNull();

        //        d.Root.Default.DeviceConfigurationEditor.IsSame().Should().BeTrue();

        //        d.Root.Default.DeviceConfigurationEditor.Local.Strips[0].Mappings[0].SwitchId = 5;

        //        d.Root.Default.DeviceConfigurationEditor.IsSame().Should().BeFalse();

        //    } );

        //}
    }
}
