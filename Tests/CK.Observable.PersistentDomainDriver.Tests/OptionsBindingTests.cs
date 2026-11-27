using System;
using System.Collections.Generic;
using System.IO;
using CK.Core;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using Shouldly;

namespace CK.Observable.PersistentDomainDriverTests;

[TestFixture]
public class OptionsBindingTests
{
    [Test]
    public void options_bind_from_CK_ObservableDomain_section()
    {
        var storePath = Path.GetFullPath( "MyStore" );
        var configuration = new ConfigurationBuilder().AddInMemoryCollection( new Dictionary<string, string?>
        {
            ["CK-ObservableDomain:Domains:D:StorePath"] = storePath,
            ["CK-ObservableDomain:Domains:D:SnapshotSaveDelay"] = "00:00:00.5",
            ["CK-ObservableDomain:Domains:D:GenerationCount"] = "8",
            ["CK-ObservableDomain:Domains:D:FallbackGenerationCount"] = "2",
            ["CK-ObservableDomain:Domains:D:CreateOnLoadError"] = "true",
            ["CK-ObservableDomain:Domains:D:DamagedFileKeepDuration"] = "1.00:00:00",
            ["CK-ObservableDomain:Domains:D:CompressionKind"] = "GZiped",
            ["CK-ObservableDomain:Domains:D:SkipTransactionCount"] = "3",
            ["CK-ObservableDomain:Domains:D:ExportedEventKeepDuration"] = "00:10:00",
            ["CK-ObservableDomain:Domains:D:ExportedEventKeepLimit"] = "42",
            ["CK-ObservableDomain:Domains:D:DebugMode"] = "true",
        } ).Build();

        var options = configuration.GetSection( PersistentDomainDriverOptions.SectionName ).Get<PersistentDomainDriverOptions>();

        options.ShouldNotBeNull();
        var d = options.GetDomainOptions( "D" );
        d.StorePath.ShouldBe( storePath );
        d.SnapshotSaveDelay.ShouldBe( TimeSpan.FromMilliseconds( 500 ) );
        d.GenerationCount.ShouldBe( 8 );
        d.FallbackGenerationCount.ShouldBe( 2 );
        d.CreateOnLoadError.ShouldBeTrue();
        d.DamagedFileKeepDuration.ShouldBe( TimeSpan.FromDays( 1 ) );
        d.CompressionKind.ShouldBe( CompressionKind.GZiped );
        d.SkipTransactionCount.ShouldBe( 3 );
        d.ExportedEventKeepDuration.ShouldBe( TimeSpan.FromMinutes( 10 ) );
        d.ExportedEventKeepLimit.ShouldBe( 42 );
        d.DebugMode.ShouldBeTrue();
        d.Validate( new ActivityMonitor(), "D" ).ShouldBeTrue();
    }

    [Test]
    public void a_value_that_does_not_convert_throws_and_names_the_domain_and_the_key()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection( new Dictionary<string, string?>
        {
            ["CK-ObservableDomain:Domains:D:StorePath"] = Path.GetFullPath( "MyStore" ),
            ["CK-ObservableDomain:Domains:D:CompressionKind"] = "GZip",
        } ).Build();

        var ex = Should.Throw<InvalidOperationException>( () => PersistentDomainDriverOptions.Read( configuration, "D" ) );

