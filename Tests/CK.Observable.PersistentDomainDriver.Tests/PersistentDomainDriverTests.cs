using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CK.Core;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using Shouldly;

namespace CK.Observable.PersistentDomainDriverTests;

public sealed class TestDriver : PersistentDomainDriver<TestDriver>
{
    public TestDriver()
        : base( "TestDomain", autoStartTimer: false )
    {
    }

    /// <summary>Gets the <see cref="PersistentDomainDriver{TSelf}.LoadOutcome"/> that the last PreConfigure saw.</summary>
    public PersistentDomainLoadOutcome? OutcomeInPreConfigure { get; private set; }

    protected override void PreConfigure( IActivityMonitor monitor, ObservableDomain domain )
    {
        OutcomeInPreConfigure = LoadOutcome;
        domain.CreateSingleton<OTestSingleton>();
    }
}

/// <summary>Counts its two configuration hooks so a restart can tell them apart.</summary>
public sealed class SeedingDriver : PersistentDomainDriver<SeedingDriver>
{
    public SeedingDriver()
        : base( "SeedDomain", autoStartTimer: false )
    {
    }

    public int PreConfigureCount { get; private set; }

    public int InitializeNewDomainCount { get; private set; }

    protected override void PreConfigure( IActivityMonitor monitor, ObservableDomain domain )
    {
        ++PreConfigureCount;
        domain.CreateSingleton<OTestSingleton>();
    }

    protected override void InitializeNewDomain( IActivityMonitor monitor, ObservableDomain domain )
    {
        ++InitializeNewDomainCount;
        domain.AllObjects.Items.OfType<OTestSingleton>().Single().Marker = "seeded";
    }
}

/// <summary>Records each outcome. Returns <see cref="Result"/>, or throws when <see cref="Throws"/> is true.</summary>
public sealed class RecordingHandler<T> : IPersistentDomainLoadHandler<T> where T : IObservableDomainDriver
{
    public RecordingHandler( bool result = true, bool throws = false )
    {
        Result = result;
        Throws = throws;
    }

    public bool Result { get; }

    public bool Throws { get; }

    public List<PersistentDomainLoadOutcome> Outcomes { get; } = new List<PersistentDomainLoadOutcome>();

    public Task<bool> OnDomainLoadedAsync( IActivityMonitor monitor, T driver, PersistentDomainLoadOutcome outcome )
    {
        Outcomes.Add( outcome );
        if( Throws ) throw new InvalidOperationException( "The handler fails." );
        return Task.FromResult( Result );
    }
}

/// <summary>A configurator that refuses the configuration: the initial transaction fails.</summary>
public sealed class RefusingConfigurator : IObservableDomainConfigurator<TestDriver>
{
    public bool ConfigureDomain( IActivityMonitor monitor, ObservableDomain observableDomain ) => false;
}

/// <summary>
/// A configurator that adds a failing domain post action to the initial transaction.
/// The start does not wait for the domain post actions: the error does not fail it.
/// </summary>
public sealed class FailingDomainPostActionConfigurator : IObservableDomainConfigurator<TestDriver>
{
    public bool ConfigureDomain( IActivityMonitor monitor, ObservableDomain observableDomain )
    {
        EventHandler<TransactionDoneEventArgs>? handler = null;
        handler = ( sender, e ) =>
        {
            observableDomain.TransactionDone -= handler;
            e.DomainPostActions.Add( ctx => Task.FromException( new InvalidOperationException( "The domain post action fails." ) ) );
        };
        observableDomain.TransactionDone += handler;
        return true;
    }
}

sealed class SingleServiceProvider : IServiceProvider
{
    readonly IConfiguration _configuration;

    public SingleServiceProvider( IConfiguration configuration )
    {
        _configuration = configuration;
    }

    public object? GetService( Type serviceType )
    {
        return serviceType == typeof( IConfiguration ) ? _configuration : null;
    }
}

[TestFixture]
public class PersistentDomainDriverTests
{
    static readonly IEnumerable<IObservableDomainConfigurator<TestDriver>> _noConfigurators = Enumerable.Empty<IObservableDomainConfigurator<TestDriver>>();
    static readonly IEnumerable<IPersistentDomainLoadHandler<TestDriver>> _noHandlers = Enumerable.Empty<IPersistentDomainLoadHandler<TestDriver>>();

