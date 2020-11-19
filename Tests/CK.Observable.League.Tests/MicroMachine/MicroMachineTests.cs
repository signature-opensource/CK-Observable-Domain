using CK.Core;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.League.Tests.MicroMachine
{
    [TestFixture]
    public class MicroMachineTests
    {
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

        [Test]
        public async Task initial_configuration_and_subsequent_work()
        {
            var store = BasicLeagueTests.CreateStore( nameof( initial_configuration_and_subsequent_work ) );
            var league = (await ObservableLeague.LoadAsync( TestHelper.Monitor, store ))!;
            await league.Coordinator.ModifyAsync( TestHelper.Monitor, ( m, coodinator ) =>
            {
                var d = coodinator.Root.CreateDomain( "M", typeof( Root ).AssemblyQualifiedName! );
                d.Options = d.Options.SetLifeCycleOption( DomainLifeCycleOption.Never );
            } );

            using( TestHelper.Monitor.OpenInfo( "Initializing Machine Domain." ) )
            {
                await using( var shell = (await league.Find( "M" )!.LoadAsync<Root>( TestHelper.Monitor ))! )
                {
                    await shell.ModifyThrowAsync( TestHelper.Monitor, ( m, d ) =>
                    {
                        d.Root.Machine.Clock.IsActive = true;
                        d.Root.Machine.IsRunning.Should().BeTrue();
                        d.Root.Machine.CreateThing( 1 );
                        d.Root.Machine.CreateThing( 3712 );
                        d.Root.Machine.CmdToTheMachine();

                        d.Root.Machine.BridgeToTheSidekick.Should().BeNull( "The sidekick is not yet instantiated." );
                        d.Root.Machine.TestCallEnsureBridge();
                        d.Root.Machine.BridgeToTheSidekick.Should().NotBeNull( "Now it is." );
                    } );
                }
            }

            using( TestHelper.Monitor.OpenInfo( "Restoring Machine Domain." ) )
            {
                await using( var shell = (await league.Find( "M" )!.LoadAsync<Root>( TestHelper.Monitor ))! )
                {
                    await shell.ModifyThrowAsync( TestHelper.Monitor, ( m, d ) =>
                    {
                        d.Root.Machine.CmdToTheMachine();
                    } );
                }
            }
        }
    }
}
