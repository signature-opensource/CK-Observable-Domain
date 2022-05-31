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
            using var d = new ObservableDomain<Root>(TestHelper.Monitor, "TEST", startTimer: true );
            d.Modify( TestHelper.Monitor, () =>
            {
                d.Root.Machine.Clock.IsActive = true;
                // CumulateUnloadedTime changes the CumulativeOffset at reload: serialization cannot be idempotent.
                d.Root.Machine.Clock.CumulateUnloadedTime = false;
                d.Root.Machine.IsRunning.Should().BeTrue();
                d.Root.Machine.CreateThing( 1 );
                d.Root.Machine.Things.Should().HaveCount( 1 );

            } ).Success.Should().BeTrue();

            // No Identification appears: => IdentificationTimeout.
            Thread.Sleep( 250 );
            ObservableDomain.IdempotenceSerializationCheck( TestHelper.Monitor, d, restoreSidekicks: true );
            d.Modify( TestHelper.Monitor, () =>
            {
                d.Root.Machine.Things.Should().HaveCount( 1 );
                d.Root.Machine.Things[0].IdentifiedId.Should().BeNull();
                d.Root.Machine.Things[0].Error.Should().Be( "IdentificationTimeout" );

            } ).Success.Should().BeTrue();

            ObservableDomain.IdempotenceSerializationCheck( TestHelper.Monitor, d, restoreSidekicks: true );
            // AutoDisposed fired.
            Thread.Sleep( 200 );
            d.Modify( TestHelper.Monitor, () =>
            {
                d.Root.Machine.Things.Should().BeEmpty();

            } ).Success.Should().BeTrue();
        }

        [TestCase( 10 )]
        [TestCase( 50 )]
        [TestCase( 100 )]
        [TestCase( 300 )]
        [TestCase( 500 )]
        public async Task initial_configuration_and_subsequent_work( int numberOfThing )
        {
            var store = BasicLeagueTests.CreateStore( nameof( initial_configuration_and_subsequent_work ) );
            var league = (await ObservableLeague.LoadAsync( TestHelper.Monitor, store ))!;
            await league.Coordinator.ModifyAsync( TestHelper.Monitor, ( m, coodinator ) =>
            {
                var d = coodinator.Root.CreateDomain( "M", typeof( Root ).AssemblyQualifiedName! );
                d.Options = d.Options.SetLifeCycleOption( DomainLifeCycleOption.Never )
                                     .SetCompressionKind( CompressionKind.GZiped );
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
                        // This will be processed by the sidekick even if we had no TestCallEnsureBridge:
                        // commands are handled after the new sidekick initialization (and after the release of
                        // the write lock).
                        d.Root.Machine.CmdToTheMachine( "no bug" );
                    } );
                }
            }

            using( TestHelper.Monitor.OpenInfo( "When commands bugs,  Machine Domain." ) )
            {
                await using( var shell = (await league.Find( "M" )!.LoadAsync<Root>( TestHelper.Monitor ))! )
                {
                    await shell.ModifyThrowAsync( TestHelper.Monitor, ( m, d ) =>
                    {
                        for( int i = 0; i < numberOfThing; ++i )
                        {
                            d.Root.Machine.CreateThing( 3712 + i );
                        }
                        d.Root.Machine.CmdToTheMachine( "no bug" );
                        d.Root.Machine.CommandReceivedCount.Should().Be( 2 );

                    } );
                    var result = await shell.ModifyAsync( TestHelper.Monitor, ( m, d ) =>
                    {
                        d.Root.Machine.CmdToTheMachine( "bug" );
                        d.Root.Machine.CommandReceivedCount.Should().Be( 3 );
                    } );
                    result.Success.Should().BeFalse();
                    result.CommandHandlingErrors.Should().NotBeEmpty();

                    result = await shell.ModifyAsync( TestHelper.Monitor, ( m, d ) =>
                    {
                        d.Root.Machine.CommandReceivedCount.Should().Be( 3, "This has been committed." );
                        d.Root.Machine.CmdToTheMachine( "bug in sending" );
                        Assert.Fail( "We never hit this: line above threw an exception." );
                    } );
                    result.Success.Should().BeFalse();
                    result.Errors.Should().NotBeEmpty();

                    result = await shell.ModifyAsync( TestHelper.Monitor, ( m, d ) =>
                    {
                        d.Root.Machine.CommandReceivedCount.Should().Be( 3, "The 4th call has been rollbacked." );
                    } );
                    result.Success.Should().BeTrue();
                }
            }
        }

        [Test]
        public void waiting_for_timeouts()
        {
            const int thingCount = 50;
            const int waitTimeBetweenThings = 50;

            using var d = new ObservableDomain<Root>(TestHelper.Monitor, "TEST", startTimer: true );
            d.Modify( TestHelper.Monitor, () =>
            {
                d.Root.Machine.Configuration.IdentifyThingTimeout = TimeSpan.FromMilliseconds( 200 );
                d.Root.Machine.Configuration.AutoDestroyedTimeout = TimeSpan.FromMilliseconds( 200 );
                d.Root.Machine.Clock.IsActive = true;
                d.Root.Machine.IsRunning.Should().BeTrue();
                for( int i = 0; i < thingCount; ++i )
                {
                    d.Root.Machine.CreateThing( i );
                    Thread.Sleep( waitTimeBetweenThings );
                }
            } ).Success.Should().BeTrue();

            // No Identification appears: => IdentificationTimeout.
            Thread.Sleep( 200 + (thingCount * waitTimeBetweenThings) );
            d.Modify( TestHelper.Monitor, () =>
            {
                int count = d.Root.Machine.Things.Count;
                for( int i = 0; i < count; ++i )
                {
                    d.Root.Machine.Things[i].IdentifiedId.Should().BeNull();
                    d.Root.Machine.Things[i].Error.Should().Be( "IdentificationTimeout" );
                }
            } ).Success.Should().BeTrue();

            // AutoDisposed fired.
            Thread.Sleep( (thingCount * waitTimeBetweenThings) );
            d.Modify( TestHelper.Monitor, () =>
            {
                d.Root.Machine.Things.Should().BeEmpty();

            } ).Success.Should().BeTrue();
        }

    }
}