    [Test]
    public async Task restart_reloads_state_and_finds_existing_singleton()
    {
        var monitor = new ActivityMonitor();
        var store = TestStore.NewDirectory();
        var services = NewServices( store, "TestDomain" );

        var driver1 = new TestDriver();
        await driver1.DoHostStartAsync( monitor, services, _noConfigurators, _noHandlers );
        OdFiles( store ).Length.ShouldBe( 1 );
        await SetMarkerAsync( monitor, driver1, "hello" );
        await StopAsync( monitor, driver1 );

        var driver2 = new TestDriver();
        await driver2.DoHostStartAsync( monitor, services, _noConfigurators, _noHandlers );
        driver2.LoadOutcome.ShouldNotBeNull().Kind.ShouldBe( PersistentDomainLoadKind.Loaded );
        int singletonCount = 0;
        string? marker = null;
        await driver2.Domain.ModifyThrowAsync( monitor, () =>
        {
            var all = driver2.Domain.AllObjects.Items.OfType<OTestSingleton>().ToList();
            singletonCount = all.Count;
            marker = all.Single().Marker;
        } );
        // PreConfigure ran again on the reloaded domain: CreateSingleton found the existing instance.
        singletonCount.ShouldBe( 1 );
        marker.ShouldBe( "hello" );
        await StopAsync( monitor, driver2 );
    }

    [Test]
    public async Task InitializeNewDomain_runs_on_creation_only_and_a_reload_keeps_the_seeded_state()
    {
        var monitor = new ActivityMonitor();
        var services = NewServices( TestStore.NewDirectory(), "SeedDomain" );
        var noConfigurators = Enumerable.Empty<IObservableDomainConfigurator<SeedingDriver>>();
        var noHandlers = Enumerable.Empty<IPersistentDomainLoadHandler<SeedingDriver>>();

        var driver1 = new SeedingDriver();
        await driver1.DoHostStartAsync( monitor, services, noConfigurators, noHandlers );
        driver1.IsNewDomain.ShouldBeTrue();
        driver1.PreConfigureCount.ShouldBe( 1 );
        driver1.InitializeNewDomainCount.ShouldBe( 1 );
        await driver1.Domain.ModifyThrowAsync( monitor, () =>
        {
            driver1.Domain.AllObjects.Items.OfType<OTestSingleton>().Single().Marker = "changed by the app";
        }, waitForDomainPostActionsCompletion: true );
        await driver1.DoHostStopAsync( monitor );
        driver1.Domain.Dispose();

        var driver2 = new SeedingDriver();
        await driver2.DoHostStartAsync( monitor, services, noConfigurators, noHandlers );
        string? marker = null;
        await driver2.Domain.ModifyThrowAsync( monitor, () =>
        {
            marker = driver2.Domain.AllObjects.Items.OfType<OTestSingleton>().Single().Marker;
        } );
        driver2.IsNewDomain.ShouldBeFalse();
        // PreConfigure ran again, the seed did not: what the app wrote survives.
        driver2.PreConfigureCount.ShouldBe( 1 );
        driver2.InitializeNewDomainCount.ShouldBe( 0 );
        marker.ShouldBe( "changed by the app" );
        await driver2.DoHostStopAsync( monitor );
        driver2.Domain.Dispose();
    }

    [Test]
    public async Task a_damaged_newest_snapshot_aborts_the_host_start()
    {
        var monitor = new ActivityMonitor();
        var store = TestStore.NewDirectory();
        var services = NewServices( store, "TestDomain" );
        await StartAndStopAsync( monitor, services, store );
        ZeroFill( OdFiles( store )[^1] );
        var before = Content( store );

        var driver = new TestDriver();
        await Should.ThrowAsync<InvalidOperationException>( () => driver.DoHostStartAsync( monitor, services, _noConfigurators, _noHandlers ) );

        Content( store ).ShouldBe( before );
    }

    [Test]
    public async Task the_League_named_knobs_reach_the_collector_and_the_client()
    {
        var monitor = new ActivityMonitor();
        var services = NewServices( TestStore.NewDirectory(), "TestDomain", extra: new Dictionary<string, string?>
        {
            ["CK-ObservableDomain:Domains:TestDomain:ExportedEventKeepDuration"] = "00:10:00",
            ["CK-ObservableDomain:Domains:TestDomain:ExportedEventKeepLimit"] = "42",
            ["CK-ObservableDomain:Domains:TestDomain:DebugMode"] = "true",
        } );

        var driver = new TestDriver();
        await driver.DoHostStartAsync( monitor, services, _noConfigurators, _noHandlers );

        // Binding is not wiring: these are the properties the League sets in ObservableLeague.Shell.
        driver.EventCollector.KeepDuration.ShouldBe( TimeSpan.FromMinutes( 10 ) );
        driver.EventCollector.KeepLimit.ShouldBe( 42 );
        // The driver has no public Client property: the domain gives its client.
        driver.Domain.DomainClient.ShouldBeOfType<PersistentDomainClient>().DebugMode.ShouldBeTrue();
        await StopAsync( monitor, driver );
    }

