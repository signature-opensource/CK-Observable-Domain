using CK.Core;
using Shouldly;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.League.Tests;

public class DebugModeTests
{
    [Test]
    public async Task debug_mode_can_be_enabled_via_options_Async()
    {
        var store = BasicLeagueTests.CreateStore( nameof( debug_mode_can_be_enabled_via_options_Async ) );
        var league = await ObservableLeague.LoadAsync( TestHelper.Monitor, store );
        Debug.Assert( league != null );

        await league.Coordinator.ModifyThrowAsync( TestHelper.Monitor, ( m, d ) =>
        {
            var domain = d.Root.CreateDomain( "TestDomain", typeof( Model.School ).AssemblyQualifiedName! );
            domain.Options = domain.Options.SetDebugMode( true );
        }, waitForDomainPostActionsCompletion: true );

        league.Coordinator.Read( TestHelper.Monitor, ( m, d ) =>
        {
            var domain = d.Root.Domains["TestDomain"];
            return domain.Options.DebugMode;
        } ).ShouldBeTrue();

        await league.CloseAsync( TestHelper.Monitor );
    }

    [Test]
    public async Task domain_with_debug_mode_saves_and_loads_correctly_Async()
    {
        var store = BasicLeagueTests.CreateStore( nameof( domain_with_debug_mode_saves_and_loads_correctly_Async ) );
        var league = await ObservableLeague.LoadAsync( TestHelper.Monitor, store );
        Debug.Assert( league != null );

        // Create domain with debug mode enabled
        await league.Coordinator.ModifyThrowAsync( TestHelper.Monitor, ( m, d ) =>
        {
            var domain = d.Root.CreateDomain( "DebugDomain", typeof( Model.School ).AssemblyQualifiedName! );
            domain.Options = domain.Options.SetDebugMode( true );
        }, waitForDomainPostActionsCompletion: true );

        // Add data to the domain
        await using( var shell = await league.Find( "DebugDomain" )!.LoadAsync<Model.School>( TestHelper.Monitor ) )
        {
            Debug.Assert( shell != null );
            await shell.ModifyThrowAsync( TestHelper.Monitor, ( m, d ) =>
            {
                d.Root.Persons.Add( new Model.Person() { FirstName = "Debug", LastName = "Test", Age = 42 } );
            } );
        }

        await league.CloseAsync( TestHelper.Monitor );

        // Reload the league - this will verify sentinel bytes during deserialization
        var league2 = await ObservableLeague.LoadAsync( TestHelper.Monitor, store );
        Debug.Assert( league2 != null );

        // Verify data integrity
        await using( var shell2 = await league2.Find( "DebugDomain" )!.LoadAsync<Model.School>( TestHelper.Monitor ) )
        {
            Debug.Assert( shell2 != null );
            shell2.Read( TestHelper.Monitor, ( m, d ) =>
            {
                d.Root.Persons.Count.ShouldBe( 1 );
                d.Root.Persons[0].FirstName.ShouldBe( "Debug" );
                d.Root.Persons[0].LastName.ShouldBe( "Test" );
                return d.Root.Persons[0].Age;
            } ).ShouldBe( 42 );
        }

        await league2.CloseAsync( TestHelper.Monitor );
    }

    [Test]
    public async Task debug_mode_option_persists_through_league_serialization_Async()
    {
        var store = BasicLeagueTests.CreateStore( nameof( debug_mode_option_persists_through_league_serialization_Async ) );
        var league = await ObservableLeague.LoadAsync( TestHelper.Monitor, store );
        Debug.Assert( league != null );

        await league.Coordinator.ModifyThrowAsync( TestHelper.Monitor, ( m, d ) =>
        {
            var domain = d.Root.CreateDomain( "PersistTest", typeof( Model.School ).AssemblyQualifiedName! );
            domain.Options = domain.Options.SetDebugMode( true );
        }, waitForDomainPostActionsCompletion: true );

        await league.CloseAsync( TestHelper.Monitor );

        // Reload and verify debug mode persisted
        var league2 = await ObservableLeague.LoadAsync( TestHelper.Monitor, store );
        Debug.Assert( league2 != null );

        league2.Coordinator.Read( TestHelper.Monitor, ( m, d ) =>
        {
            var domain = d.Root.Domains["PersistTest"];
            return domain.Options.DebugMode;
        } ).ShouldBeTrue( "DebugMode should persist through serialization" );

        await league2.CloseAsync( TestHelper.Monitor );
    }

    [Test]
    public void ManagedDomainOptions_serialization_with_debug_mode()
    {
        var options = new ManagedDomainOptions(
            loadOption: DomainLifeCycleOption.Always,
            c: CompressionKind.None,
            skipTransactionCount: 0,
            snapshotSaveDelay: TimeSpan.Zero,
            snapshotKeepDuration: TimeSpan.FromDays( 2 ),
            snapshotMaximalTotalKiB: 10 * 1024,
            eventKeepDuration: TimeSpan.FromMinutes( 5 ),
            eventKeepLimit: 10,
            housekeepingRate: 50,
            debugMode: true
        );

        options.DebugMode.ShouldBeTrue();

        // Test idempotence
        BinarySerialization.BinarySerializer.IdempotenceCheck( options );
    }
}
