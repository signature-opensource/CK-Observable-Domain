using NUnit.Framework;
using System.Threading.Tasks;
using CK.Testing;
using static CK.Testing.StObjEngineTestHelper;
using CK.Cris.AmbientValues;
using CK.Cris;
using CK.Cris.AspNet;
using CK.Setup;

namespace CK.TS.ObservableDomain.Tests
{
    [TestFixture]
    public class TSTests
    {
        [Test]
        public async Task CK_TS_ObservableDomain_Async()
        {
            var targetProjectPath = TestHelper.GetTypeScriptBuildModeTargetProjectPath();
            EngineConfiguration configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.EnsureTypeScriptConfigurationAspect( targetProjectPath );
            configuration.FirstBinPath.Assemblies.Add( "CK.TS.ObservableDomain" );
            configuration.RunSuccessfully();
            await using var runner = TestHelper.CreateTypeScriptRunner( targetProjectPath );
            runner.Run();
        }

        [Test]
        public async Task CK_Observable_MQTTWatcher_Async()
        {
            var targetProjectPath = TestHelper.GetTypeScriptBuildModeTargetProjectPath();
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.EnsureTypeScriptConfigurationAspect( targetProjectPath );
            configuration.FirstBinPath.Assemblies.Add( "CK.Observable.MQTTWatcher" );
            configuration.FirstBinPath.Assemblies.Add( "CK.Cris.AspNet" );
            configuration.RunSuccessfully();
            await using var runner = TestHelper.CreateTypeScriptRunner( targetProjectPath );
            runner.Run();
        }

        [Test]
        public async Task CK_Observable_SignalRWatcher_Async()
        {
            var targetProjectPath = TestHelper.GetTypeScriptBuildModeTargetProjectPath();
            var configuration = TestHelper.CreateDefaultEngineConfiguration();
            configuration.FirstBinPath.EnsureTypeScriptConfigurationAspect( targetProjectPath );
            configuration.FirstBinPath.Assemblies.Add( "CK.Observable.SignalRWatcher" );
            configuration.FirstBinPath.Assemblies.Add( "CK.Cris.AspNet" );
            configuration.RunSuccessfully();
            await using var runner = TestHelper.CreateTypeScriptRunner( targetProjectPath );
            runner.Run();
        }

    }
}
