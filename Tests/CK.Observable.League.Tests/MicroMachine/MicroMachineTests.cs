using CK.Core;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.League.Tests.MicroMachine;

[TestFixture]
public class MicroMachineTests
{
    [Test]
    [Explicit( "Unfortunately this test fails on CI. Timed tests are a mess..." )]
    public async Task AutoDisposed_works_accross_serialization_Async()
    {
        using var d = new ObservableDomain<Root>(TestHelper.Monitor, "TEST", startTimer: true );
        await d.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            d.Root.Machine.Clock.IsActive = true;
            // CumulateUnloadedTime changes the CumulativeOffset at reload: serialization cannot be idempotent
            // so we disable it.
            d.Root.Machine.Clock.CumulateUnloadedTime = false;
            d.Root.Machine.IsRunning.Should().BeTrue();
            d.Root.Machine.CreateThing( 1 );
            d.Root.Machine.Things.Should().HaveCount( 1 );

        } );

        // No Identification appears: => IdentificationTimeout.
        Thread.Sleep( 250 );
        // We must set restoreSidekicks to true here because the MicroMachine sidekick registers
        // itself to the Device's OnDestroyed event and a null when handler is saved (event registered
        // to sidekick is skipped). If the second write has no sidekick, the null will be missing...
        ObservableDomain.IdempotenceSerializationCheck( TestHelper.Monitor, d, restoreSidekicks: true );
        await d.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            d.Root.Machine.Things.Should().HaveCount( 1 );
            d.Root.Machine.Things[0].IdentifiedId.Should().BeNull();
            d.Root.Machine.Things[0].Error.Should().Be( "IdentificationTimeout" );

        } );

        ObservableDomain.IdempotenceSerializationCheck( TestHelper.Monitor, d, restoreSidekicks: true );
        // AutoDisposed fired.
        Thread.Sleep( 200 );
        d.Read( TestHelper.Monitor, () =>
        {
            d.Root.Machine.Things.Should().BeEmpty();
        } );
    }

    [TestCase( 10 )]
    [TestCase( 50 )]
    [TestCase( 100 )]
    [TestCase( 300 )]
    [TestCase( 500 )]
    public async Task initial_configuration_and_subsequent_work_Async( int numberOfThing )
    {
        var store = BasicLeagueTests.CreateStore( nameof( initial_configuration_and_subsequent_work_Async ) );
        var league = (await ObservableLeague.LoadAsync( TestHelper.Monitor, store ))!;
        await league.Coordinator.ModifyThrowAsync( TestHelper.Monitor, ( m, coodinator ) =>
        {
            var d = coodinator.Root.CreateDomain( "M", typeof( Root ).AssemblyQualifiedName! );
            d.Options = d.Options.SetLifeCycleOption( DomainLifeCycleOption.Never )
                                 .SetCompressionKind( CompressionKind.GZiped );
        }, waitForDomainPostActionsCompletion: true );

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
                var result = await shell.TryModifyAsync( TestHelper.Monitor, ( m, d ) =>
                {
                    d.Root.Machine.CmdToTheMachine( "bug" );
                    d.Root.Machine.CommandReceivedCount.Should().Be( 3 );
                } );
                result.Success.Should().BeFalse();
                result.CommandHandlingErrors.Should().NotBeEmpty();

                result = await shell.TryModifyAsync( TestHelper.Monitor, ( m, d ) =>
                {
                    d.Root.Machine.CommandReceivedCount.Should().Be( 3, "This has been committed." );
                    d.Root.Machine.CmdToTheMachine( "bug in sending" );
                    Assert.Fail( "We never hit this: line above threw an exception." );
                } );
                result.Success.Should().BeFalse();
                result.Errors.Should().NotBeEmpty();

                result = await shell.TryModifyAsync( TestHelper.Monitor, ( m, d ) =>
                {
                    d.Root.Machine.CommandReceivedCount.Should().Be( 3, "The 4th call has been rollbacked." );
                } );
                result.Success.Should().BeTrue();
            }
        }
    }

    [Test]
    public async Task waiting_for_timeouts_Async()
    {
        const int thingCount = 50;
        const int waitTimeBetweenThings = 50;

        using var d = new ObservableDomain<Root>(TestHelper.Monitor, "TEST", startTimer: true );
        await d.ModifyThrowAsync( TestHelper.Monitor, () =>
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
        } );

        // No Identification appears: => IdentificationTimeout.
        Thread.Sleep( 200 + (thingCount * waitTimeBetweenThings) );
        await d.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            int count = d.Root.Machine.Things.Count;
            for( int i = 0; i < count; ++i )
            {
                d.Root.Machine.Things[i].IdentifiedId.Should().BeNull();
                d.Root.Machine.Things[i].Error.Should().Be( "IdentificationTimeout" );
            }
        } );

        // AutoDisposed fired.
        Thread.Sleep( (thingCount * waitTimeBetweenThings) );
        d.Read( TestHelper.Monitor, () =>
        {
            d.Root.Machine.Things.Should().BeEmpty();
        } );
    }

}
