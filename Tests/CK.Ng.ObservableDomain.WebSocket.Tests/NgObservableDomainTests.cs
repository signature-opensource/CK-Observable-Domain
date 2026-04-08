using System.Linq;
using System.Threading.Tasks;
using CK.Core;
using CK.Monitoring;
using CK.Ng.ObservableDomain.WebSocket.Tests.Drivers;
using CK.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;

using static CK.Testing.MonitorTestHelper;

namespace CK.Ng.ObservableDomain.WebSocket.Tests;

[TestFixture]
public class NgObservableDomainTests
{
    [Test]
    public async Task CK_Ng_Observable_Domain_WebSocket_Async()
    {
        var targetProjectPath = TestHelper.GetTypeScriptInlineTargetProjectPath();

        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Path = TestHelper.BinFolder;
        configuration.FirstBinPath.Assemblies.AddRange( ["CK.Ng.ObservableDomain.WebSocket", "CK.Ng.Cris.AspNet"] );
        configuration.FirstBinPath.Types.Add( typeof( SampleDomainDriver ) );
        configuration.FirstBinPath.Types.Add( typeof( SampleConfigurator ) );
        configuration.FirstBinPath.Types.Add( typeof( SampleAsyncConfigurator ) );

        var tsConfig = configuration.FirstBinPath.EnsureTypeScriptConfigurationAspect( targetProjectPath );

        TestHelper.Monitor.MinimalFilter = LogFilter.Debug;
        using( var collector = GrandOutput.Default!.CreateMemoryCollector( 256 ) )
        {
            var map = (await configuration.RunSuccessfullyAsync()).LoadMap();
            var builder = WebApplication.CreateSlimBuilder();
            builder.AddApplicationIdentityServiceConfiguration();
            await using var server = await builder.CreateRunningAspNetServerAsync( map );

            await collector.UpdateCachedEntriesAsync();

            var configuratorLogs = collector.CachedTexts.Where( l => l.StartsWith( ">>>" ) ).ToList();
            configuratorLogs.Count.ShouldBe( 4 );
            configuratorLogs[0].ShouldContain( "PreConfigure" );
            configuratorLogs[1].ShouldContain( "SampleConfigurator" );
            configuratorLogs[2].ShouldContain( "PostConfigure" );
            configuratorLogs[3].ShouldContain( "SampleAsyncConfigurator" );

            await using var runner = TestHelper.CreateTypeScriptRunner( targetProjectPath, server.ServerAddress );
            await TestHelper.SuspendAsync( resume => resume );
            runner.Run();
        }
    }
}
