using CK.Core;
using CK.Observable.League.Tests.Model;
using Shouldly;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.League.Tests;

[TestFixture]
public class ApplyEnsureDomainOptionsTests
{
    [Explicit]
    [TestCase( "WithAppIdentityService" )]
    [TestCase( "WithoutAppIdentityService" )]
    public async Task DefaultObservableLeague_can_be_configured_by_its_options_Async( string mode )
    {
        var options = new DefaultObservableLeagueOptions
        {
            StorePath = BasicLeagueTests.GetStorePath( nameof( DefaultObservableLeague_can_be_configured_by_its_options_Async ) ),
            EnsureDomains =
            {
                new EnsureDomainOptions
                {
                    DomainName = "WithNoRootTypesAndNoDefaultOptions",
                    CreateLifeCycleOption = DomainLifeCycleOption.Always,
                    CreateCompressionKind = CompressionKind.GZiped,
                    CreateSkipTransactionCount = 3712,
                    CreateSnapshotSaveDelay = TimeSpan.FromSeconds( 3713 ),
                    CreateSnapshotKeepDuration = TimeSpan.FromDays( 3714 ),
                    CreateSnapshotMaximalTotalKiB = 3715,
                    CreateHousekeepingRate = 3716,
                    CreateExportedEventKeepDuration = TimeSpan.FromMinutes( 3717 ),
                    CreateExportedEventKeepLimit = 3718
                },
                new EnsureDomainOptions
                {
                    DomainName = "WithOneRootType",
                    RootTypes =
                    {
                        typeof(School).AssemblyQualifiedName!
                    }
                }
            }
        };
        var emptyServices = new SimpleServiceContainer();
        // This tests that with ApplicationIdentityService available in the DI, the
        // start of the ApplicationIdentityService can occur after the call to DefaultObservableLeague.StartAsync.
        // Note that this should not happen in real life: the ApplicationIdentityService must be started before
        // any IHostedService.
        AppIdentity.ApplicationIdentityService? identityService = null;
        if( mode == "WithAppIdentityService" )
        {
            var identityServiceConfig = AppIdentity.ApplicationIdentityServiceConfiguration.CreateEmpty();
            identityService = new AppIdentity.ApplicationIdentityService( identityServiceConfig, emptyServices );
            identityService.Heartbeat.Sync += Heartbeat_Sync;
        }
        var def = new DefaultObservableLeague( emptyServices, Options.Create( options ), identityService );
        await ((IHostedService)def).StartAsync( default );
        // Starts ApplicationIdentityService after.
        if( identityService != null )
        {
            using( TestHelper.Monitor.OpenInfo( "Calling StartAndInitializeAsync." ) )
            {
                await identityService.StartAndInitializeAsync();
            }
        }

        def.Coordinator.Read( TestHelper.Monitor, ( monitor, d ) =>
        {
            var d1 = d.Root.Domains["WithNoRootTypesAndNoDefaultOptions"];
            d1.RootTypes.ShouldBeEmpty();
            d1.Options.LifeCycleOption.ShouldBe( DomainLifeCycleOption.Always );
            d1.Options.CompressionKind.ShouldBe( CompressionKind.GZiped );
            d1.Options.SkipTransactionCount.ShouldBe( 3712 );
            d1.Options.SnapshotSaveDelay.ShouldBe( TimeSpan.FromSeconds( 3713 ) );
            d1.Options.SnapshotKeepDuration.ShouldBe( TimeSpan.FromDays( 3714 ) );
            d1.Options.SnapshotMaximalTotalKiB.ShouldBe( 3715 );
            d1.Options.HousekeepingRate.ShouldBe( 3716 );
            d1.Options.ExportedEventKeepDuration.ShouldBe( TimeSpan.FromMinutes( 3717 ) );
            d1.Options.ExportedEventKeepLimit.ShouldBe( 3718 );

            var d2 = d.Root.Domains["WithOneRootType"];
            d2.RootTypes.Count.ShouldBe( 1 );
            d2.Options.LifeCycleOption.ShouldBe( DomainLifeCycleOption.Always );
            d2.RootTypes[0].ShouldStartWith( "CK.Observable.League.Tests.Model.School, " );
        } );

        var loader1 = def.Find( "WithNoRootTypesAndNoDefaultOptions" )!;
        var loader2 = def.Find( "WithOneRootType" )!;

        // Domain n°1: True, Domain n°2 (WithOneRootType): False.
        //
        // This is totally buggy...
        // The ObservableDomain definitely requires a heavy refoctoring!
        // 
        TestHelper.Monitor.Info( $"Domain n°1: {loader1.IsLoaded}, Domain n°2 (WithOneRootType): {loader2.IsLoaded}." );

        //{
        //    Debug.Assert( loader2 != null );
        //    loader2.IsLoaded.ShouldBeTrue();

        //    loader2.RootTypes.ShouldBe( new[] { typeof( School ) } );
        //    await using var d2 = await loader2.LoadAsync<School>( TestHelper.Monitor );
        //    Debug.Assert( d2 != null );
        //    d2.Read( TestHelper.Monitor, ( monitor, d ) =>
        //    {
        //        d.AllRoots.Count.ShouldBe( 1 );
        //        d.Root.Persons.ShouldBeEmpty();
        //    } );
        //}

        {
            Debug.Assert( loader1 != null );
            loader1.RootTypes.ShouldBeEmpty();
            loader1.IsLoaded.ShouldBeTrue();

            await using var d1 = await loader1.LoadAsync( TestHelper.Monitor );
            Debug.Assert( d1 != null );

            d1.Read( TestHelper.Monitor, ( monitor, d ) =>
            {
                d.AllRoots.Count.ShouldBe( 0 );
            } );
        }

        TestHelper.Monitor.Info( "Stopping the league..." );
        await ((IHostedService)def).StopAsync( default );
        if( identityService != null )
        {
            TestHelper.Monitor.Info( "Disposing the ApplicationIdentityService..." );
            await identityService.DisposeAsync();
        }
    }

