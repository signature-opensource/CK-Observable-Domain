using CK.Core;
using CK.Monitoring;
using CK.Setup;
using System;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;

namespace CK.Setup
{
    public sealed class CKomposableAppBuilder
    {
        readonly NormalizedPath _parentFolderPath;
        readonly NormalizedPath _rootLogPath;
        readonly IActivityMonitor _monitor;
        readonly NormalizedPath _msBuildOutputPath;
        readonly NormalizedPath _builderFolderPath;
        readonly EngineConfiguration _engineConfiguration;
        string _applicationName;

        public string ApplicationName
        {
            get => _applicationName;
            set => _applicationName = value ?? String.Empty;
        }

        public IActivityMonitor Monitor => _monitor;

        public NormalizedPath MsBuildOutputPath => _msBuildOutputPath;

        public NormalizedPath ParentFolderPath => _parentFolderPath;

        public NormalizedPath BuilderFolderPath => _builderFolderPath;

        public NormalizedPath GetAppFolderPath( string? appName = null ) => _parentFolderPath.AppendPart( appName ?? ApplicationName + ".App" );

        public NormalizedPath GetHostFolderPath( string? appName = null ) => _parentFolderPath.AppendPart( appName ?? ApplicationName + ".Host" );

        public NormalizedPath GetAppBinPath( string? appName = null ) => GetAppFolderPath( appName ).Combine( _msBuildOutputPath );

        public EngineConfiguration EngineConfiguration => _engineConfiguration;

        public static int Run( Action<IActivityMonitor,CKomposableAppBuilder> configure, [CallerFilePath] string? programFilePath = null )
        {
            var builderFolder = new NormalizedPath( programFilePath ).RemoveLastPart();
            var rootLogPath = builderFolder.AppendPart( "Logs" );
            LogFile.RootLogPath = rootLogPath;
            GrandOutput.EnsureActiveDefault();
            var monitor = new ActivityMonitor();
            try
            {
                var parentPath = builderFolder.RemoveLastPart();
                var msBuildOutputPath = new NormalizedPath( AppContext.BaseDirectory ).RemovePrefix( builderFolder );
                var applicationName = builderFolder.LastPart;
                if( applicationName.EndsWith( ".Builder" ) ) applicationName = applicationName.Substring( 0, applicationName.Length - 8 );
                var b = new CKomposableAppBuilder( parentPath, rootLogPath, monitor, msBuildOutputPath, applicationName, builderFolder );
                configure( monitor, b );
                var engineResult = b.EngineConfiguration.Run( monitor );
                return engineResult != null && engineResult.Status != RunStatus.Failed ? 0 : 1;
            }
            catch( Exception ex )
            {
                monitor.Error( ex );
                return -1;
            }
            finally
            {
                monitor.MonitorEnd();
                GrandOutput.Default?.Dispose();
                NormalizedPath lastLog = Directory.EnumerateFiles( rootLogPath.AppendPart( "Text" ) ).OrderBy( f => File.GetLastWriteTimeUtc( f ) ).LastOrDefault();
                if( !lastLog.IsEmptyPath )
                {
                    NormalizedPath lastRun = lastLog.Combine( "../../../LastRun.log" );
                    File.Copy( lastLog, lastRun );
                }
            }
        }

        CKomposableAppBuilder( NormalizedPath parentPath,
                               NormalizedPath rootLogPath,
                               IActivityMonitor monitor,
                               NormalizedPath msBuildOutputPath,
                               string applicationName,
                               NormalizedPath builderFolderPath )
        {
            _parentFolderPath = parentPath;
            _rootLogPath = rootLogPath;
            _monitor = monitor;
            _msBuildOutputPath = msBuildOutputPath;
            _applicationName = applicationName;
            _builderFolderPath = builderFolderPath;
            _engineConfiguration = new EngineConfiguration();
            _engineConfiguration.FirstBinPath.Path = GetAppBinPath();
            _engineConfiguration.FirstBinPath.ProjectPath = GetHostFolderPath();
        }

        // Should be an extension method on a CKomposableAppBuilder.
        // provided by CK.TypeScriptEngine....
        public TypeScriptBinPathAspectConfiguration EnsureDefaultTypeScriptAspectConfiguration( string binPathName = "First" )
        {
            var binPath = _engineConfiguration.FindRequiredBinPath( binPathName );
            var tsAspect = _engineConfiguration.EnsureAspect<TypeScriptAspectConfiguration>();
            var tsBinPathAspect = _engineConfiguration.FirstBinPath.EnsureAspect<TypeScriptBinPathAspectConfiguration>();
            tsBinPathAspect.AutoInstallVSCodeSupport = true;
            tsBinPathAspect.AutoInstallYarn = true;
            tsBinPathAspect.GitIgnoreCKGenFolder = true;
            tsBinPathAspect.TargetProjectPath = GetHostFolderPath().AppendPart( "Client" );
            return tsBinPathAspect;
        }

    }

}