    [Test]
    public async Task handlers_receive_the_outcome_before_PreConfigure()
    {
        var monitor = new ActivityMonitor();
        var services = NewServices( TestStore.NewDirectory(), "TestDomain" );
        var handler = new RecordingHandler<TestDriver>();

        var driver = new TestDriver();
        await driver.DoHostStartAsync( monitor, services, _noConfigurators, new[] { handler } );

        var outcome = handler.Outcomes.ShouldHaveSingleItem();
        outcome.Kind.ShouldBe( PersistentDomainLoadKind.Created );
        driver.OutcomeInPreConfigure.ShouldBeSameAs( outcome );
        driver.LoadOutcome.ShouldBeSameAs( outcome );
        await StopAsync( monitor, driver );
    }

    [Test]
    public async Task handler_returning_false_stops_the_start_and_writes_nothing()
    {
        var monitor = new ActivityMonitor();
        var store = TestStore.NewDirectory();
        var services = NewServices( store, "TestDomain" );
        await StartAndStopAsync( monitor, services, store );
        var before = Content( store );
        var refusing = new RecordingHandler<TestDriver>( result: false );
        var accepting = new RecordingHandler<TestDriver>();

        var driver = new TestDriver();
        await Should.ThrowAsync<InvalidOperationException>( () => driver.DoHostStartAsync( monitor, services, _noConfigurators, new[] { refusing, accepting } ) );

        // All the handlers run, as the configurators do. Then the start fails.
        refusing.Outcomes.ShouldHaveSingleItem().Kind.ShouldBe( PersistentDomainLoadKind.Loaded );
        accepting.Outcomes.ShouldHaveSingleItem().Kind.ShouldBe( PersistentDomainLoadKind.Loaded );
        Content( store ).ShouldBe( before );
    }

    [Test]
    public async Task no_save_writes_after_the_stop()
    {
        var monitor = new ActivityMonitor();
        var store = TestStore.NewDirectory();
        var services = NewServices( store, "TestDomain" );
        var driver = new TestDriver();
        await driver.DoHostStartAsync( monitor, services, _noConfigurators, _noHandlers );
        await driver.DoHostStopAsync( monitor );
        var before = Content( store );

        // The stop disposes the domain: no commit can come after it.
        await Should.ThrowAsync<ObservableDomainDisposedException>( () => SetMarkerAsync( monitor, driver, "after stop" ) );
        // A save post action that a commit added before the stop and that runs after it writes no file.
        var saver = driver.Saver.ShouldNotBeNull();
        saver.Offer( new PendingSnapshot( 1000, DateTime.UtcNow, new byte[] { 1, 2, 3 } ) );
        (await saver.SaveAsync( monitor )).ShouldBeFalse();

        Content( store ).ShouldBe( before );
        driver.Domain.Dispose();
    }

    [Test]
    public async Task no_save_writes_after_a_failed_start()
    {
        var monitor = new ActivityMonitor();
        var store = TestStore.NewDirectory();
        var services = NewServices( store, "TestDomain" );
        var driver = new TestDriver();
        // The domain wraps the error of the initial transaction.
        await Should.ThrowAsync<Exception>( () => driver.DoHostStartAsync( monitor, services, new[] { new RefusingConfigurator() }, _noHandlers ) );
        var before = Content( store );
        OdFiles( store ).ShouldBeEmpty();
        // The driver creates the saver after the initial transaction: a start that fails before has no saver.
        driver.Saver.ShouldBeNull();

        await Task.Delay( 100 );
        Content( store ).ShouldBe( before );
        // The lock is released: a new driver starts on the folder.
        var driver2 = new TestDriver();
        await driver2.DoHostStartAsync( monitor, services, _noConfigurators, _noHandlers );
        await StopAsync( monitor, driver2 );
    }

    [Test]
    public async Task strict_failure_calls_the_handlers_with_Failed()
    {
        var monitor = new ActivityMonitor();
        var store = TestStore.NewDirectory();
        var services = NewServices( store, "TestDomain" );
        await StartAndStopAsync( monitor, services, store );
        ZeroFill( OdFiles( store )[^1] );
        var before = Content( store );
        var first = new RecordingHandler<TestDriver>( throws: true );
        var second = new RecordingHandler<TestDriver>();

        var driver = new TestDriver();
        await Should.ThrowAsync<InvalidOperationException>( () => driver.DoHostStartAsync( monitor, services, _noConfigurators, new[] { first, second } ) );

        first.Outcomes.ShouldHaveSingleItem().Kind.ShouldBe( PersistentDomainLoadKind.Failed );
        second.Outcomes.ShouldHaveSingleItem().Kind.ShouldBe( PersistentDomainLoadKind.Failed );
        Content( store ).ShouldBe( before );
    }