    private void Heartbeat_Sync( IActivityMonitor monitor, int e )
    {
        monitor.Info( $"Heartbeat_Sync n°{e}." );
    }

    [Test]
    public async Task EnsureDomainOptions_ignores_existing_domains_Async()
    {
        var options = new DefaultObservableLeagueOptions
        {
            StorePath = BasicLeagueTests.GetStorePath( nameof( EnsureDomainOptions_ignores_existing_domains_Async ) ),
            EnsureDomains =
            {
                new EnsureDomainOptions
                {
                    DomainName = "NoRoot"
                },
                new EnsureDomainOptions
                {
                    DomainName = "OneRootType",
                    RootTypes =
                    {
                        typeof(School).AssemblyQualifiedName!
                    }
                },
                new EnsureDomainOptions
                {
                    DomainName = "TwoRootType",
                    RootTypes =
                    {
                        typeof(School).AssemblyQualifiedName!,
                        typeof(MicroMachine.Root).AssemblyQualifiedName!
                    }
                }
            }
        };

        var league = await ObservableLeague.LoadAsync( TestHelper.Monitor, new DirectoryStreamStore( options.StorePath ) );
        Debug.Assert( league != null );
        league.Coordinator.Read( TestHelper.Monitor, ( monitor, d ) => d.Root.Domains.Count ).ShouldBe( 0 );

        await league.ApplyEnsureDomainOptionsAsync( TestHelper.Monitor, options.EnsureDomains );
        league.Coordinator.Read( TestHelper.Monitor, ( monitor, d ) => d.Root.Domains.Count ).ShouldBe( 3 );

        using( TestHelper.Monitor.CollectEntries( out var entries, LogLevelFilter.Trace ) )
        {
            await league.ApplyEnsureDomainOptionsAsync( TestHelper.Monitor, options.EnsureDomains );
            entries.Select( e => e.Text )
                .ShouldContain( "Domain 'NoRoot' found. Root types '' match. Everything is fine." )
                .ShouldContain( "Domain 'OneRootType' found. Root types 'CK.Observable.League.Tests.Model.School' match. Everything is fine." )
                .ShouldContain( "Domain 'TwoRootType' found. Root types 'CK.Observable.League.Tests.Model.School', 'CK.Observable.League.Tests.MicroMachine.Root' match. Everything is fine." );
        }
    }

    [Test]
    public void DefaultObservableLeagueOptions_can_be_read_from_Configuration()
    {
        IConfigurationBuilder builder = new ConfigurationBuilder().AddInMemoryCollection( new Dictionary<string,string?>
        {
            { "CK-ObservableLeague:StorePath", "Some/Path" },
            { "CK-ObservableLeague:EnsureDomains:0:DomainName", "FirstDomain" },
            { "CK-ObservableLeague:EnsureDomains:0:RootTypes:0", "a root type" },
            { "CK-ObservableLeague:EnsureDomains:0:RootTypes:1", "another root type" },
            { "CK-ObservableLeague:EnsureDomains:0:CreateLifeCycleOption", "Never" },
            { "CK-ObservableLeague:EnsureDomains:0:CreateSnapshotKeepDuration", "0.12:34:56" },
        } );
        IConfigurationRoot root = builder.Build();
        var o = root.GetRequiredSection( "CK-ObservableLeague" ).Get<DefaultObservableLeagueOptions>();
        Debug.Assert( o != null );
        o.StorePath.ShouldBe( "Some/Path" );
        o.EnsureDomains.Count.ShouldBe( 1 );
        o.EnsureDomains[0].DomainName.ShouldBe( "FirstDomain" );
        o.EnsureDomains[0].RootTypes.ShouldContain( "a root type" ).ShouldContain( "another root type" );
        o.EnsureDomains[0].CreateLifeCycleOption.ShouldBe( DomainLifeCycleOption.Never );
        o.EnsureDomains[0].CreateSnapshotKeepDuration.ShouldBe( TimeSpan.FromHours( 12 ) + TimeSpan.FromMinutes( 34 ) + TimeSpan.FromSeconds( 56 ) );
    }
}
