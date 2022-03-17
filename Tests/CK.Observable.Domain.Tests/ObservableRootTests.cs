using NUnit.Framework;
using System.Linq;
using CK.Observable.Domain.Tests.RootSample;
using FluentAssertions;
using System.IO;
using CK.Core;
using static CK.Testing.MonitorTestHelper;
using CK.BinarySerialization;

namespace CK.Observable.Domain.Tests
{
    [TestFixture]
    public class ObservableRootTests
    {
        static JsonEventCollector.TransactionEvent LastEvent = null;
        static void TrackLastEvent( IActivityMonitor m, JsonEventCollector.TransactionEvent e ) => LastEvent = e;

        [Test]
        public void initializing_and_persisting_new_empty_domain()
        {
            using var d = new ObservableDomain<ApplicationState>(TestHelper.Monitor, nameof( initializing_and_persisting_new_empty_domain), startTimer: true );
            d.Root.Should().NotBeNull();
            d.TransactionSerialNumber.Should().Be( 0 );

            using var d2 = SaveAndLoad( d );
            d.Root.Should().NotBeNull();
            d.TransactionSerialNumber.Should().Be( 0 );
        }

        [Test]
        public void initializing_and_persisting_domain()
        {
            var eventCollector = new JsonEventCollector();
            eventCollector.LastEventChanged += TrackLastEvent;

            using( var d = new ObservableDomain<ApplicationState>(TestHelper.Monitor, "TEST", startTimer: true ) )
            {
                eventCollector.CollectEvent( d, false );
                d.Root.ToDoNumbers.Should().BeEmpty();

                d.Modify( TestHelper.Monitor, () =>
                {
                    d.Root.ToDoNumbers.Add( 42 );
                } );
                string t1 = LastEvent.ExportedEvents;
                t1.Should().Be( "" );

                using( var d2 = SaveAndLoad( d ) )
                {
                    d2.TransactionSerialNumber.Should().Be( 1 );
                    d2.Root.ToDoNumbers[0].Should().Be( 42 );

                    ObservableDomain.IdempotenceSerializationCheck( TestHelper.Monitor, d2 );
                }
            }
        }

        [Test]
        public void serialization_tests()
        {
            using( var d = new ObservableDomain<ApplicationState>(TestHelper.Monitor, nameof( serialization_tests ), startTimer: true ) )
            {
                d.Modify( TestHelper.Monitor, () =>
                {
                    d.Root.ToDoNumbers.AddRange( Enumerable.Range( 10, 20 ) );
                    for( int i = 0; i < 30; ++i )
                    {
                        var pInfo = new ProductInfo( $"Product Info n°{i}", i );
                        var p = new Product( pInfo );
                        d.Root.Products.Add( $"p{i}", pInfo );
                        d.Root.ProductInfos.Add( pInfo );
                    }
                } );
                var ctx = new BinarySerialization.BinaryDeserializerContext();
                ctx.Services.Add<ObservableDomain>( new ObservableDomain<ApplicationState>(TestHelper.Monitor, nameof( serialization_tests ), startTimer: true ) );
                BinarySerialization.BinarySerializer.IdempotenceCheck( d.Root, deserializerContext: ctx );
            }
        }

        internal static ObservableDomain<T> SaveAndLoad<T>( ObservableDomain<T> domain ) where T : ObservableRootObject
        {
            using( var s = new MemoryStream() )
            {
                domain.Save( TestHelper.Monitor, s );
                s.Position = 0;
                return new ObservableDomain<T>( TestHelper.Monitor, domain.DomainName, null, RewindableStream.FromStream( s ) );
            }
        }
    }
}
