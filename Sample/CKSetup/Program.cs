using CK.Core;
using CK.Monitoring;
using CK.Setup;
using System;
using System.Diagnostics;

namespace CKSetup
{
    partial class Program
    {
        static int Main( string[] args )
        { 
            LogFile.RootLogPath = Environment.CurrentDirectory;
            GrandOutput.EnsureActiveDefault();
            var monitor = new ActivityMonitor();
            try
            {
                var appBuilder = new CKomposableAppBuilder( args );
                var configuration = new EngineConfiguration();
                configuration.FirstBinPath.Path = appBuilder.GetAppBinPath( "CK.Observable.ServerSample.App" );
                configuration.FirstBinPath.OutputPath = appBuilder.GetOutputPath( "CK.Observable.ServerSample.Host" );

                var tsAspect = configuration.EnsureAspect<TypeScriptAspectConfiguration>();
                var tsBinPathAspect = configuration.FirstBinPath.EnsureAspect<TypeScriptBinPathAspectConfiguration>();
                tsBinPathAspect.AutoInstallVSCodeSupport = true;
                tsBinPathAspect.AutoInstallYarn = true;
                tsBinPathAspect.EnsureTestSupport = true;
                tsBinPathAspect.GitIgnoreCKGenFolder = true;
                tsBinPathAspect.TargetProjectPath = "{OutputPath}/Client";

                var engineResult = configuration.Run( monitor );

                return engineResult != null && engineResult.Status != RunStatus.Failed ? 0 : 1;
            }
            catch( Exception ex )
            {
                monitor.Error( ex );
            }
            finally
            {
                GrandOutput.Default?.Dispose();
            }
        }

    }
}
