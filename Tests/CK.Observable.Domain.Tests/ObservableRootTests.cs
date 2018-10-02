using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CK.Observable.Domain.Tests.RootSample;
using static CK.Testing.MonitorTestHelper;
using FluentAssertions;
using System.IO;
using CK.Core;

namespace CK.Observable.Domain.Tests
{
    [TestFixture]
    public class ObservableRootTests
    {
        [Test]
        public void initializing_and_persisting_new_empty_domain()
        {
            var d = new ObservableDomain<ApplicationState>();
            d.Root.Should().NotBeNull();
            d.TransactionSerialNumber.Should().Be( 0 );

            var d2 = SaveAndLoad( d );
            d.Root.Should().NotBeNull();
            d.TransactionSerialNumber.Should().Be( 0 );
        }

        [Test]
        public void initializing_and_persisting_domain()
        {
            var eventCollector = new TransactionEventCollector();

            var d = new ObservableDomain<ApplicationState>( eventCollector );
            d.Root.ToDoNumbers.Should().BeEmpty();

            d.Modify( () =>
            {
                d.Root.ToDoNumbers.Add( 42 );
            } );
            string t1 = eventCollector.WriteEventsFrom( 0 );
            t1.Should().Be( @"{""N"":1,""E"":[[""I"",1,0,42]]}", "Initial root objects instanciations are not exposed." );

            var d2 = SaveAndLoad( d );
            d.TransactionSerialNumber.Should().Be( 1 );
            d.Root.ToDoNumbers[0].Should().Be( 42 );
        }

        public void serialization_tests()
        {
            var d = new ObservableDomain<ApplicationState>();
            d.Modify( () =>
            {
                d.Root.ToDoNumbers.AddRange( Enumerable.Range(10, 20) );
                for( int i = 0; i < 30; ++i )
                {
                    var pInfo = new ProductInfo( $"Product Info n°{i}", i );
                    var p = new Product( pInfo );
                    d.Root.Products.Add( $"p{i}", pInfo );
                }
            } );
            var services = new SimpleServiceContainer();
            services.Add( new ObservableDomain<ApplicationState>() );
            BinarySerializer.IdempotenceCheck( d.Root, services );
        }

        [Test]
        public void serialization_test_of_NotificationState()
        {
            var od = new ObservableDomain<Signature.Process.Dispatching.NotificationState>();
            od.Modify( () =>
            {
                od.Root.BarcodeScanner.UpdateOnScan( "aaa", null );
            } );

            var od2 = SaveAndLoad( od );

            var services = new SimpleServiceContainer();
            var domain = new ObservableDomain<Signature.Process.Dispatching.NotificationState>();
            services.Add( domain );
            services.Add<ObservableDomain>( domain );
            BinarySerializer.IdempotenceCheck( od.Root.BarcodeScanner, services );
        }

        [Test]
        public void SerializationTest_2()
        {
            Signature.Process.Dispatching.BarcodeScannerState bs = null;

            var od = new ObservableDomain();
            od.Modify( () =>
            {
                bs = new Signature.Process.Dispatching.BarcodeScannerState();
                bs.UpdateOnScan( "aaa", null );
            } );

            var services = new SimpleServiceContainer();
            services.Add( new ObservableDomain() );

            BinarySerializer.IdempotenceCheck( bs, services );
        }


        internal static ObservableDomain<T> SaveAndLoad<T>( ObservableDomain<T> domain ) where T : ObservableRootObject
        {
            using( var s = new MemoryStream() )
            {
                domain.Save( s, leaveOpen: true );
                s.Position = 0;
                return new ObservableDomain<T>( null, null, s );
            }
        }
    }
}
