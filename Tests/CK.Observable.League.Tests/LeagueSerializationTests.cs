using CK.Core;
using CK.Text;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.League.Tests
{
    public class LeagueSearializationTests
    {
        [Test]
        public void coordinator_serialization()
        {
            var d = new ObservableDomain<Coordinator>( TestHelper.Monitor, String.Empty );

            var services = new SimpleServiceContainer();
            services.Add<ObservableDomain>( new ObservableDomain<Coordinator>( TestHelper.Monitor, String.Empty ) );
            BinarySerializer.IdempotenceCheck( d.Root, services );
        }
    }
}