    [Test]
    public async Task a_strict_failure_has_the_first_skipped_file_error_as_inner_exception()
    {
        var monitor = new ActivityMonitor();
        var store = TestStore.NewDirectory();
        var services = NewServices( store, "TestDomain" );
        await StartAndStopAsync( monitor, services, store );
        // A valid file whose payload does not deserialize is the newest file.
        var payload = new byte[64];
        payload[0] = 1;
        new GenerationStore( store, "TestDomain" ).WriteGeneration( 1, DateTime.UtcNow, payload );

        var driver = new TestDriver();
        var ex = await Should.ThrowAsync<InvalidOperationException>( () => driver.DoHostStartAsync( monitor, services, _noConfigurators, _noHandlers ) );

        var skipped = driver.LoadOutcome.ShouldNotBeNull().SkippedFiles.ShouldHaveSingleItem();
        skipped.Result.ShouldBe( SnapshotFileResult.Unloadable );
        ex.InnerException.ShouldNotBeNull().ShouldBeSameAs( skipped.Error );
    }

    [Test]
    public async Task second_driver_on_the_same_folder_fails_on_the_lock()
    {
        var monitor = new ActivityMonitor();
        var services = NewServices( TestStore.NewDirectory(), "TestDomain" );

        var driver1 = new TestDriver();
        await driver1.DoHostStartAsync( monitor, services, _noConfigurators, _noHandlers );
        var driver2 = new TestDriver();
        await Should.ThrowAsync<InvalidOperationException>( () => driver2.DoHostStartAsync( monitor, services, _noConfigurators, _noHandlers ) );

        await StopAsync( monitor, driver1 );
        await driver2.DoHostStartAsync( monitor, services, _noConfigurators, _noHandlers );
        driver2.LoadOutcome.ShouldNotBeNull().Kind.ShouldBe( PersistentDomainLoadKind.Loaded );
        await StopAsync( monitor, driver2 );
    }

    [Test]
    public async Task missing_store_path_and_identity_service_fails_with_a_clear_error()
    {
        var monitor = new ActivityMonitor();
        var services = new SingleServiceProvider( new ConfigurationBuilder().Build() );

        var driver = new TestDriver();
        var ex = await Should.ThrowAsync<InvalidOperationException>( () => driver.DoHostStartAsync( monitor, services, _noConfigurators, _noHandlers ) );

        ex.Message.ShouldContain( "TestDomain" );
    }

    [Test]
    public async Task an_empty_store_path_is_the_default_folder()
    {
        // A JSON null binds as an empty string. The driver uses its default folder, here with no identity service.
        var monitor = new ActivityMonitor();
        var services = new SingleServiceProvider( new ConfigurationBuilder().AddInMemoryCollection( new Dictionary<string, string?>
        {
            ["CK-ObservableDomain:Domains:TestDomain:StorePath"] = "",
        } ).Build() );

        var driver = new TestDriver();
        using var infos = monitor.CollectEntries( out var entries, LogLevelFilter.Info );
        var ex = await Should.ThrowAsync<InvalidOperationException>( () => driver.DoHostStartAsync( monitor, services, _noConfigurators, _noHandlers ) );

        ex.Message.ShouldContain( "no ApplicationIdentityService" );
        // The operator sees that the empty value is not a folder.
        entries.ShouldContain( e => e.MaskedLevel == LogLevel.Info && e.Text.Contains( "StorePath" ) && e.Text.Contains( "empty" ) );
    }

    [Test]
    public async Task a_newest_file_renamed_with_another_case_loads_and_the_start_succeeds()
    {
        var monitor = new ActivityMonitor();
        var store = TestStore.NewDirectory();
        var services = NewServices( store, "TestDomain" );
        await StartAndStopAsync( monitor, services, store );
        var newest = OdFiles( store )[^1];
        var renamed = Path.ChangeExtension( newest, ".OD" );
        File.Move( newest, renamed );

        var driver = new TestDriver();
        await driver.DoHostStartAsync( monitor, services, _noConfigurators, _noHandlers );

        var outcome = driver.LoadOutcome.ShouldNotBeNull();
        outcome.Kind.ShouldBe( PersistentDomainLoadKind.Loaded );
        outcome.SkippedFiles.ShouldBeEmpty();
        await StopAsync( monitor, driver );
        File.Exists( renamed ).ShouldBeTrue();
    }

