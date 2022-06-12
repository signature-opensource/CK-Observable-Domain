using CK.BinarySerialization;
using CK.Core;
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
    public class DeviceHostTests
    {
        [SerializationVersion(0)]
        public class Root : ObservableRootObject
        {
            public readonly OSampleDeviceHost Host;

            public Root()
            {
                Host = new OSampleDeviceHost();
            }

            public Root( IBinaryDeserializer d, ITypeReadInfo info )
                : base( Sliced.Instance )
            {
                Host = d.ReadObject<OSampleDeviceHost>();
            }

            public static void Write( IBinarySerializer s, in Root o )
            {
                s.WriteObject( o.Host );
            }
        }

        [Test]
        public async Task ObservableDeviceHostObject_sync_the_list_of_ObservableDevice_and_Devices_Async()
        {
            using var _ = TestHelper.Monitor.OpenInfo( nameof( ObservableDeviceHostObject_sync_the_list_of_ObservableDevice_and_Devices_Async ) );

            var host = new SampleDeviceHost();
            await host.EnsureDeviceAsync( TestHelper.Monitor, new SampleDeviceConfiguration()
            {
                Name = "TheOne",
                Status = DeviceModel.DeviceConfigurationStatus.Runnable,
                PeriodMilliseconds = 100,
                Message = "Not used here."
            } );
            var sp = new SimpleServiceContainer();
            sp.Add( host );

            using var obs = new ObservableDomain<Root>( TestHelper.Monitor, nameof( ObservableDeviceHostObject_sync_the_list_of_ObservableDevice_and_Devices_Async ), false, serviceProvider: sp );

            // Here the Device exists but not the ObservableDevice.
            await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                obs.Root.Host.Devices.Should().ContainKey( "TheOne" );
                obs.Root.Host.Devices["TheOne"].Object.Should().BeNull( "No ObservableObject yet." );
                obs.Root.Host.Devices["TheOne"].Status.Should().Be( DeviceControlStatus.HasSharedControl );
            } );

            // Now we create the TheOne ObservableObject.
            await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                var d = new OSampleDevice( "TheOne" );
                d.IsBoundDevice.Should().BeTrue( "It has been automatically bound to the Device." );
                obs.Root.Host.Devices["TheOne"].Object.Should().BeSameAs( d, "The ObservableHostDevice has updated its DeviceInfo." );
            } );

            // Starts the device.
            await host.GetDevices()["TheOne"].StartAsync( TestHelper.Monitor );

            using( obs.AcquireReadLock() )
            {
                var oInfo = obs.Root.Host.Devices["TheOne"];
                Debug.Assert( oInfo.Object != null );
                oInfo.Object.IsBoundDevice.Should().BeTrue();
                oInfo.Object.IsRunning.Should().BeTrue();
                oInfo.IsRunning.Should().BeTrue();
                oInfo.Status.Should().Be( DeviceControlStatus.HasSharedControl );
            }

            // Destroying the Device.
            await host.ClearAsync( TestHelper.Monitor, waitForDeviceDestroyed: true );

            using( obs.AcquireReadLock() )
            {
                var oInfo = obs.Root.Host.Devices["TheOne"];
                Debug.Assert( oInfo.Object != null );
                oInfo.Object.IsBoundDevice.Should().BeFalse( "No more bound to the Device." );
                oInfo.IsRunning.Should().BeFalse();
                oInfo.Status.Should().Be( DeviceControlStatus.MissingDevice );
            }

            // Now we destroy the ObservableObject.
            await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                var d = obs.Root.Host.Devices["TheOne"].Object;
                Debug.Assert( d != null );
                d.Destroy();
                obs.Root.Host.Devices.Should().BeEmpty( "The ObservableHostDevice has no more info to keep (no device, no observable)." );
            } );

        }


        [Test]
        public async Task deserialization_triggers_events_when_resynchronizing_Async()
        {
            using var _ = TestHelper.Monitor.OpenInfo( nameof( deserialization_triggers_events_when_resynchronizing_Async ) );

            var host = new SampleDeviceHost();
            await host.EnsureDeviceAsync( TestHelper.Monitor, new SampleDeviceConfiguration()
            {
                Name = "TheOne",
                Status = DeviceModel.DeviceConfigurationStatus.Runnable,
                PeriodMilliseconds = Timeout.Infinite,
                Message = "Not used here."
            } );
            var sp = new SimpleServiceContainer();
            sp.Add( host );

            // Collects ObservableEvent emitted.
            IReadOnlyList<ObservableEvent>? lastEvents = null;
            var obs = new ObservableDomain<Root>( TestHelper.Monitor, nameof( deserialization_triggers_events_when_resynchronizing_Async ), false, serviceProvider: sp );
            obs.OnSuccessfulTransaction += ( d, ev ) => lastEvents = ev.Events;

            obs.HasWaitingSidekicks.Should().BeTrue();
            await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                obs.Root.Host.Devices.Should().ContainKey( "TheOne", "The host is synchronized." );
                obs.Root.Host.Devices["TheOne"].Object.Should().BeNull( "No ObservableObject yet." );
                obs.Root.Host.Devices["TheOne"].Status.Should().Be( DeviceControlStatus.HasSharedControl );
                obs.Root.Host.Devices["TheOne"].IsRunning.Should().BeFalse();
            } );
            Debug.Assert( lastEvents != null );
            lastEvents.Select( e => e.ToString() ).Should().BeEquivalentTo( new[] {
                "NewObject 3 (ODeviceInfo<OSampleDevice>).",
                "CollectionMapSet 2[TheOne] = Device: TheOne [Stopped, HasSharedControl]",
                "NewProperty DeviceName -> 0.",
                "PropertyChanged 3.DeviceName = TheOne.",
                "NewProperty Object -> 1.",
                "PropertyChanged 3.Object = null.",
                "NewProperty Status -> 2.",
                "PropertyChanged 3.Status = HasSharedControl.",
                "NewProperty IsRunning -> 3.",
                "PropertyChanged 3.IsRunning = False.",
                "NewProperty ControllerKey -> 4.",
                "PropertyChanged 3.ControllerKey = null.",
                "NewProperty ConfigurationControllerKey -> 5.",
                "PropertyChanged 3.ConfigurationControllerKey = null.",
                "NewProperty IsDestroyed -> 6.",
                "PropertyChanged 3.IsDestroyed = False.",
                "NewProperty OId -> 7.",
                "PropertyChanged 3.OId = 3." }, o => o.WithStrictOrdering() );
            lastEvents = null;

            // Saving the domain while the device is stopped.
            using var memory = new MemoryStream();
            obs.Save( TestHelper.Monitor, memory, debugMode: true );

            // Starting the Device. ObservableEvents are raised.
            await host.GetDevices()["TheOne"].StartAsync( TestHelper.Monitor );
            using( obs.AcquireReadLock() )
            {
                obs.Root.Host.Devices["TheOne"].IsRunning.Should().BeTrue( "The observable has been updated." );
            }
            Debug.Assert( lastEvents != null );
            lastEvents.Should().HaveCount( 1 );
            lastEvents[0].ToString().Should().Be( "PropertyChanged 3.IsRunning = True." );
            lastEvents = null;

            // Reloads the domain: for the serialized state, the device is not running.
            // This load occurs in an implicit (fake) transaction: the sidekicks are not instantiated.
            memory.Position = 0;
            using( var read = RewindableStream.FromStream( memory ) )
            {
                obs.Load( TestHelper.Monitor, read );
            }
            using( obs.AcquireReadLock() )
            {
                obs.Root.Host.Devices["TheOne"].IsRunning.Should().BeFalse( "No sidekick yet." );
            }
            lastEvents.Should().BeNull( "No real transaction: no event." );
            await obs.ModifyThrowAsync( TestHelper.Monitor, null );
            using( obs.AcquireReadLock() )
            {
                obs.Root.Host.Devices["TheOne"].IsRunning.Should().BeTrue( "The sidekick has been instantiated." );
            }
            Debug.Assert( lastEvents != null );
            lastEvents.Should().HaveCount( 1 );
            lastEvents[0].ToString().Should().Be( "PropertyChanged 3.IsRunning = True.", "Sidekick resynchronized the value." );
            lastEvents = null;

            // Same reload but now within a real transaction.
            lastEvents = null;
            await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
             {
                 memory.Position = 0;
                 using( var read = RewindableStream.FromStream( memory ) )
                 {
                     obs.Load( TestHelper.Monitor, read );
                 }
             } );
            using( obs.AcquireReadLock() )
            {
                obs.Root.Host.Devices["TheOne"].IsRunning.Should().BeTrue( "The observable has been updated by the sidekick." );
            }
            Debug.Assert( lastEvents != null, "The initialization of the sidekicks exposes its side effects." );
            lastEvents.Should().HaveCount( 1 );
            lastEvents[0].ToString().Should().Be( "PropertyChanged 3.IsRunning = True." );
            lastEvents = null;

            // Destroying the Device.
            await host.ClearAsync( TestHelper.Monitor, true );

            // Reloading from the memory (existing stopped device).
            await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
            {
                memory.Position = 0;
                using( var read = RewindableStream.FromStream( memory ) )
                {
                    obs.Load( TestHelper.Monitor, read );
                }
            } );
            // Same as before: the resynchronization is visible through the events.
            using( obs.AcquireReadLock() )
            {
                obs.Root.Host.Devices.Should().BeEmpty();
            }
            Debug.Assert( lastEvents != null, "The initialization of the sidekicks exposes its side effects." );
            lastEvents.Should().HaveCount( 1 );
            lastEvents[0].ToString().Should().Be( "CollectionRemoveKey 2[TheOne]" );

            await host.ClearAsync( TestHelper.Monitor, true );
            obs.Dispose();
        }

    }
}
