using NUnit.Framework;
using System.Threading.Tasks;
using CK.Testing;
using static CK.Testing.StObjEngineTestHelper;
using CK.Cris.AmbientValues;
using CK.Cris;
using CK.Cris.AspNet;

namespace CK.TS.ObservableDomain.Tests
{
    [TestFixture]
    public class TSTests
    {
        [Test]
        public async Task CK_TS_ObservableDomain_Async()
        {
            var targetProjectPath = TestHelper.GetTypeScriptBuildModeTargetProjectPath();
            TestHelper.RunSuccessfulEngineWithTypeScript( targetProjectPath, typeof( CK.ObservableDomain.TSPackage ).Assembly );
            await using var runner = TestHelper.CreateTypeScriptRunner( targetProjectPath );
            runner.Run();
        }

        [Test]
        public async Task CK_Observable_MQTTWatcher_Async()
        {
            var targetProjectPath = TestHelper.GetTypeScriptBuildModeTargetProjectPath();

            var c = TestHelper.CreateTypeCollector()
                              .AddModelDependentAssembly( typeof( CK.Observable.MQTTWatcher.TSPackage ).Assembly )
                              .AddModelDependentAssembly( typeof( CrisAspNetService ).Assembly );    
            TestHelper.RunSuccessfulEngineWithTypeScript( targetProjectPath, c );
            await using var runner = TestHelper.CreateTypeScriptRunner( targetProjectPath );
            runner.Run();
        }

    }
}
