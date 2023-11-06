using CK.Core;
using CK.Observable.League.Tests.Model;
using FluentAssertions;
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

namespace CK.Observable.League.Tests
{
    [TestFixture]
    public class ApplyEnsureDomainOptionsTests
    {
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
            AppIdentity.ApplicationIdentityService? identityService = null;
            if( mode == "WithAppIdentityService" )
            {
                // For CK.AppIdentity > v0.1.2
                // var identityServiceConfig = AppIdentity.ApplicationIdentityServiceConfiguration.CreateEmpty();
                // identityService = new AppIdentity.ApplicationIdentityService( identityServiceConfig, emptyServices );

                // Currently:
                var identityServiceConfig = AppIdentity.ApplicationIdentityServiceConfiguration.Create( TestHelper.Monitor,
                                                                                                        c => c["FullName"] = "Test/$Test" );
                Debug.Assert( identityServiceConfig != null );
                emptyServices.Add( typeof( IEnumerable<AppIdentity.IApplicationIdentityFeatureDriver> ), new AppIdentity.IApplicationIdentityFeatureDriver[] { } );
                identityService = new AppIdentity.ApplicationIdentityService( identityServiceConfig, emptyServices );
            }
            var def = new DefaultObservableLeague( emptyServices, Options.Create( options ), identityService );
            await ((IHostedService)def).StartAsync( default );
            // Starts ApplicationIdentityService after.
            if( identityService != null )
            {
                await ((IHostedService)identityService).StartAsync( default );
            }

            def.Coordinator.Read( TestHelper.Monitor, ( monitor, d ) =>
            {
                var d1 = d.Root.Domains["WithNoRootTypesAndNoDefaultOptions"];
                d1.RootTypes.Should().BeEmpty();
                d1.Options.LifeCycleOption.Should().Be( DomainLifeCycleOption.Always );
                d1.Options.CompressionKind.Should().Be( CompressionKind.GZiped );
                d1.Options.SkipTransactionCount.Should().Be( 3712 );
                d1.Options.SnapshotSaveDelay.Should().Be( TimeSpan.FromSeconds( 3713 ) );
                d1.Options.SnapshotKeepDuration.Should().Be( TimeSpan.FromDays( 3714 ) );
                d1.Options.SnapshotMaximalTotalKiB.Should().Be( 3715 );
                d1.Options.HousekeepingRate.Should().Be( 3716 );
                d1.Options.ExportedEventKeepDuration.Should().Be( TimeSpan.FromMinutes( 3717 ) );
                d1.Options.ExportedEventKeepLimit.Should().Be( 3718 );

                var d2 = d.Root.Domains["WithOneRootType"];
                d2.RootTypes.Should().HaveCount( 1 );
                d2.RootTypes[0].Should().StartWith( "CK.Observable.League.Tests.Model.School, " );
            } );

            var loader1 = def.Find( "WithNoRootTypesAndNoDefaultOptions" );
            Debug.Assert( loader1 != null );
            loader1.RootTypes.Should().BeEmpty();

            await using var d1 = await loader1.LoadAsync( TestHelper.Monitor );
            Debug.Assert( d1 != null );

            d1.Read( TestHelper.Monitor, ( monitor, d ) =>
            {
                d.AllRoots.Should().HaveCount( 0 );
            } );

            var loader2 = def.Find( "WithOneRootType" );
            Debug.Assert( loader2 != null );

            loader2.RootTypes.Should().BeEquivalentTo( new[] { typeof( School ) } );
            await using var d2 = await loader2.LoadAsync<School>( TestHelper.Monitor );
            Debug.Assert( d2 != null );
            d2.Read( TestHelper.Monitor, ( monitor, d ) =>
            {
                d.AllRoots.Should().HaveCount( 1 );
                d.Root.Persons.Should().BeEmpty();
            } );
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
            league.Coordinator.Read( TestHelper.Monitor, ( monitor, d ) => d.Root.Domains.Count ).Should().Be( 0 );

            await league.ApplyEnsureDomainOptionsAsync( TestHelper.Monitor, options.EnsureDomains );
            league.Coordinator.Read( TestHelper.Monitor, ( monitor, d ) => d.Root.Domains.Count ).Should().Be( 3 );

            using( TestHelper.Monitor.CollectEntries( out var entries, LogLevelFilter.Trace ) )
            {
                await league.ApplyEnsureDomainOptionsAsync( TestHelper.Monitor, options.EnsureDomains );
                entries.Select( e => e.Text ).Should()
                    .Contain( "Domain 'NoRoot' found. Root types '' match. Everything is fine." )
                    .And.Contain( "Domain 'OneRootType' found. Root types 'CK.Observable.League.Tests.Model.School' match. Everything is fine." )
                    .And.Contain( "Domain 'TwoRootType' found. Root types 'CK.Observable.League.Tests.Model.School', 'CK.Observable.League.Tests.MicroMachine.Root' match. Everything is fine." );
            }
        }

        [Test]
        public void DefaultObservableLeagueOptions_can_be_read_from_Configuration()
        {
            IConfigurationBuilder builder = new ConfigurationBuilder().AddInMemoryCollection( new Dictionary<string,string>
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
            o.StorePath.Should().Be( "Some/Path" );
            o.EnsureDomains.Should().HaveCount( 1 );
            o.EnsureDomains[0].DomainName.Should().Be( "FirstDomain" );
            o.EnsureDomains[0].RootTypes.Should().NotBeEmpty().And.Contain( "a root type" ).And.Contain( "another root type" );
            o.EnsureDomains[0].CreateLifeCycleOption.Should().Be( DomainLifeCycleOption.Never );
            o.EnsureDomains[0].CreateSnapshotKeepDuration.Should().Be( TimeSpan.FromHours( 12 ) + TimeSpan.FromMinutes( 34 ) + TimeSpan.FromSeconds( 56 ) );
        }
    }
}
