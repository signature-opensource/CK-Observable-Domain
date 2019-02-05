using CodeCake;
using SimpleGitVersion;
using System;
using System.Collections.Generic;
using System.IO;

namespace CodeCakeBuilder
{
    public class PackageVersionReplacer : IDisposable
    {
        public static readonly string DefaultVersionFileToken = "0.0.0-version-replaced-in-CodeCakeBuilder";

        private readonly Dictionary<string, TemporaryFile> originalToCopyPaths = new Dictionary<string, TemporaryFile>();

        public PackageVersionReplacer( SimpleRepositoryInfo gitInfo, string tokenInFile, IEnumerable<string> filesToProcess )
        {
            if( gitInfo == null )
            {
                throw new ArgumentNullException( nameof( gitInfo ) );
            }
            if( filesToProcess == null )
            {
                throw new ArgumentNullException( nameof( filesToProcess ) );
            }
            if( string.IsNullOrEmpty( tokenInFile ) )
            {
                throw new ArgumentNullException( nameof( tokenInFile ) );
            }

            if( gitInfo.IsValid )
            {
                foreach( string filePath in filesToProcess )
                {
                    if( filePath == null ) throw new ArgumentException( $"{filesToProcess} contains a null string", nameof( filesToProcess ) );

                    // Copy file to temp. file
                    TemporaryFile tf = new TemporaryFile( true, Path.GetExtension( filePath ).Trim( '.' ) );
                    originalToCopyPaths.Add( filePath, tf );
                    File.Copy( filePath, tf.Path, true );

                    // Replace token by SafeSemVersion
                    string fileContents = File.ReadAllText( filePath );
                    fileContents = fileContents.Replace( tokenInFile, gitInfo.SafeSemVersion );
                    File.WriteAllText( filePath, fileContents );
                }
            }
        }

        public PackageVersionReplacer( SimpleRepositoryInfo gitInfo, string tokenInFile,
            params string[] filesToProcess ) : this( gitInfo, tokenInFile, (IEnumerable<string>)filesToProcess )
        {

        }

        public void Dispose()
        {
            foreach( var kvp in originalToCopyPaths )
            {
                File.Copy( kvp.Value.Path, kvp.Key, true );
                kvp.Value.Dispose();
            }
        }
    }
}
