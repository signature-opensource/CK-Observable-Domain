using CK.Core;
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

        string GetFullPath( string fullName )
        {
            if( String.IsNullOrEmpty( fullName ) ) throw new ArgumentNullException( nameof( fullName ) );
            return _path + fullName.ToLowerInvariant();
        }

        Task<bool> IStreamStore.ExistsAsync( string fullName )
        {
            return Task.FromResult( File.Exists( GetFullPath( fullName ) ) );
        }

        async Task<DateTime> IStreamStore.CreateAsync( string fullName, Func<Stream, Task> writer )
        {
            fullName = GetFullPath( fullName );
            try
            {
                using( var output = new FileStream( fullName, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.SequentialScan|FileOptions.Asynchronous ) )
                {
                    await writer( output );
                }
                return File.GetLastWriteTimeUtc( fullName );
            }
            catch( Exception )
            {
                File.Delete( fullName );
                throw;
            }
        }

        async Task<DateTime> IStreamStore.UpdateAsync( string fullName, Func<Stream, Task> writer, bool allowCreate, DateTime checkLastWriteTimeUtc )
        {
            fullName = GetFullPath( fullName );
            bool exists = File.Exists( fullName );
            if( !exists && !allowCreate ) throw new ArgumentException( $"'{fullName}' does not exist.", nameof( fullName ) );
            if( exists )
            {
                if( checkLastWriteTimeUtc != default && checkLastWriteTimeUtc != File.GetLastWriteTimeUtc( fullName ) )
                {
                    return Util.UtcMaxValue;
                }
            }
            using( var output = new FileStream( fullName, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.SequentialScan|FileOptions.Asynchronous ) )
            {
                await writer( output );
            }
            return File.GetLastWriteTimeUtc( fullName );
        }

        Task IStreamStore.DeleteAsync( string fullName )
        {
            fullName = GetFullPath( fullName );
            if( File.Exists( fullName ) ) File.Delete( fullName );
            return Task.CompletedTask;
        }

        Task<Stream?> IStreamStore.OpenReadAsync( string fullName )
        {
            fullName = GetFullPath( fullName );
            Stream? result = File.Exists( fullName )
                                ? new FileStream( fullName, FileMode.Open, FileAccess.Read, FileShare.None, 4096, FileOptions.SequentialScan | FileOptions.Asynchronous )
                                : null;
            return Task.FromResult( result );
        }

        Task<int> IStreamStore.DeleteAsync( Func<string, bool> predicate )
        {
            int count = 0;
            foreach( var e in Directory.EnumerateFiles( _path, "*", SearchOption.AllDirectories ) )
            {
                if( predicate( e.Substring( _path.Length ) ) )
                {
                    File.Delete( e );
                    ++count;
                }
            }
            return Task.FromResult( count );
        }
    }
}
