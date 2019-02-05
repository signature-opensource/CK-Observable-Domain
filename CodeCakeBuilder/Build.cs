using Cake.Common.Diagnostics;
using Cake.Common.IO;
using Cake.Common.Solution;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Npm;
using Cake.Npm.Publish;
using CK.Text;
using CodeCakeBuilder;
using SimpleGitVersion;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace CodeCake
{

    [AddPath( "%UserProfile%/.nuget/packages/**/tools*" )]
    public partial class Build : CodeCakeHost
    {
        public Build()
        {
            Cake.Log.Verbosity = Verbosity.Diagnostic;

            string solutionFilePath = Cake.GetFiles( "*.sln" ).Single().FullPath;

            var releasesDir = Cake.Directory( "CodeCakeBuilder/Releases" );
            var projects = Cake.ParseSolution( solutionFilePath )
                                       .Projects
                                       .Where( p => !(p is SolutionFolder)
                                                    && p.Name != "CodeCakeBuilder" );

            // We do not generate NuGet packages for /Tests projects for this solution.
            var projectsToPublish = projects
                                        .Where( p => !p.Path.Segments.Contains( "Tests" ) );

            SimpleRepositoryInfo gitInfo = Cake.GetSimpleRepositoryInfo();

            // Configuration is either "Debug" or "Release".
            string configuration = "Debug";

            Task( "Check-Repository" )
                .Does( () =>
                 {
                     configuration = StandardCheckRepository( projectsToPublish, gitInfo );
                 } );

            Task( "Clean" )
                .IsDependentOn( "Check-Repository" )
                .Does( () =>
                 {
                     Cake.CleanDirectories( projects.Select( p => p.Path.GetDirectory().Combine( "bin" ) ) );
                     Cake.CleanDirectories( projects.Select( p => p.Path.GetDirectory().Combine( "obj" ) ) );
                     Cake.CleanDirectories( releasesDir );
                     Cake.DeleteFiles( "Tests/**/TestResult*.xml" );
                 } );

            Task( "Build" )
                .IsDependentOn( "Check-Repository" )
                .IsDependentOn( "Clean" )
                .Does( () =>
                 {
                     StandardSolutionBuild( solutionFilePath, gitInfo, configuration );
                 } );

            Task( "Unit-Testing" )
                .IsDependentOn( "Build" )
                .WithCriteria( () => Cake.InteractiveMode() == InteractiveMode.NoInteraction
                                     || Cake.ReadInteractiveOption( "RunUnitTests", "Run Unit Tests?", 'Y', 'N' ) == 'Y' )
                .Does( () =>
                 {
                     var testProjects = projects.Where( p => p.Name.EndsWith( ".Tests" ) );
                     StandardUnitTests( configuration, testProjects );
                 } );


            Task( "Create-NuGet-Packages" )
                .WithCriteria( () => gitInfo.IsValid )
                .IsDependentOn( "Unit-Testing" )
                .Does( () =>
                 {
                     StandardCreateNuGetPackages( releasesDir, projectsToPublish, gitInfo, configuration );
                 } );

            Task( "Push-NuGet-Packages" )
                .IsDependentOn( "Create-NuGet-Packages" )
                .WithCriteria( () => gitInfo.IsValid )
                .Does( () =>
                 {
                     SignaturePushNuGetPackages( Cake.GetFiles( releasesDir.Path + "/*.nupkg" ), gitInfo );
                 } );

            Task( "Npm-Process" )
                .IsDependentOn( "Check-Repository" )
                .IsDependentOn( "Clean" )
                .Does( () =>
                {

                    // Provisional version management for NPM packages!
                    using( var replacer = new PackageVersionReplacer(
                        gitInfo,
                        "0.0.0-version-replaced-in-CodeCakeBuilder",
                        Cake.File( "js/package.json" ).Path.FullPath,
                        Cake.File( "js/package-lock.json" ).Path.FullPath
                    ) )
                    {
                        Cake.NpmInstall(
                            s => s
                                .WithLogLevel( NpmLogLevel.Warn )
                                .FromPath( Cake.Directory( "js" ) )
                            );

                        Cake.NpmRunScript(
                            "test",
                            s => s
                                .WithLogLevel( NpmLogLevel.Warn )
                                .FromPath( Cake.Directory( "js" ) )
                        );

                        Cake.NpmRunScript(
                            "build",
                            s => s
                                .WithLogLevel( NpmLogLevel.Warn )
                                .FromPath( Cake.Directory( "js" ) )
                        );

                        if( gitInfo.IsValid )
                        {
                            string packageJsonContents = File.ReadAllText( Cake.File( "js/package.json" ) );
                            StringMatcher sm = new StringMatcher( packageJsonContents, 0 );

                            Trace.Assert( sm.MatchJSONObject( out object o ) );

                            var l = (List<KeyValuePair<string, object>>)o;

                            string packageName = (string)l.First( kvp => kvp.Key == "name" ).Value;
                            string packageVersion = (string)l.First( kvp => kvp.Key == "version" ).Value;


                            string tag;
                            bool makeLatest = false;
                            if( gitInfo.IsValidRelease )
                            {
                                if( gitInfo.PreReleaseName.Length == 0 )
                                {
                                    // 1.0.0
                                    tag = "stable";
                                    makeLatest = true;
                                }
                                else if(
                                    gitInfo.PreReleaseName == "prerelease"
                                    || gitInfo.PreReleaseName == "rc" )
                                {
                                    // 1.0.0-prerelease
                                    // 1.0.0-rc
                                    tag = "latest"; // This ensures NPM gives this package by default
                                }
                                else
                                {
                                    // 1.0.0-alpha
                                    // 1.0.0-beta
                                    // etc.
                                    tag = "preview";
                                }
                            }
                            else
                            {
                                // CI build
                                tag = "ci";
                            }

                            if( Cake.InteractiveMode() != InteractiveMode.Interactive
                                || Cake.ReadInteractiveOption( "PublishNpmPackages", $"Publish \"{packageName}@{packageVersion}\" to NPM repository with the NPM tag \"{tag}\"?", 'Y', 'N' ) == 'Y' )
                            {
                                Cake.Information( "Publishing NPM package..." );

                                Cake.NpmPublish(
                                    s => s
                                        .WithTag( tag )
                                        .FromPath( Cake.Directory( "js" ) )
                                );

                                if( makeLatest )
                                {
                                    Cake.NpmDistTagAdd( packageName, packageVersion, "latest" );
                                }
                            }
                        }
                        else
                        {
                            Cake.Warning( "Skipping npm publish: No valid version" );
                        }
                    }
                } );


            // The Default task for this script can be set here.
            Task( "Default" )
                .IsDependentOn( "Npm-Process" )
                .IsDependentOn( "Push-NuGet-Packages" );
        }

    }
}
