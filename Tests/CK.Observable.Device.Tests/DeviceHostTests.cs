using CK.Core;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Device.Tests
{
    [TestFixture]
    public class ObservableDeviceHostTests
    {
        public class Root : ObservableRootObject
        {
            public readonly OSampleDeviceHost Host;

            readonly ObservableDictionary<string, OSampleDevice> _devices;

            public IObservableReadOnlyDictionary<string, OSampleDevice> Devices => _devices;

            public Root()
            {
                Host = new OSampleDeviceHost();
                _devices = new ObservableDictionary<string,OSampleDevice>();
                Host.Devices.ItemAdded += OnDeviceAdded;
                Host.Devices.ItemRemoved += OnDeviceRemoved;
                Host.Devices.CollectionCleared += OnDeviceCleared;
            }

            void OnDeviceAdded( object sender, CollectionAddKeyEvent e )
            {
                var deviceName = (string)e.Key; 
                _devices.Add( deviceName, new OSampleDevice( deviceName ) );
            }

            void OnDeviceRemoved( object sender, CollectionRemoveKeyEvent e )
            {
                throw new NotImplementedException();
            }

            void OnDeviceCleared( object sender, CollectionClearEvent e )
            {
                throw new NotImplementedException();
            }

        }

        [Test]
        public async Task host_can_synchronize_a_list_of_ObservableDevice_Async()
        {
            using var _ = TestHelper.Monitor.OpenInfo( nameof( host_can_synchronize_a_list_of_ObservableDevice_Async ) );

            var host = new SampleDeviceHost();
            await host.EnsureDeviceAsync( TestHelper.Monitor, new SampleDeviceConfiguration() { Name = "TheOne" } );
            var sp = new SimpleServiceContainer();
            sp.Add( host );

            using var obs = new ObservableDomain<Root>( TestHelper.Monitor, nameof( host_can_synchronize_a_list_of_ObservableDevice_Async ), false, serviceProvider: sp );

            await obs.ModifyThrowAsync( TestHelper.Monitor, () => { } );

            obs.AllObjects.OfType<OSampleDevice>().Single().DeviceName.Should().Be( "TheOne" );
        }
    }
}
