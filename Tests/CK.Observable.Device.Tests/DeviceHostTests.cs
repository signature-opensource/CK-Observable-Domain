using CK.BinarySerialization;
using CK.Core;
using Shouldly;
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
using Microsoft.VisualStudio.TestPlatform.CoreUtilities.Extensions;

namespace CK.Observable.Device.Tests;

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
            obs.Root.Host.Devices.Keys.ShouldContain( "TheOne" );
            obs.Root.Host.Devices["TheOne"].Object.ShouldBeNull( "No ObservableObject yet." );
            obs.Root.Host.Devices["TheOne"].Status.ShouldBeNull( "Since Object is null, DeviceControlStatus property also returns null." );
        } );

        // Now we create the TheOne ObservableObject.
        await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            var d = new OSampleDevice( "TheOne" );
            d.IsBoundDevice.ShouldBeTrue( "It has been automatically bound to the Device." );
            obs.Root.Host.Devices["TheOne"].Object.ShouldBeSameAs( d, "The ObservableHostDevice has updated its DeviceInfo." );
        } );

        // Starts the device.
        await host.GetDevices()["TheOne"].StartAsync( TestHelper.Monitor );

        obs.Read( TestHelper.Monitor, () =>
        {
            var oInfo = obs.Root.Host.Devices["TheOne"];
            Debug.Assert( oInfo.Object != null );
            oInfo.Object.IsBoundDevice.ShouldBeTrue();
            oInfo.Object.IsRunning.ShouldNotBeNull().ShouldBeTrue();
            oInfo.IsRunning.ShouldBeTrue();
            oInfo.Status.ShouldBe( DeviceControlStatus.HasSharedControl );
        } );

        // Destroying the Device.
        await host.ClearAsync( TestHelper.Monitor, waitForDeviceDestroyed: true );

        obs.Read( TestHelper.Monitor, () =>
        {
            var oInfo = obs.Root.Host.Devices["TheOne"];
            Debug.Assert( oInfo.Object != null );
            oInfo.Object.IsBoundDevice.ShouldBeFalse( "No more bound to the Device." );
            oInfo.IsRunning.ShouldBeFalse();
            oInfo.Status.ShouldBe( DeviceControlStatus.MissingDevice );
        } );

        // Now we destroy the ObservableObject.
        await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            var oDeviceInfo = obs.Root.Host.Devices["TheOne"];
            var d = obs.Root.Host.Devices["TheOne"].Object;
            Debug.Assert( d != null );
            d.Destroy();
            obs.Root.Host.Devices.ShouldBeEmpty( "The ObservableHostDevice has no more info to keep (no device, no observable)." );
            oDeviceInfo.IsDestroyed.ShouldBeTrue( "The ODeviceInfo should have been destroyed with the ObservableDeviceObject." );
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
        obs.TransactionDone += ( d, ev ) => lastEvents = ev.Events;

        obs.HasWaitingSidekicks.ShouldBeTrue();
        await obs.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            obs.Root.Host.Devices.Keys.ShouldContain( "TheOne", "The host is synchronized." );
            obs.Root.Host.Devices["TheOne"].Object.ShouldBeNull( "No ObservableObject yet." );
            obs.Root.Host.Devices["TheOne"].Status.ShouldBeNull( "Since Object is null, DeviceControlStatus property also returns null." );
            obs.Root.Host.Devices["TheOne"].IsRunning.ShouldBeFalse();
        } );
        Debug.Assert( lastEvents != null );
        lastEvents.Select( e => e.ToString() ).ShouldBe( [
            "NewObject 3 (ODeviceInfo<OSampleDevice>).",
            "CollectionMapSet 2[TheOne] = Device: TheOne [Stopped, ]",
            "NewProperty DeviceName -> 0.",
            "PropertyChanged 3.DeviceName = TheOne.",
            "NewProperty Object -> 1.",
            "PropertyChanged 3.Object = null.",
            "NewProperty Status -> 2.",
            "PropertyChanged 3.Status = null.",
            "NewProperty IsRunning -> 3.",
            "PropertyChanged 3.IsRunning = False.",
            "NewProperty ControllerKey -> 4.",
            "PropertyChanged 3.ControllerKey = null.",
            "NewProperty ConfigurationControllerKey -> 5.",
            "PropertyChanged 3.ConfigurationControllerKey = null.",
            "NewProperty IsDestroyed -> 6.",
            "PropertyChanged 3.IsDestroyed = False.",
            "NewProperty OId -> 7.",
            "PropertyChanged 3.OId = 3." ] );
        lastEvents = null;

        // Saving the domain while the device is stopped.
        using var memory = new MemoryStream();
        obs.Save( TestHelper.Monitor, memory, debugMode: true );

        // Starting the Device. ObservableEvents are raised.
        await host.GetDevices()["TheOne"].StartAsync( TestHelper.Monitor );
        obs.Read( TestHelper.Monitor, () =>
        {
            obs.Root.Host.Devices["TheOne"].IsRunning.ShouldBeTrue( "The observable has been updated." );
        } );
        Debug.Assert( lastEvents != null );
        lastEvents.Count.ShouldBe( 1 );
        lastEvents[0].ToString().ShouldBe( "PropertyChanged 3.IsRunning = True." );
        lastEvents = null;

        // Reloads the domain: for the serialized state, the device is not running.
        // This load occurs in an implicit (fake) transaction: the sidekicks are not instantiated.
        memory.Position = 0;
        using( var read = RewindableStream.FromStream( memory ) )
        {
            obs.Load( TestHelper.Monitor, read );
        }
        obs.Read( TestHelper.Monitor, () =>
        {
            obs.Root.Host.Devices["TheOne"].IsRunning.ShouldBeFalse( "No sidekick yet." );
        } );
        lastEvents.ShouldBeNull( "No real transaction: no event." );
        await obs.ModifyThrowAsync( TestHelper.Monitor, null );
        obs.Read( TestHelper.Monitor, () =>
        {
            obs.Root.Host.Devices["TheOne"].IsRunning.ShouldBeTrue( "The sidekick has been instantiated." );
        } );
        Debug.Assert( lastEvents != null );
        lastEvents.Count.ShouldBe( 1 );
        lastEvents[0].ToString().ShouldBe( "PropertyChanged 3.IsRunning = True.", "Sidekick resynchronized the value." );
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
        obs.Read( TestHelper.Monitor, () =>
        {
            obs.Root.Host.Devices["TheOne"].IsRunning.ShouldBeTrue( "The observable has been updated by the sidekick." );
        } );
        Debug.Assert( lastEvents != null, "The initialization of the sidekicks exposes its side effects." );
        lastEvents.Count.ShouldBe( 1 );
        lastEvents[0].ToString().ShouldBe( "PropertyChanged 3.IsRunning = True." );
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
        obs.Read( TestHelper.Monitor, () =>
        {
            obs.Root.Host.Devices.ShouldBeEmpty();
        } );
        Debug.Assert( lastEvents != null, "The initialization of the sidekicks exposes its side effects." );
        lastEvents.Count.ShouldBe( 1 );
        lastEvents[0].ToString().ShouldBe( "CollectionRemoveKey 2[TheOne]" );

        await host.ClearAsync( TestHelper.Monitor, true );
        obs.Dispose();
    }

}
