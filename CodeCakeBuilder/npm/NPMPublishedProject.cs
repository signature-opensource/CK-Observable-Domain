using Cake.Common.IO;
using Cake.Core;
using Cake.Core.Tooling;
using Cake.Npm;
using Cake.Npm.Pack;
using Cake.Yarn;
using CK.Core;
using CodeCake.Abstractions;
using Newtonsoft.Json.Linq;
using System;
using System.IO;

namespace CodeCake
{
    public class NPMPublishedProject : NPMProject, ILocalArtifact
    {
        readonly bool _ckliLocalFeedMode;

        NPMPublishedProject( NPMSolution npmSolution, SimplePackageJsonFile json, NormalizedPath outputPath )
            : base( npmSolution, json, outputPath )
        {
            _ckliLocalFeedMode = json.CKliLocalFeedMode;
            ArtifactInstance = new ArtifactInstance( new Artifact( "NPM", json.Name ), GlobalInfo.BuildInfo.Version );
            string tgz = json.Name.Replace( "@", "" ).Replace( '/', '-' );
            TGZName = tgz + "-" + GlobalInfo.BuildInfo.Version.ToNormalizedString() + ".tgz";
        }

        /// <summary>
        /// Create a <see cref="NPMProject"/> that can be a <see cref="NPMPublishedProject"/>.
        /// </summary>
        /// <param name="solution">The NPM solution that contains the project.</param>
        /// <param name="dirPath">The directory path where is located the npm package.</param>
        /// <param name="outputPath">The directory path where the build output is. It can be the same than <paramref name="dirPath"/>.</param>
        /// <returns></returns>
        public static NPMProject Create( NPMSolution solution,
                                         NormalizedPath dirPath,
                                         NormalizedPath outputPath )
        {
            var json = SimplePackageJsonFile.Create( solution.GlobalInfo.Cake, dirPath );
            NPMProject output;
            if( json.IsPrivate )
            {
                output = CreateNPMProject( solution, json, outputPath );
            }
            else
            {
                output = new NPMPublishedProject( solution, json, outputPath );
            }
            return output;
        }

        public override bool IsPublished => true;

        public ArtifactInstance ArtifactInstance { get; }

        public string Name => ArtifactInstance.Artifact.Name;

        public string TGZName { get; }

        private protected override void DoRunScript( string scriptName, bool runInBuildDirectory )
        {
            using( TemporarySetPackageVersion( ArtifactInstance.Version ) )
            {
                base.DoRunScript( scriptName, runInBuildDirectory );
            }
        }

        /// <summary>
        /// Generates the .tgz file in the <see cref="StandardGlobalInfo.ReleasesFolder"/>
        /// by calling npm pack.
        /// </summary>
        /// <param name="globalInfo">The global information object.</param>
        /// <param name="cleanupPackageJson">
        /// By default, "scripts" and "devDependencies" are removed from the package.json file.
        /// </param>
        /// <param name="packageJsonPreProcessor">Optional package.json pre processor.</param>
        public void RunPack( Action<JObject> packageJsonPreProcessor = null )
        {
            var tgz = OutputPath.AppendPart( TGZName );
            using( TemporarySetPackageVersion( ArtifactInstance.Version, true ) )
            {
                using( TemporaryPrePack( ArtifactInstance.Version, packageJsonPreProcessor, false ) )
                {
                    if( NpmSolution.UseYarn )
                    {
                        GlobalInfo.Cake.Yarn().Pack( s =>
                        {
                            s.ArgumentCustomization = args => args.Append( "-o " + TGZName );
                            s.WorkingDirectory = OutputPath.ToString();
                        } );
                    }
                    else
                    {
                        GlobalInfo.Cake.NpmPack( new NpmPackSettings()
                        {
                            LogLevel = NpmLogLevel.Info,
                            WorkingDirectory = OutputPath.ToString()
                        } );
                    }
                }

                if(_ckliLocalFeedMode)
                {
                    //It meant that we just build a "dirty" package: we need to build the one that will actually get published.
                    File.Move( tgz, tgz.Path + ".local" );
                    using( TemporaryPrePack( ArtifactInstance.Version, packageJsonPreProcessor, true ) )
                    {
                        if( NpmSolution.UseYarn )
                        {
                            GlobalInfo.Cake.Yarn().Pack( s =>
                            {
                                s.ArgumentCustomization = args => args.Append( "-o " + tgz );
                                s.WorkingDirectory = OutputPath.ToString();
                            } );
                        }
                        else
                        {
                            GlobalInfo.Cake.NpmPack( new NpmPackSettings()
                            {
                                LogLevel = NpmLogLevel.Info,
                                WorkingDirectory = OutputPath.ToString()
                            } );
                        }
                    }
                }
            }
            if( !File.Exists( tgz ) )
            {
                GlobalInfo.Cake.TerminateWithError( $"Package file '{tgz}' has not been generated by 'npm pack'." );
            }
            var target = GlobalInfo.ReleasesFolder.AppendPart( TGZName );
            GlobalInfo.Cake.CopyFile( tgz.Path, target.Path );
            GlobalInfo.Cake.DeleteFile( tgz.Path );
        }
    }
}
