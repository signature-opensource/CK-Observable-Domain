using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    /// <summary>
    /// Implements a <see cref="IStreamStore"/> with files in a directory.
    /// </summary>
    public sealed class DirectoryStreamStore : IStreamStore
    {
        readonly string _path;
        readonly string _pathNone;
        readonly string _pathGZiped;
        readonly string[] _paths;

        readonly struct MetaEntry
        {
            public readonly CompressionKind Kind;
            public readonly FileInfo File;

            public MetaEntry( FileInfo e, CompressionKind k )
            {
                Kind = k;
                File = e;
            }
        }

        /// <summary>
        /// Initializes a new <see cref="DirectoryStreamStore"/> on a directory:
        /// the directory is created if it does not exist.
        /// An <see cref="IOException"/> is throw if the path is an existing file.
        /// </summary>
        /// <param name="path">The local directory path.</param>
        public DirectoryStreamStore( string path )
        {
            Debug.Assert( Enum.GetNames( typeof( CompressionKind ) ).SequenceEqual( new[] { "None", "GZiped" } ) );
            Debug.Assert( ((int[])Enum.GetValues( typeof( CompressionKind ) )).SequenceEqual( new[] { 0, 1 } ) );

            _path = Path.GetFullPath( path );
            _pathNone = FileUtil.NormalizePathSeparator( Path.Combine( _path, "None" ), true );
            _pathGZiped = FileUtil.NormalizePathSeparator( Path.Combine( _path, "GZiped" ), true );
            _paths = new string[] { _pathNone, _pathGZiped };
            Directory.CreateDirectory( _pathNone );
            Directory.CreateDirectory( _pathGZiped );
        }

        MetaEntry Find( string fullName )
        {
            fullName = fullName.ToLowerInvariant();
            FileInfo e = new FileInfo( _pathNone + fullName );
            if( e.Exists ) return new MetaEntry( e, CompressionKind.None );
            e = new FileInfo( _pathGZiped + fullName );
            if( e.Exists ) return new MetaEntry( e, CompressionKind.GZiped );
            return new MetaEntry();
        }

        /// <summary>
        /// Gets the full path of a file.
        /// </summary>
        /// <param name="k">The compression kind.</param>
        /// <param name="fullName">The entry name.</param>
        /// <returns>The full path of the stored file.</returns>
        string GetFullPath( CompressionKind k, string fullName ) => _paths[(int)k] + fullName.ToLowerInvariant();

        Task<bool> IStreamStore.ExistsAsync( string fullName )
        {
            return Task.FromResult( Find( fullName ).File != null );
        }

        async Task<DateTime> IStreamStore.CreateAsync( string fullName, Func<Stream, Task> writer, CompressionKind storageKind )
        {
            fullName = GetFullPath( storageKind, fullName );
            try
            {
                using( var output = new FileStream( fullName, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.SequentialScan ) )
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

        async Task<DateTime> IStreamStore.UpdateAsync( string fullName, Func<Stream, Task> writer, CompressionKind storageKind, bool allowCreate, DateTime checkLastWriteTimeUtc )
        {
            Debug.Assert( Enum.GetNames( typeof( CompressionKind ) ).SequenceEqual( new[] { "None", "GZiped" } ) );
            var e = Find( fullName );
            if( e.File == null && !allowCreate ) throw new ArgumentException( $"'{fullName}' does not exist.", nameof( fullName ) );
            if( e.File != null )
            {
                if( checkLastWriteTimeUtc != default( DateTime )
                    && checkLastWriteTimeUtc != e.File.LastWriteTimeUtc )
                {
                    return Util.UtcMaxValue;
                }
                if( e.Kind != storageKind ) e.File.Delete();
            }
            fullName = GetFullPath( storageKind, fullName );
            using( var output = new FileStream( fullName, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.SequentialScan ) )
            {
                await writer( output );
            }
            return File.GetLastWriteTimeUtc( fullName );
        }

        void IStreamStore.Delete( string fullName )
        {
            var e = Find( fullName );
            if( e.File != null ) e.File.Delete();
        }

        void IDisposable.Dispose()
        {
        }

        void IStreamStore.Flush()
        {
        }

        LocalStoredStream IStreamStore.OpenRead( string fullName )
        {
            var e = Find( fullName );
            if( e.File == null ) return new LocalStoredStream();
            return new LocalStoredStream( e.Kind, e.File.OpenRead(), e.File.LastWriteTimeUtc );
        }

        void IStreamStore.ExtractToFile( string fullName, string targetPath )
        {
            var e = Find( fullName );
            if( e.File == null ) throw new ArgumentException( $"'{fullName}' not found in Directory store.", nameof( fullName ) );
            Directory.CreateDirectory( Path.GetDirectoryName( targetPath ) );
            if( e.Kind == CompressionKind.None ) e.File.CopyTo( targetPath, false );
            else
            {
                using( var s = StreamStoreExtension.OpenRead( this, fullName, CompressionKind.None ).Stream )
                using( var output = new FileStream( targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.SequentialScan ) )
                {
                    s.CopyTo( output );
                }
            }
        }

        int IStreamStore.Delete( Func<string, bool> predicate )
        {
            int count = DoDelete( predicate, _pathNone );
            count += DoDelete( predicate, _pathGZiped );
            return count;
        }

        static int DoDelete( Func<string, bool> predicate, string prefix )
        {
            int count = 0;
            foreach( var e in Directory.EnumerateFiles( prefix, "*", SearchOption.AllDirectories ) )
            {
                if( predicate( e.Substring( prefix.Length ) ) )
                {
                    File.Delete( e );
                    ++count;
                }
            }
            return count;
        }
    }
}