    [Test]
    public async Task stop_writes_a_final_generation()
    {
        var monitor = new ActivityMonitor();
        var store = TestStore.NewDirectory();
        var services = NewServices( store, "TestDomain", snapshotSaveDelay: "-00:00:01" );

        var driver = new TestDriver();
        await driver.DoHostStartAsync( monitor, services, _noConfigurators, _noHandlers );
        await SetMarkerAsync( monitor, driver, "before stop" );
        // A negative delay: the commit writes no file.
        OdFiles( store ).Length.ShouldBe( 1 );
        await StopAsync( monitor, driver );

        OdFiles( store ).Length.ShouldBe( 2 );
        MarkerOf( monitor, OdFiles( store )[^1] ).ShouldBe( "before stop" );
    }

    [Test]
    public async Task fallback_start_marks_the_damaged_file_after_success()
    {
        var monitor = new ActivityMonitor();
        var store = TestStore.NewDirectory();
        var services = NewServices( store, "TestDomain", extra: new Dictionary<string, string?>
        {
            ["CK-ObservableDomain:Domains:TestDomain:FallbackGenerationCount"] = "1",
        } );
        await StartAndStopAsync( monitor, services, store );
        var newest = OdFiles( store )[^1];
        ZeroFill( newest );

        var driver = new TestDriver();
        await driver.DoHostStartAsync( monitor, services, _noConfigurators, _noHandlers );

        driver.LoadOutcome.ShouldNotBeNull().Kind.ShouldBe( PersistentDomainLoadKind.LoadedFromFallback );
        File.Exists( newest ).ShouldBeFalse();
        File.Exists( newest + ".damaged" ).ShouldBeTrue();
        await StopAsync( monitor, driver );
    }

    [Test]
    public async Task retention_at_start_keeps_the_superseded_files()
    {
        var monitor = new ActivityMonitor();
        var store = TestStore.NewDirectory();
        var services = NewServices( store, "TestDomain", createOnLoadError: true, extra: new Dictionary<string, string?>
        {
            ["CK-ObservableDomain:Domains:TestDomain:FallbackGenerationCount"] = "1",
            ["CK-ObservableDomain:Domains:TestDomain:GenerationCount"] = "3",
        } );
        await WriteGenerationsAsync( store, "TestDomain", 5 );
        var files = OdFiles( store );
        files.Length.ShouldBe( 5 );
        ZeroFill( files[4] );
        ZeroFill( files[3] );

        var driver = new TestDriver();
        await driver.DoHostStartAsync( monitor, services, _noConfigurators, _noHandlers );

        driver.LoadOutcome.ShouldNotBeNull().Kind.ShouldBe( PersistentDomainLoadKind.CreatedAfterLoadFailure );
        // The 3 untried files are marked and the retention deletes none of them.
        for( int i = 0; i < 3; ++i )
        {
            File.Exists( files[i] ).ShouldBeFalse();
            File.Exists( files[i] + ".superseded" ).ShouldBeTrue();
        }
        File.Exists( files[3] + ".damaged" ).ShouldBeTrue();
        File.Exists( files[4] + ".damaged" ).ShouldBeTrue();
        // The new generation exists.
        var newFiles = OdFiles( store );
        newFiles.ShouldHaveSingleItem().ShouldNotBeOneOf( files );
        await StopAsync( monitor, driver );
    }

    [Test]
    public async Task an_invalid_configuration_fails_the_start_and_changes_no_file()
    {
        var monitor = new ActivityMonitor();
        var store = TestStore.NewDirectory();
        await StartAndStopAsync( monitor, NewServices( store, "TestDomain" ), store );
        var before = Content( store );
        var services = NewServices( store, "TestDomain", extra: new Dictionary<string, string?>
        {
            ["CK-ObservableDomain:Domains:TestDomain:GenerationCount"] = "3",
            ["CK-ObservableDomain:Domains:TestDomain:FallbackGenerationCount"] = "3",
        } );

        var driver = new TestDriver();
        var ex = await Should.ThrowAsync<InvalidOperationException>( () => driver.DoHostStartAsync( monitor, services, _noConfigurators, _noHandlers ) );

        ex.Message.ShouldContain( "invalid options" );
        driver.LoadOutcome.ShouldBeNull();
        Content( store ).ShouldBe( before );
    }

    [Test]
    public async Task a_value_that_does_not_convert_fails_the_start_with_the_domain_and_the_key()
    {
        var monitor = new ActivityMonitor();
        var store = TestStore.NewDirectory();
        // The valid name is "GZiped". The entry has a valid StorePath next to the typo.
        var services = NewServices( store, "TestDomain", extra: new Dictionary<string, string?>
        {
            ["CK-ObservableDomain:Domains:TestDomain:CompressionKind"] = "GZip",
        } );

        var driver = new TestDriver();
        var ex = await Should.ThrowAsync<InvalidOperationException>( () => driver.DoHostStartAsync( monitor, services, _noConfigurators, _noHandlers ) );

        ex.Message.ShouldContain( "'TestDomain'" );
        ex.Message.ShouldContain( "CompressionKind" );
        driver.LoadOutcome.ShouldBeNull();
        Directory.GetFiles( store ).ShouldBeEmpty();
    }

