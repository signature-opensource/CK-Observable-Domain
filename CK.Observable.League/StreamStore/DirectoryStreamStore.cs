using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CK.Observable;

/// <summary>
/// Implements a <see cref="IStreamStore"/> with files in a directory.
/// </summary>
public sealed class DirectoryStreamStore : IBackupStreamStore
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
        Throw.CheckNotNullOrEmptyArgument( name );
        name = name.ToLowerInvariant();
        return _path + name;
    }

    string GetFullWritePath( ref string name )
    {
        var p = GetFullPath( ref name );
        Throw.CheckArgument( FileUtil.IndexOfInvalidFileNameChars( name ) < 0 );
        return p;
    }

    string GetBackupFolder( string name ) => _path + name + ".bak";

    Task<bool> IStreamStore.ExistsAsync( string name )
    {
        return Task.FromResult( File.Exists( GetFullPath( ref name ) ) );
    }

    async Task<DateTime> IStreamStore.CreateAsync( string name, Func<Stream, Task> writer )
    {
        var path = GetFullWritePath( ref name );
        try
        {
            using( var output = new FileStream( path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.SequentialScan | FileOptions.Asynchronous ) )
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
        if( !exists && !allowCreate ) Throw.ArgumentException( $"'{name}' does not exist in store '{_path}'.", nameof( name ) );
        if( exists )
        {
            if( checkLastWriteTimeUtc != default && checkLastWriteTimeUtc != File.GetLastWriteTimeUtc( path ) )
            {
                return Util.UtcMaxValue;
            }
        }
        var now = DateTime.UtcNow;
        string tempFilePath = FileUtil.EnsureUniqueTimedFile( path, ".tmp", now );
        using( var output = File.Open( tempFilePath, FileMode.Create ) )
        {
            await writer( output ).ConfigureAwait( false );
            await output.FlushAsync().ConfigureAwait( false );
        }
        if( exists )
        {
            string backupPath = GetBackupFolder( name );
            Directory.CreateDirectory( backupPath );
            FileUtil.MoveToUniqueTimedFile( path, backupPath + Path.DirectorySeparatorChar, String.Empty, now );
        }
        File.Move( tempFilePath, path );
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

    /// <inheritdoc />
    public void CleanBackups( IActivityMonitor monitor, string name, TimeSpan maximumKeepDuration, long maximumTotalBytes )
    {
        if( maximumKeepDuration <= TimeSpan.Zero && maximumTotalBytes <= 0 )
        {
            monitor.Warn( $"Cleanup is disabled in the ObservableLeague store: maximumKeepDuration and maximumTotalBytes are not set." );
            return; // All means of cleanup are disabled. Don't do anything.
        }

        int preservedByDateCount = 0;
        long byteLengthOfPreservedByDate = 0;
        long totalByteLength = 0;

        // Consider all backups for deletion when zero (as long as another criteria is enabled, see return above)
        DateTime minDate = maximumKeepDuration > TimeSpan.Zero ? DateTime.UtcNow - maximumKeepDuration : DateTime.UtcNow;
        DirectoryInfo backupDirectory = new DirectoryInfo( GetBackupFolder( name ) );

        if( !backupDirectory.Exists )
        {
            monitor.Warn( $"Backup directory '{backupDirectory.FullName}' doesn't exist." );
            return; 
        }
        var candidates = new List<KeyValuePair<DateTime, FileInfo>>();
        foreach( FileInfo file in backupDirectory.EnumerateFiles() )
        {
            var n = file.Name.AsSpan();
            if( FileUtil.TryMatchFileNameUniqueTimeUtcFormat( ref n, out DateTime d ) )
            {
                if( d >= minDate )
                {
                    ++preservedByDateCount;
                    byteLengthOfPreservedByDate += file.Length;
                }
                totalByteLength += file.Length;
                candidates.Add( new KeyValuePair<DateTime, FileInfo>( d, file ) );
            }
        }

        int canBeDeletedCount = candidates.Count - preservedByDateCount;
        bool canDeleteByBytes = totalByteLength > 0;

        if( canBeDeletedCount > 0 )
        {
            // Note: The comparer is a reverse comparer. The most RECENT log file is the FIRST.
            candidates.Sort( ( a, b ) => DateTime.Compare( b.Key, a.Key ) );
            candidates.RemoveRange( 0, preservedByDateCount );
            monitor.Debug( $"Considering {candidates.Count} log files to delete." );

            long totalFileSize = byteLengthOfPreservedByDate;
            int i = 0;
            foreach( var kvp in candidates )
            {
                var file = kvp.Value;
                totalFileSize += file.Length;

                if( !canDeleteByBytes // Both count and bytes are disabled: Delete all older files
                    || (canDeleteByBytes && totalFileSize > maximumTotalBytes) ) // Size enabled: Delete when size matches
                {
                    try
                    {
                        file.Delete();
                    }
                    catch( Exception ex )
                    {
                        monitor.Warn( $"Failed to delete file {file.FullName} (housekeeping).", ex );
                    }
                }
                ++i;
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> GetBackupNames( string name )
    {
        DirectoryInfo backupDirectory = new DirectoryInfo( GetBackupFolder( name ) );
        if( !backupDirectory.Exists ) return Array.Empty<string>(); // Directory doesn't even exist.
        return backupDirectory.EnumerateFiles( "*", SearchOption.TopDirectoryOnly ).Select( f => f.Name ).ToArray();

    }
}
