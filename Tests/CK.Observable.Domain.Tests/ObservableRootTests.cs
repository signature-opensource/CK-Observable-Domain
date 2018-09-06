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

        static ObservableDomain<T> SaveAndLoad<T>( ObservableDomain<T> domain ) where T : ObservableRootObject
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