    // R8: the start does not wait for the domain post actions of the initial transaction, so their errors do not fail it.
    [Test]
    public async Task a_failing_domain_post_action_of_the_initial_transaction_does_not_fail_the_start()
    {
        var monitor = new ActivityMonitor();
        var store = TestStore.NewDirectory();
        var services = NewServices( store, "TestDomain", createOnLoadError: true, extra: new Dictionary<string, string?>
        {
            ["CK-ObservableDomain:Domains:TestDomain:FallbackGenerationCount"] = "1",
            ["CK-ObservableDomain:Domains:TestDomain:GenerationCount"] = "3",
        } );
        await WriteGenerationsAsync( store, "TestDomain", 5 );
        var files = OdFiles( store );
        ZeroFill( files[4] );
        ZeroFill( files[3] );

        var driver = new TestDriver();
        await driver.DoHostStartAsync( monitor, services, new[] { new FailingDomainPostActionConfigurator() }, _noHandlers );
        driver.LoadOutcome.ShouldNotBeNull().Kind.ShouldBe( PersistentDomainLoadKind.CreatedAfterLoadFailure );

        // The start wrote a generation of the new domain, so the marks are on disk too.
        var written = OdFiles( store ).ShouldHaveSingleItem();
        written.ShouldNotBeOneOf( files );
        AssertRecoveryMarks( files );
        await StopAsync( monitor, driver );

        // The next start loads the new domain. Its saves and its retention keep the marked files.
        var driver2 = new TestDriver();
        await driver2.DoHostStartAsync( monitor, services, _noConfigurators, _noHandlers );
        driver2.LoadOutcome.ShouldNotBeNull().Kind.ShouldBe( PersistentDomainLoadKind.Loaded );
        for( int i = 0; i < 4; ++i ) await SetMarkerAsync( monitor, driver2, $"run {i}" );
        await StopAsync( monitor, driver2 );
        AssertRecoveryMarks( files );
        OdFiles( store ).Length.ShouldBe( 3 );
    }

    [Test]
    public async Task a_configurator_failure_after_a_recovery_changes_no_file()
    {
        var monitor = new ActivityMonitor();
        var store = TestStore.NewDirectory();
        var services = NewServices( store, "TestDomain", createOnLoadError: true, extra: new Dictionary<string, string?>
        {
            ["CK-ObservableDomain:Domains:TestDomain:FallbackGenerationCount"] = "1",
            ["CK-ObservableDomain:Domains:TestDomain:GenerationCount"] = "3",
        } );
        await WriteGenerationsAsync( store, "TestDomain", 5 );
        var files = OdFiles( store );
        ZeroFill( files[4] );
        ZeroFill( files[3] );
        var before = Content( store );

        var driver = new TestDriver();
        await Should.ThrowAsync<Exception>( () => driver.DoHostStartAsync( monitor, services, new[] { new RefusingConfigurator() }, _noHandlers ) );

        // The start wrote no generation, so it marked no file. The next start reports the same recovery.
        // The start created the lock file: it is not a generation file.
        var after = Content( store );
        after.Remove( "TestDomain.lock" ).ShouldBeTrue();
        after.ShouldBe( before );
        var driver2 = new TestDriver();
        await driver2.DoHostStartAsync( monitor, services, _noConfigurators, _noHandlers );
        var outcome = driver2.LoadOutcome.ShouldNotBeNull();
        outcome.Kind.ShouldBe( PersistentDomainLoadKind.CreatedAfterLoadFailure );
        outcome.SkippedFiles.Count.ShouldBe( 2 );
        outcome.SupersededFiles.Count.ShouldBe( 3 );
        await StopAsync( monitor, driver2 );
        AssertRecoveryMarks( files );
    }

