using NUnit.Framework;
using System.Threading.Tasks;
using CK.Testing;
using CK.Cris.AmbientValues;
using CK.Cris;
using CK.Setup;
using static CK.Testing.MonitorTestHelper;

namespace CK.TS.ObservableDomain.Tests;

[TestFixture]
public class TSTests
{
    [Test]
    public async Task CK_TS_ObservableDomain_Async()
    {
        var targetProjectPath = TestHelper.GetTypeScriptInlineTargetProjectPath();

        EngineConfiguration configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.EnsureTypeScriptConfigurationAspect( targetProjectPath );
        configuration.FirstBinPath.Assemblies.Add( "CK.TS.ObservableDomain" );
        await configuration.RunSuccessfullyAsync();

        await using var runner = TestHelper.CreateTypeScriptRunner( targetProjectPath );
        runner.Run();
    }

    [Test]
    public async Task CK_Observable_SignalRWatcher_Async()
    {
        var targetProjectPath = TestHelper.GetTypeScriptInlineTargetProjectPath();

        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.EnsureTypeScriptConfigurationAspect( targetProjectPath );
        configuration.FirstBinPath.Assemblies.Add( "CK.Observable.SignalRWatcher" );
        configuration.FirstBinPath.Assemblies.Add( "CK.Cris.AspNet" );
        await configuration.RunSuccessfullyAsync();

        await using var runner = TestHelper.CreateTypeScriptRunner( targetProjectPath );
        runner.Run();
    }

}
