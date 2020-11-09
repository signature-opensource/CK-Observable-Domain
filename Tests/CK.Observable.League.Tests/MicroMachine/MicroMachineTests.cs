using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.League.Tests.MicroMachine
{
    [TestFixture]
    public class MicroMachineTests
    {
        [Test]
        public void empty_serialization()
        {
            var d = new ObservableDomain<Root>( TestHelper.Monitor, "TEST" );
            ObservableDomain.IdempotenceSerializationCheck( TestHelper.Monitor, d );
        }

        [Test]
        public void AutoDisposed_works_accross_serialization()
        {
            using var d = new ObservableDomain<Root>( TestHelper.Monitor, "TEST" );
            d.Modify( TestHelper.Monitor, () =>
            {
                d.Root.Machine.Clock.IsActive = true;
                // CumulateUnloadedTime changes the CumulativeOffset at reload: serialization cannot be idempotent.
                d.Root.Machine.Clock.CumulateUnloadedTime = false;
                d.Root.Machine.IsRunning.Should().BeTrue();
                d.Root.Machine.CreateThing( 1 );
            } );
            // No Identification appears: => IdentificationTimeout.
            Thread.Sleep( 250 );
            ObservableDomain.IdempotenceSerializationCheck( TestHelper.Monitor, d );
            d.Modify( TestHelper.Monitor, () =>
            {
                d.Root.Machine.Things[0].IdentifiedId.Should().BeNull();
                d.Root.Machine.Things[0].Error.Should().Be( "IdentificationTimeout" );
            } );
            ObservableDomain.IdempotenceSerializationCheck( TestHelper.Monitor, d );
            // AutoDisposed fired.
            Thread.Sleep( 200 );
            d.Modify( TestHelper.Monitor, () =>
            {
                d.Root.Machine.Things.Should().BeEmpty();
            } );
        }

    }
}