    // Windows only: on Unix, rename(2) ignores the FileShare emulation of .NET (an advisory flock),
    // so the handle blocks no rename and the production code has nothing to handle.
    [Test]
    [Platform( "Win" )]
    public async Task a_failed_mark_protects_the_file_until_a_later_retention_marks_it()
    {
        var monitor = new ActivityMonitor();
        var store = TestStore.NewDirectory();
        var services = NewServices( store, "TestDomain", createOnLoadError: true, extra: new Dictionary<string, string?>
        {
            ["CK-ObservableDomain:Domains:TestDomain:FallbackGenerationCount"] = "1",
            ["CK-ObservableDomain:Domains:TestDomain:GenerationCount"] = "2",
        } );
        await WriteGenerationsAsync( store, "TestDomain", 5 );
        var files = OdFiles( store );
        ZeroFill( files[4] );
        ZeroFill( files[3] );

        var driver = new TestDriver();
        // An antivirus holds the oldest untried file without FileShare.Delete: its rename fails.
        using( new FileStream( files[0], FileMode.Open, FileAccess.Read, FileShare.Read ) )
        {
            await driver.DoHostStartAsync( monitor, services, _noConfigurators, _noHandlers );
            driver.LoadOutcome.ShouldNotBeNull().Kind.ShouldBe( PersistentDomainLoadKind.CreatedAfterLoadFailure );
            File.Exists( files[0] ).ShouldBeTrue();
            File.Exists( files[1] + ".superseded" ).ShouldBeTrue();

            // More saves than GenerationCount: the count retention does not delete the unmarked file.
            for( int i = 0; i < 3; ++i ) await SetMarkerAsync( monitor, driver, $"run {i}" );
            File.Exists( files[0] ).ShouldBeTrue();
            OdFiles( store ).Length.ShouldBe( 3 );
        }

        // The handle is closed. The retention of the next save marks the file.
        await SetMarkerAsync( monitor, driver, "after the release" );
        File.Exists( files[0] ).ShouldBeFalse();
        File.Exists( files[0] + ".superseded" ).ShouldBeTrue();
        OdFiles( store ).Length.ShouldBe( 2 );
        await StopAsync( monitor, driver );
    }

    [Test]
    public async Task a_hanging_domain_post_action_does_not_delay_the_stop()
    {
        var monitor = new ActivityMonitor();
        var store = TestStore.NewDirectory();
        var services = NewServices( store, "TestDomain", snapshotSaveDelay: "-00:00:01" );
        var driver = new TestDriver();
        await driver.DoHostStartAsync( monitor, services, _noConfigurators, _noHandlers );
        var release = new TaskCompletionSource( TaskCreationOptions.RunContinuationsAsynchronously );
        driver.Domain.TransactionDone += ( sender, e ) => e.DomainPostActions.Add( ctx => release.Task );
        // This commit starts a domain post action that does not end. The stop must save this commit.
        await driver.Domain.ModifyThrowAsync( monitor, () => driver.Domain.AllObjects.Items.OfType<OTestSingleton>().Single().Marker = "hang" );
        try
        {
            var stop = driver.DoHostStopAsync( monitor );
            (await Task.WhenAny( stop, Task.Delay( 5000 ) )).ShouldBe( stop, "The stop waits for the domain post action." );
            await stop;
            // The final save is written and the lock is released.
            OdFiles( store ).Length.ShouldBe( 2 );
            MarkerOf( monitor, OdFiles( store )[^1] ).ShouldBe( "hang" );
            var driver2 = new TestDriver();
            await driver2.DoHostStartAsync( monitor, services, _noConfigurators, _noHandlers );
            await StopAsync( monitor, driver2 );
        }
        finally
        {
            release.SetResult();
        }
        driver.Domain.Dispose();
    }

    [Test]
    public async Task a_gziped_domain_reloads_with_the_same_state()
    {
        var monitor = new ActivityMonitor();
        var store = TestStore.NewDirectory();
        var services = NewServices( store, "TestDomain", extra: new Dictionary<string, string?>
        {
            ["CK-ObservableDomain:Domains:TestDomain:CompressionKind"] = "GZiped",
        } );

        var driver1 = new TestDriver();
        await driver1.DoHostStartAsync( monitor, services, _noConfigurators, _noHandlers );
        await SetMarkerAsync( monitor, driver1, "compressed" );
        await StopAsync( monitor, driver1 );

        var driver2 = new TestDriver();
        await driver2.DoHostStartAsync( monitor, services, _noConfigurators, _noHandlers );
        driver2.LoadOutcome.ShouldNotBeNull().Kind.ShouldBe( PersistentDomainLoadKind.Loaded );
        driver2.Domain.DomainClient.ShouldBeOfType<PersistentDomainClient>().CompressionKind.ShouldBe( CompressionKind.GZiped );
        string? marker = null;
        await driver2.Domain.ModifyThrowAsync( monitor, () =>
        {
            marker = driver2.Domain.AllObjects.Items.OfType<OTestSingleton>().Single().Marker;
        } );
        marker.ShouldBe( "compressed" );
        await StopAsync( monitor, driver2 );
    }