        ex.Message.ShouldContain( "'D'" );
        ex.Message.ShouldContain( "CK-ObservableDomain:Domains:D:CompressionKind" );
        ex.InnerException.ShouldNotBeNull();
    }

    [Test]
    public void read_finds_the_entry_of_the_domain_whatever_the_case_and_ignores_the_other_entries()
    {
        var storePath = Path.GetFullPath( "MyStore" );
        var configuration = new ConfigurationBuilder().AddInMemoryCollection( new Dictionary<string, string?>
        {
            ["CK-ObservableDomain:Domains:d:StorePath"] = storePath,
            ["CK-ObservableDomain:Domains:d:GenerationCount"] = "8",
            // The typo of another domain does not fail this domain.
            ["CK-ObservableDomain:Domains:Other:CompressionKind"] = "GZip",
        } ).Build();

        var d = PersistentDomainDriverOptions.Read( configuration, "D" );
        d.StorePath.ShouldBe( storePath );
        d.GenerationCount.ShouldBe( 8 );

        // No entry and no configuration give the defaults.
        PersistentDomainDriverOptions.Read( configuration, "None" ).GenerationCount.ShouldBe( 5 );
        PersistentDomainDriverOptions.Read( null, "D" ).GenerationCount.ShouldBe( 5 );
    }

    [Test]
    public void defaults_match_the_spec()
    {
        var o = new PersistentDomainOptions();
        o.StorePath.ShouldBeNull();
        o.SnapshotSaveDelay.ShouldBe( TimeSpan.Zero );
        o.GenerationCount.ShouldBe( 5 );
        o.FallbackGenerationCount.ShouldBe( 0 );
        o.CreateOnLoadError.ShouldBeFalse();
        o.DamagedFileKeepDuration.ShouldBe( TimeSpan.FromDays( 7 ) );
        o.CompressionKind.ShouldBe( CompressionKind.None );
        o.SkipTransactionCount.ShouldBe( 0 );
        o.ExportedEventKeepDuration.ShouldBe( TimeSpan.FromMinutes( 5 ) );
        o.ExportedEventKeepLimit.ShouldBe( 10 );
        o.DebugMode.ShouldBeFalse();
    }

    [Test]
    public void defaults_are_valid()
    {
        new PersistentDomainOptions().Validate( new ActivityMonitor(), "D" ).ShouldBeTrue();
    }

    [Test]
    public void fallback_count_must_be_lower_than_generation_count()
    {
        var o = new PersistentDomainOptions { GenerationCount = 3, FallbackGenerationCount = 3 };
        var (monitor, collector) = NewMonitor();
        o.Validate( monitor, "D" ).ShouldBeFalse();
        var error = collector.Entries.ShouldHaveSingleItem().Text;
        error.ShouldContain( "'D'" );
        error.ShouldContain( "FallbackGenerationCount" );
    }

    [Test]
    public void relative_store_path_is_rejected()
    {
        var o = new PersistentDomainOptions { StorePath = "Data" };
        var (monitor, collector) = NewMonitor();
        o.Validate( monitor, "D" ).ShouldBeFalse();
        var error = collector.Entries.ShouldHaveSingleItem().Text;
        error.ShouldContain( "'D'" );
        error.ShouldContain( "StorePath" );
    }

    [Test]
    public void huge_damaged_keep_duration_is_rejected()
    {
        var o = new PersistentDomainOptions { DamagedFileKeepDuration = TimeSpan.MaxValue };
        var (monitor, collector) = NewMonitor();
        o.Validate( monitor, "D" ).ShouldBeFalse();
        var error = collector.Entries.ShouldHaveSingleItem().Text;
        error.ShouldContain( "'D'" );
        error.ShouldContain( "DamagedFileKeepDuration" );
        // The upper bound itself is valid.
        new PersistentDomainOptions { DamagedFileKeepDuration = TimeSpan.FromDays( 3650 ) }
            .Validate( new ActivityMonitor(), "D" ).ShouldBeTrue();
    }

    [Test]
    public void huge_snapshot_save_delay_is_rejected()
    {
        var o = new PersistentDomainOptions { SnapshotSaveDelay = TimeSpan.FromDays( 1 ) + TimeSpan.FromSeconds( 1 ) };
        var (monitor, collector) = NewMonitor();
        o.Validate( monitor, "D" ).ShouldBeFalse();
        var error = collector.Entries.ShouldHaveSingleItem().Text;
        error.ShouldContain( "'D'" );
        error.ShouldContain( "SnapshotSaveDelay" );
        // The upper bound itself is valid.
        new PersistentDomainOptions { SnapshotSaveDelay = TimeSpan.FromDays( 1 ) }
            .Validate( new ActivityMonitor(), "D" ).ShouldBeTrue();
    }

    // The event collector subtracts the duration from the current time: a huge value throws at each transaction.
    [Test]
    public void huge_exported_event_keep_duration_is_rejected()
    {
        var o = new PersistentDomainOptions { ExportedEventKeepDuration = TimeSpan.FromDays( 1 ) + TimeSpan.FromSeconds( 1 ) };
        var (monitor, collector) = NewMonitor();
        o.Validate( monitor, "D" ).ShouldBeFalse();
        var error = collector.Entries.ShouldHaveSingleItem().Text;
        error.ShouldContain( "'D'" );
        error.ShouldContain( "ExportedEventKeepDuration" );
        // The upper bound itself is valid.
        new PersistentDomainOptions { ExportedEventKeepDuration = TimeSpan.FromDays( 1 ) }
            .Validate( new ActivityMonitor(), "D" ).ShouldBeTrue();
    }

    [Test]
    public void negative_damaged_keep_duration_and_non_positive_generation_count_are_rejected()
    {
        var o = new PersistentDomainOptions { GenerationCount = 0, FallbackGenerationCount = -1, DamagedFileKeepDuration = TimeSpan.FromSeconds( -1 ) };
        var (monitor, collector) = NewMonitor();
        o.Validate( monitor, "D" ).ShouldBeFalse();
        // One error per invalid option.
        collector.Entries.Count.ShouldBe( 3 );
    }

    [Test]
    public void a_numeric_compression_kind_that_is_not_defined_is_rejected()
    {
        // The binder converts "2" to the enum with no error.
        var configuration = new ConfigurationBuilder().AddInMemoryCollection( new Dictionary<string, string?>
        {
            ["CK-ObservableDomain:Domains:D:CompressionKind"] = "2",
        } ).Build();
        var o = PersistentDomainDriverOptions.Read( configuration, "D" );
        var (monitor, collector) = NewMonitor();
        o.Validate( monitor, "D" ).ShouldBeFalse();
        var error = collector.Entries.ShouldHaveSingleItem().Text;
        error.ShouldContain( "'D'" );
        error.ShouldContain( "CompressionKind" );
    }

    [TestCase( nameof( PersistentDomainOptions.ExportedEventKeepLimit ) )]
    [TestCase( nameof( PersistentDomainOptions.ExportedEventKeepDuration ) )]
    [TestCase( nameof( PersistentDomainOptions.SkipTransactionCount ) )]
    public void a_value_below_the_limit_of_the_setter_is_rejected( string name )
    {
        var o = name switch
        {
            nameof( PersistentDomainOptions.ExportedEventKeepLimit ) => new PersistentDomainOptions { ExportedEventKeepLimit = 0 },
            nameof( PersistentDomainOptions.ExportedEventKeepDuration ) => new PersistentDomainOptions { ExportedEventKeepDuration = TimeSpan.FromSeconds( -1 ) },
            _ => new PersistentDomainOptions { SkipTransactionCount = -2 }
        };
        var (monitor, collector) = NewMonitor();
        o.Validate( monitor, "D" ).ShouldBeFalse();
        var error = collector.Entries.ShouldHaveSingleItem().Text;
        error.ShouldContain( "'D'" );
        error.ShouldContain( name );
    }

    [Test]
    public void the_limits_of_the_setters_are_valid()
    {
        new PersistentDomainOptions { ExportedEventKeepLimit = 1, ExportedEventKeepDuration = TimeSpan.Zero, SkipTransactionCount = -1 }
            .Validate( new ActivityMonitor(), "D" ).ShouldBeTrue();
    }

    [Test]
    public void an_empty_store_path_is_valid_because_a_json_null_binds_as_an_empty_string()
    {
        // The JSON provider 8 and 9 binds "StorePath": null as "". The driver then uses its default folder.
        new PersistentDomainOptions { StorePath = "" }.Validate( new ActivityMonitor(), "D" ).ShouldBeTrue();
    }

    static (IActivityMonitor Monitor, ActivityMonitorSimpleCollector Collector) NewMonitor()
    {
        var monitor = new ActivityMonitor();
        var collector = new ActivityMonitorSimpleCollector { MinimalFilter = LogLevelFilter.Error, Capacity = 50 };
        monitor.Output.RegisterClient( collector );
        return (monitor, collector);
    }
}
