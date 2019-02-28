using Cake.Common.Diagnostics;
using Cake.Common.IO;
using Cake.Common.IO.Paths;
using Cake.Npm;
using Cake.Npm.Pack;
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
    public partial class Build
    {
        /// <summary>
        /// Builds a NPM package using conventional methods. See remarks!
        /// </summary>
        /// <param name="gitInfo"></param>
        /// <param name="packageDir"></param>
        /// <remarks>
        /// The following process is executed inside the <paramref name="packageDir"/>:
        /// 1. `npm install`
        /// 2. `npm run clean`
        /// 3. `npm run test`
        /// 4. `npm run build`
        /// 5. `npm publish`
        /// With the following constraints:
        /// - `package.json` must have a `clean` script
        /// - `package.json` must have a `build` script that drops artifacts in its own `dist` directory
        /// - `package.json` must have a `test` script that returns 0 (if you don't have tests, just put `echo 'No tests'` or whatever)
        /// - `package.json` must have the following version: `0.0.0-version-replaced-in-CodeCakeBuilder`
        /// </remarks>
        void CustomNpmBuild( SimpleRepositoryInfo gitInfo, ConvertableDirectoryPath packageDir, ConvertableDirectoryPath releasesDir )
        {
            var packageJsonPath = packageDir.Path.CombineWithFilePath( "package.json" ).FullPath;
            var packageLockJsonPath = packageDir.Path.CombineWithFilePath( "package-lock.json" ).FullPath;
            using( var replacer = new PackageVersionReplacer(
                gitInfo,
                "0.0.0-version-replaced-in-CodeCakeBuilder",
                packageJsonPath,
                packageLockJsonPath
            ) )
            {
                // npm install
                Cake.NpmInstall(
                    s => s
                        .WithLogLevel( NpmLogLevel.Warn )
                        .FromPath( packageDir )
                    );

                // npm run clean
                Cake.NpmRunScript(
                    "clean",
                    s => s
                        .WithLogLevel( NpmLogLevel.Info )
                        .FromPath( packageDir )
                );

                // npm run build
                Cake.NpmRunScript(
                    "build",
                    s => s
                        .WithLogLevel( NpmLogLevel.Info )
                        .FromPath( packageDir )
                );

                // npm run test
                Cake.NpmRunScript(
                    "test",
                    s => s
                        .WithLogLevel( NpmLogLevel.Info )
                        .FromPath( packageDir )
                );

                // Read package name and version from (modified) package.json
                string packageJsonContents = File.ReadAllText( packageJsonPath );
                StringMatcher sm = new StringMatcher( packageJsonContents, 0 );
                Trace.Assert( sm.MatchJSONObject( out object o ) );
                var l = (List<KeyValuePair<string, object>>)o;
                string packageName = (string)l.First( kvp => kvp.Key == "name" ).Value;
                string packageVersion = (string)l.First( kvp => kvp.Key == "version" ).Value;

                string tgzName = $"{packageName}-{packageVersion}.tgz";
                Cake.Information( $"Packing TGZ..." );
                // Pack package up and put that in CCB/Releases.
                var packSettings = new NpmPackSettings();
                packSettings.FromPath( packageDir );
                packSettings.RedirectStandardOutput = true;
                packSettings.RedirectStandardError = true;
                var packFile = Cake.NpmPack( packSettings ).Single();

                Cake.EnsureDirectoryExists( releasesDir );
                Cake.MoveFile( packFile, releasesDir.Path.CombineWithFilePath( packFile.GetFilename() ) );

                if( gitInfo.IsValid )
                {
                    List<string> tags = new List<string>();
                    if( gitInfo.IsValidRelease )
                    {
                        if( gitInfo.PreReleaseName.Length == 0 )
                        {
                            // 1.0.0
                            // Goes to everything and overrides all tags
                            tags.Add( "stable" );
                            tags.Add( "latest" ); // By default, npm install <pkg> (without any @<version> or @<tag> specifier) installs the latest tag.
                            tags.Add( "preview" );
                            tags.Add( "next" );
                            tags.Add( "ci" );
                        }
                        else if(
                            gitInfo.PreReleaseName == "prerelease"
                            || gitInfo.PreReleaseName == "rc" )
                        {
                            // 1.0.0-prerelease
                            // 1.0.0-rc
                            // Goes to everything except "stable"
                            tags.Add( "latest" ); // By default, npm install <pkg> (without any @<version> or @<tag> specifier) installs the latest tag.
                            tags.Add( "preview" );
                            tags.Add( "next" );
                            tags.Add( "ci" );
                        }
                        else
                        {
                            // 1.0.0-alpha
                            // 1.0.0-beta
                            // etc.
                            // Goes to "preview"+"next" and "ci"
                            tags.Add( "preview" );
                            tags.Add( "next" );
                            tags.Add( "ci" );
                        }
                    }
                    else
                    {
                        // CI build
                        tags.Add( "ci" );
                    }

                    string tagList = string.Join( ", ", tags.Select( t => $"\"{t}\"" ) );

                    if( Cake.InteractiveMode() != InteractiveMode.Interactive
                        || Cake.ReadInteractiveOption( "PublishNpmPackages", $"Publish \"{packageName}@{packageVersion}\" to NPM repository with tags {tagList}?", 'Y', 'N' ) == 'Y' )
                    {
                        Cake.Information( $"Publishing NPM package \"{packageName}@{packageVersion}\" with tag \"{tags.First()}\"..." );

                        // Publish first tag with npm publish
                        Cake.NpmPublish(
                            s => s
                                .WithTag( tags.First() )
                                .FromPath( packageDir )
                        );

                        // Publish all remaining tags with npm dist-tag
                        foreach( var tag in tags.Skip( 1 ) )
                        {
                            Cake.Information( $"Adding tag \"{tag}\" to \"{packageName}@{packageVersion}\"..." );
                            // The FromPath is actually required - if executed outside the relevant directory,
                            // it will miss the .npmrc with registry configs.
                            Cake.NpmDistTagAdd( packageName, packageVersion, tag, s => s.FromPath( packageDir ) );
                        }
                    }
                }
                else
                {
                    Cake.Warning( "Skipping npm publish: Version isn't valid." );
                }
            }
        }

    }
}