    /// <summary>
    /// Checks the marks of a recovery over 5 generations: the 3 oldest are superseded and the 2 newest are damaged.
    /// </summary>
    static void AssertRecoveryMarks( string[] files )
    {
        for( int i = 0; i < 3; ++i )
        {
            File.Exists( files[i] ).ShouldBeFalse();
            File.Exists( files[i] + ".superseded" ).ShouldBeTrue();
        }
        File.Exists( files[3] + ".damaged" ).ShouldBeTrue();
        File.Exists( files[4] + ".damaged" ).ShouldBeTrue();
    }

    /// <summary>
    /// Starts and stops a <see cref="TestDriver"/>. The folder then has 2 generations: the start and one commit.
    /// The commit saved the last state, so the stop writes no file.
    /// </summary>
    static async Task StartAndStopAsync( IActivityMonitor monitor, IServiceProvider services, string store )
    {
        var driver = new TestDriver();
        await driver.DoHostStartAsync( monitor, services, _noConfigurators, _noHandlers );
        await SetMarkerAsync( monitor, driver, "first run" );
        await StopAsync( monitor, driver );
        OdFiles( store ).Length.ShouldBe( 2 );
    }

    static Task SetMarkerAsync( IActivityMonitor monitor, TestDriver driver, string marker )
    {
        return driver.Domain.ModifyThrowAsync( monitor, () =>
        {
            driver.Domain.AllObjects.Items.OfType<OTestSingleton>().Single().Marker = marker;
        }, waitForDomainPostActionsCompletion: true );
    }

    static async Task StopAsync( IActivityMonitor monitor, IObservableDomainDriver driver )
    {
        switch( driver )
        {
            case TestDriver t: await t.DoHostStopAsync( monitor ); break;
            case SeedingDriver s: await s.DoHostStopAsync( monitor ); break;
        }
        driver.Domain.Dispose();
    }

    /// <summary>Writes <paramref name="count"/> generations of an empty domain with a store, a client and a saver.</summary>
    static async Task WriteGenerationsAsync( string folder, string domainName, int count )
    {
        var monitor = new ActivityMonitor();
        var options = new PersistentDomainOptions();
        var client = new PersistentDomainClient( domainName, options, null );
        using var domain = client.CreateNew( monitor );
        var saver = new SnapshotSaver( new GenerationStore( folder, domainName ), options, m => client.CaptureCommittedSnapshot( m, domain ), () => client.LastCommittedTransactionNumber );
        client.Saver = saver;
        for( int i = 0; i < count; ++i )
        {
            await domain.ModifyThrowAsync( monitor, () => new OCounter { Count = i }, waitForDomainPostActionsCompletion: true );
        }
        await saver.StopAsync();
        saver.Dispose();
    }

    static string? MarkerOf( IActivityMonitor monitor, string path )
    {
        byte[]? payload;
        using( var input = File.OpenRead( path ) )
        {
            SnapshotFileFormat.Read( input, out _, out payload ).ShouldBe( SnapshotReadStatus.Valid );
        }
        payload.ShouldNotBeNull();
        var client = new PersistentDomainClient( "TestDomain", new PersistentDomainOptions(), null );
        using var d = client.Load( monitor, payload );
        return d.AllObjects.Items.OfType<OTestSingleton>().Single().Marker;
    }

    static string[] OdFiles( string folder ) => Directory.GetFiles( folder, "*.od" ).OrderBy( p => p, StringComparer.Ordinal ).ToArray();

    /// <summary>Gets the name and the bytes of each file, as a comparable map.</summary>
    static Dictionary<string, string> Content( string folder )
        => Directory.GetFiles( folder ).ToDictionary( p => Path.GetFileName( p ), p => Convert.ToBase64String( File.ReadAllBytes( p ) ) );

    static void ZeroFill( string path )
    {
        long length = new FileInfo( path ).Length;
        File.WriteAllBytes( path, new byte[length] );
    }

    static SingleServiceProvider NewServices( string store,
                                              string domainName,
                                              bool? createOnLoadError = null,
                                              string snapshotSaveDelay = "00:00:00",
                                              Dictionary<string, string?>? extra = null )
    {
        var values = new Dictionary<string, string?>
        {
            [$"CK-ObservableDomain:Domains:{domainName}:StorePath"] = store,
            [$"CK-ObservableDomain:Domains:{domainName}:SnapshotSaveDelay"] = snapshotSaveDelay,
        };
        if( createOnLoadError.HasValue )
        {
            values[$"CK-ObservableDomain:Domains:{domainName}:CreateOnLoadError"] = createOnLoadError.Value ? "true" : "false";
        }
        if( extra != null )
        {
            foreach( var kv in extra ) values[kv.Key] = kv.Value;
        }
        return new SingleServiceProvider( new ConfigurationBuilder().AddInMemoryCollection( values ).Build() );
    }
}
