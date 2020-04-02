using CK.Core;
using CK.Text;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CK.Observable
{
    /// <summary>
    /// Implements a <see cref="IStreamStore"/> with files in a directory.
    /// </summary>
    public sealed class DirectoryStreamStore : IStreamStore
    {
        readonly string _path;

        /// <summary>
        /// Initializes a new <see cref="DirectoryStreamStore"/> on a directory:
        /// the directory is created if it does not exist.
        /// An <see cref="IOException"/> is throw if the path is an existing file.
        /// </summary>
        /// <param name="path">The local directory path.</param>
        public DirectoryStreamStore( string path )
        {
            _path = Path.GetFullPath( path );
            Directory.CreateDirectory( _path );
            _path = FileUtil.NormalizePathSeparator( _path, true );
        }

        string GetFullPath( ref string name )
        {
            if( String.IsNullOrEmpty( name ) ) throw new ArgumentNullException( nameof( name ) );
            name = name.ToLowerInvariant();
            return _path + name;
        }
        string GetFullWritePath( ref string name )
        {
            var p = GetFullPath( ref name );
            if( FileUtil.IndexOfInvalidFileNameChars( name ) >= 0 ) throw new ArgumentException( "Invalid characters in name.", nameof( name ) );
            return p;
        }

        string GetBackupFolder( string name )
        {
            return _path + name + ".bak";
        }

        Task<bool> IStreamStore.ExistsAsync( string name )
        {
            return Task.FromResult( File.Exists( GetFullPath( ref name ) ) );
        }

        async Task<DateTime> IStreamStore.CreateAsync( string name, Func<Stream, Task> writer )
        {
            var path = GetFullWritePath( ref name );
            try
            {
                using( var output = new FileStream( path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.SequentialScan|FileOptions.Asynchronous ) )
                {
                    await writer( output );
                }
                return File.GetLastWriteTimeUtc( path );
            }
            catch( Exception )
            {
                File.Delete( path );
                throw;
            }
        }

        async Task<DateTime> IStreamStore.UpdateAsync( string name, Func<Stream, Task> writer, bool allowCreate, DateTime checkLastWriteTimeUtc )
        {
            var path = GetFullWritePath( ref name );
            bool exists = File.Exists( path );
            if( !exists && !allowCreate ) throw new ArgumentException( $"'{name}' does not exist in store '{_path}'.", nameof( name ) );
            if( exists )
            {
                if( checkLastWriteTimeUtc != default && checkLastWriteTimeUtc != File.GetLastWriteTimeUtc( path ) )
                {
                    return Util.UtcMaxValue;
                }
            }
            string tempFilePath = Path.GetTempFileName();
            using( var output = File.Open( tempFilePath, FileMode.Create ) )
            {
                await writer( output );
                await output.FlushAsync();
            }
            if( exists )
            {
                string backupPath = GetBackupFolder( name );
                Directory.CreateDirectory( backupPath );
                File.Replace( tempFilePath, path, FileUtil.EnsureUniqueTimedFile( backupPath + Path.DirectorySeparatorChar, String.Empty, DateTime.UtcNow ), true );
            }
            else File.Move( tempFilePath, path );
            return File.GetLastWriteTimeUtc( path );
        }

        Task IStreamStore.DeleteAsync( string name, bool archive )
        {
            var path = GetFullWritePath( ref name );
            if( File.Exists( path ) )
            {
                DoDelete( name, path, archive );
            }
            return Task.CompletedTask;
        }

        void DoDelete( string name, string path, bool archive )
        {
            var backupPath = GetBackupFolder( name );
            if( archive )
            {
                var archivePath = FileUtil.CreateUniqueTimedFolder( _path + "Archive" + Path.DirectorySeparatorChar, "-" + name, DateTime.UtcNow );
                if( Directory.Exists( backupPath ) )
                {
                    Directory.Move( backupPath, Path.Combine( archivePath, Path.GetFileName( backupPath ) ) );
                }
                File.Move( path, Path.Combine( archivePath, name ) );
            }
            else
            {
                if( Directory.Exists( backupPath ) ) Directory.Delete( backupPath, true );
                File.Delete( path );
            }
        }

        Task<Stream?> IStreamStore.OpenReadAsync( string name )
        {
            var path = GetFullPath( ref name );
            Stream? result = File.Exists( path )
                                ? new FileStream( path, FileMode.Open, FileAccess.Read, FileShare.None, 4096, FileOptions.SequentialScan | FileOptions.Asynchronous )
                                : null;
            return Task.FromResult( result );
        }

        Task<int> IStreamStore.DeleteAsync( Func<string, bool> predicate, bool archive )
        {
            int count = 0;
            foreach( var path in Directory.EnumerateFiles( _path, "*", SearchOption.TopDirectoryOnly ) )
            {
                var name = path.Substring( _path.Length );
                if( predicate( name ) )
                {
                    DoDelete( path, name, archive );
                    ++count;
                }
            }
            return Task.FromResult( count );
        }
    }
}
