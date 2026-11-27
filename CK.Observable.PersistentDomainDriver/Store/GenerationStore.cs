using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CK.Core;

namespace CK.Observable;

/// <summary>
/// A snapshot file of one generation.
/// </summary>
/// <param name="Generation">The generation number.</param>
/// <param name="Path">The full path of the file.</param>
internal sealed record GenerationFile( long Generation, string Path );

/// <summary>
/// A mark to apply to a generation file.
/// </summary>
/// <param name="File">The file to mark.</param>
/// <param name="Mark">The mark.</param>
internal sealed record GenerationFileMark( GenerationFile File, GenerationMark Mark );

/// <summary>
/// The mark that the store adds to the name of a generation file that it does not use any more.
/// </summary>
internal enum GenerationMark
{
    /// <summary>The file failed the checks of <see cref="SnapshotFileFormat"/>.</summary>
    Damaged,

    /// <summary>The file is valid, but the domain code cannot load it.</summary>
    Unloadable,

    /// <summary>A newer generation replaced the file.</summary>
    Superseded
}

/// <summary>
/// Manages the generation files of one domain in one folder.
/// A generation file is named <c>&lt;DomainName&gt;.&lt;Generation:D19&gt;.od</c>.
/// A mark adds the suffix <c>.damaged</c>, <c>.unloadable</c> or <c>.superseded</c>.
/// The store writes each generation to a temporary file, flushes it to disk,
/// then renames it. A rename never replaces an existing file.
/// The store ignores every file that does not match these names. The comparison of the names ignores the case,
/// because NTFS ignores it: an operator can rename a file with another case.
/// </summary>
internal sealed class GenerationStore
{
    const string _odExtension = ".od";
    const string _tmpExtension = ".tmp";
    const int _generationDigits = 19;
    const int _guidDigits = 32;
    // The number of names in use that a write can move past.
    const int _maxOccupiedNames = 100;

    readonly string _folder;
    readonly string _domainName;
    readonly string _prefix;
    long _nextGeneration;

    /// <summary>
    /// Initializes a new store. The constructor reads the folder to compute <see cref="NextGeneration"/>.
    /// </summary>
    /// <param name="folder">The folder of the files. It must exist.</param>
    /// <param name="domainName">The name of the domain.</param>
    public GenerationStore( string folder, string domainName )
    {
        Throw.CheckNotNullOrWhiteSpaceArgument( folder );
        Throw.CheckNotNullOrWhiteSpaceArgument( domainName );
        _folder = folder;
        _domainName = domainName;
        _prefix = domainName + '.';
        long max = 0;
        foreach( var path in Directory.EnumerateFiles( folder ) )
        {
            if( TryParseGenerationName( Path.GetFileName( path ), out long generation, out _ ) && generation > max )
            {
                max = generation;
            }
        }
        _nextGeneration = max + 1;
    }

    /// <summary>
    /// Gets or sets the function that sets the last write time of a marked file.
    /// The default is <see cref="File.SetLastWriteTimeUtc(string, DateTime)"/>. The tests replace it to simulate a failure.
    /// </summary>
    internal Action<string, DateTime> SetLastWriteTimeUtc { get; set; } = File.SetLastWriteTimeUtc;

    /// <summary>
    /// Gets or sets the function that renames the temporary file of a new generation to its final name.
    /// The default is <see cref="File.Move(string, string, bool)"/> with no overwrite: a rename never replaces a file.
    /// The tests replace it to simulate a failure.
    /// </summary>
    internal Action<string, string> MoveFile { get; set; } = ( source, target ) => File.Move( source, target, overwrite: false );

    /// <summary>Gets the folder of the files.</summary>
    public string Folder => _folder;

    /// <summary>Gets the name of the domain.</summary>
    public string DomainName => _domainName;

    /// <summary>
    /// Gets the path of the lock file of a domain: <c>&lt;Folder&gt;/&lt;DomainName&gt;.lock</c>.
    /// The method reads no file: the driver calls it before it holds the lock.
    /// </summary>
    /// <param name="folder">The folder of the files.</param>
    /// <param name="domainName">The name of the domain.</param>
    /// <returns>The path of the lock file.</returns>
    public static string GetLockFilePath( string folder, string domainName ) => Path.Combine( folder, domainName + ".lock" );

    /// <summary>
    /// Gets the generation number of the next file.
    /// It is the largest generation in the folder, marked files included, plus one.
    /// </summary>
    public long NextGeneration => _nextGeneration;

    /// <summary>
    /// Lists the generation files that carry no mark: the ".od" files. The newest file is first.
    /// </summary>
    /// <returns>The files, newest first.</returns>
    public IReadOnlyList<GenerationFile> ListGenerations()
    {
        var result = new List<GenerationFile>();
        foreach( var path in Directory.EnumerateFiles( _folder ) )
        {
            if( TryParseGenerationName( Path.GetFileName( path ), out long generation, out string suffix ) && suffix.Length == 0 )
            {
                result.Add( new GenerationFile( generation, path ) );
            }
        }
        result.Sort( ( a, b ) => b.Generation.CompareTo( a.Generation ) );
        return result;
    }

    /// <summary>
    /// Deletes the temporary files that an earlier process left in the folder.
    /// The caller must hold the lock of the domain, so no other process writes a temporary file.
    /// </summary>
    /// <param name="monitor">The monitor.</param>
    /// <returns>The number of deleted files.</returns>
    public int DeleteOrphanTempFiles( IActivityMonitor monitor )
    {
        int count = 0;
        foreach( var path in Directory.EnumerateFiles( _folder ) )
        {
            if( !IsTempName( Path.GetFileName( path ) ) ) continue;
            try
            {
                File.Delete( path );
                ++count;
            }
            catch( Exception ex )
            {
                monitor.Warn( $"Cannot delete the temporary file '{path}'.", ex );
            }
        }
        return count;
    }

    /// <summary>
    /// Writes the next generation. The method writes a temporary file, flushes it to disk,
    /// then renames it to its final name. If a step fails, the method deletes the temporary file
    /// and rethrows. <see cref="NextGeneration"/> then keeps its value.
    /// <para>
    /// If a file or a folder already has the name of the next generation (with any case where the file system
    /// ignores the case), the method uses the next generation number. For example, an operator copied a file
    /// into the folder while the process runs. The method moves past at most 100 names, then throws an <see cref="IOException"/>.
    /// </para>
    /// <para>
    /// If another process blocks the rename for a short time (a sharing violation or an access denied error),
    /// the method tries the rename again (see <see cref="TransientFileRetry"/>).
    /// </para>
    /// </summary>
    /// <param name="transactionSerialNumber">The transaction serial number of the domain.</param>
    /// <param name="commitTimeUtc">The UTC time of the commit.</param>
    /// <param name="payload">The serialized domain.</param>
    /// <param name="beforeRename">
    /// An optional action. It runs after the flush and the close of the temporary file, and before its rename.
    /// If it throws, the method deletes the temporary file and rethrows.
    /// </param>
    /// <returns>The new generation file.</returns>
    public GenerationFile WriteGeneration( int transactionSerialNumber, DateTime commitTimeUtc, ReadOnlySpan<byte> payload, Action? beforeRename = null )
    {
        long generation = _nextGeneration;
        string target = Path.Combine( _folder, GenerationName( generation ) );
        int occupied = 0;
        while( Path.Exists( target ) )
        {
            if( ++occupied > _maxOccupiedNames )
            {
                throw new IOException( $"The names of the generations {_nextGeneration} to {generation} are in use in the folder '{_folder}'." );
            }
            target = Path.Combine( _folder, GenerationName( ++generation ) );
        }
        string temp = Path.Combine( _folder, $"{_prefix}{Guid.NewGuid():N}{_tmpExtension}" );
        try
        {
            using( var output = new FileStream( temp, FileMode.CreateNew, FileAccess.Write, FileShare.None ) )
            {
                SnapshotFileFormat.Write( output, generation, transactionSerialNumber, commitTimeUtc, payload );
                output.Flush( flushToDisk: true );
            }
            beforeRename?.Invoke();
            // An antivirus or an indexer can open the new file without FileShare.Delete for a short time.
            TransientFileRetry.Run( () => MoveFile( temp, target ) );
        }
        catch
        {
            try
            {
                File.Delete( temp );
            }
            catch
            {
                // The original error is more useful than a delete error.
            }
            throw;
        }
        _nextGeneration = generation + 1;
        return new GenerationFile( generation, target );
    }

    /// <summary>
    /// Marks a generation file: the method renames it with the suffix of the mark
    /// and sets its last write time to <paramref name="utcNow"/>.
    /// The age of a marked file is then the age of the mark.
    /// <para>
    /// The method can run again for the same file. If the file already has the mark, the method only sets its time.
    /// If the time set fails, the method tries it once more. If the second try also fails, the method logs an error:
    /// the file keeps its old time, and the age rule of <see cref="ApplyRetention"/> can delete it at once.
    /// </para>
    /// <para>
    /// If the method returns false, the file is not safely marked. The caller must protect its generation from
    /// <see cref="ApplyRetention"/> and call the method again later.
    /// </para>
    /// </summary>
    /// <param name="monitor">The monitor.</param>
    /// <param name="file">The file to mark.</param>
    /// <param name="mark">The mark.</param>
    /// <param name="utcNow">The current UTC time.</param>
    /// <param name="isRetry">True when an earlier call failed. A failed rename then logs a trace, not a warning.</param>
    /// <returns>True if the file has the mark and the current time, or if the file is gone. False if the file is not safely marked.</returns>
    public bool TryMark( IActivityMonitor monitor, GenerationFile file, GenerationMark mark, DateTime utcNow, bool isRetry = false )
    {
        string marked = file.Path + MarkSuffix( mark );
        try
        {
            if( File.Exists( file.Path ) )
            {
                File.Move( file.Path, marked, overwrite: false );
            }
            else if( !File.Exists( marked ) )
            {
                // An operator moved the file out of the folder. No file is left to protect.
                monitor.Warn( $"The file '{file.Path}' is not in the folder. The driver does not mark it as {mark}." );
                return true;
            }
        }
        catch( Exception ex )
        {
            monitor.Log( isRetry ? LogLevel.Trace : LogLevel.Warn,
                         $"Cannot mark the file '{file.Path}' as {mark}. The retention does not delete it. The driver tries again after the next save.",
                         ex );
            return false;
        }
        try
        {
            SetLastWriteTimeUtc( marked, utcNow );
            return LogMarked();
        }
        catch( Exception ex )
        {
            monitor.Warn( $"Cannot set the time of the file '{marked}'. The driver tries once more.", ex );
        }
        try
        {
            SetLastWriteTimeUtc( marked, utcNow );
            return LogMarked();
        }
        catch( Exception ex )
        {
            monitor.Error( $"Cannot set the time of the file '{marked}'. The file keeps its old time. The retention does not delete it. The driver tries again after the next save.", ex );
            return false;
        }

        bool LogMarked()
        {
            monitor.Info( $"The file '{file.Path}' is marked as {mark}. Its name is now '{Path.GetFileName( marked )}'." );
            return true;
        }
    }

    /// <summary>
    /// Calls <see cref="TryMark"/> for each mark.
    /// </summary>
    /// <param name="monitor">The monitor.</param>
    /// <param name="marks">The marks to apply.</param>
    /// <param name="utcNow">The current UTC time.</param>
    /// <param name="isRetry">True when an earlier call failed for these marks.</param>
    /// <returns>The marks that failed. The caller must protect their generations from <see cref="ApplyRetention"/>.</returns>
    public List<GenerationFileMark> ApplyMarks( IActivityMonitor monitor, IEnumerable<GenerationFileMark> marks, DateTime utcNow, bool isRetry = false )
    {
        var failed = new List<GenerationFileMark>();
        foreach( var m in marks )
        {
            if( !TryMark( monitor, m.File, m.Mark, utcNow, isRetry ) ) failed.Add( m );
        }
        return failed;
    }

    /// <summary>
    /// Deletes the files that the store does not need any more.
    /// The method keeps the newest <paramref name="generationCount"/> ".od" files.
    /// It deletes the ".damaged" and ".superseded" files that are older than <paramref name="markedKeepDuration"/>.
    /// It never deletes an ".unloadable" file. It logs and skips a file that it cannot delete.
    /// <para>
    /// It never deletes a file of a protected generation, with or without a mark.
    /// A protected ".od" file does not count in the <paramref name="generationCount"/> kept files.
    /// </para>
    /// </summary>
    /// <param name="monitor">The monitor.</param>
    /// <param name="generationCount">The number of ".od" files to keep.</param>
    /// <param name="markedKeepDuration">The time to keep a marked file.</param>
    /// <param name="utcNow">The current UTC time.</param>
    /// <param name="protectedGenerations">The generations to keep, because their mark failed. Null for none.</param>
    public void ApplyRetention( IActivityMonitor monitor, int generationCount, TimeSpan markedKeepDuration, DateTime utcNow, IReadOnlySet<long>? protectedGenerations = null )
    {
        Throw.CheckOutOfRangeArgument( generationCount >= 1 );
        var limit = utcNow - markedKeepDuration;
        var od = new List<GenerationFile>();
        var oldMarked = new List<string>();
        foreach( var path in Directory.EnumerateFiles( _folder ) )
        {
            if( !TryParseGenerationName( Path.GetFileName( path ), out long generation, out string suffix ) ) continue;
            if( protectedGenerations != null && protectedGenerations.Contains( generation ) ) continue;
            if( suffix.Length == 0 )
            {
                od.Add( new GenerationFile( generation, path ) );
            }
            else if( suffix != MarkSuffix( GenerationMark.Unloadable ) && File.GetLastWriteTimeUtc( path ) < limit )
            {
                oldMarked.Add( path );
            }
        }
        od.Sort( ( a, b ) => b.Generation.CompareTo( a.Generation ) );
        foreach( var old in od.Skip( generationCount ) )
        {
            TryDelete( monitor, old.Path );
        }
        foreach( var path in oldMarked )
        {
            TryDelete( monitor, path );
        }
    }

    static void TryDelete( IActivityMonitor monitor, string path )
    {
        try
        {
            File.Delete( path );
        }
        catch( Exception ex )
        {
            monitor.Warn( $"Cannot delete the file '{path}'.", ex );
        }
    }

    static string MarkSuffix( GenerationMark mark ) => mark switch
    {
        GenerationMark.Damaged => ".damaged",
        GenerationMark.Unloadable => ".unloadable",
        GenerationMark.Superseded => ".superseded",
        _ => throw new ArgumentOutOfRangeException( nameof( mark ) )
    };

    string GenerationName( long generation )
        => _prefix + generation.ToString( "D" + _generationDigits, CultureInfo.InvariantCulture ) + _odExtension;

    // A valid name is: prefix, 19 digits, ".od", then an optional mark suffix. The comparison ignores the case.
    // The suffix output is empty for an unmarked file. For a marked file, it is the suffix of the mark in lower case.
    bool TryParseGenerationName( string name, out long generation, out string suffix )
    {
        generation = 0;
        suffix = String.Empty;
        if( !name.StartsWith( _prefix, StringComparison.OrdinalIgnoreCase ) ) return false;
        var rest = name.AsSpan( _prefix.Length );
        if( rest.Length < _generationDigits + _odExtension.Length ) return false;
        var digits = rest.Slice( 0, _generationDigits );
        foreach( char c in digits )
        {
            if( c < '0' || c > '9' ) return false;
        }
        if( !long.TryParse( digits, NumberStyles.None, CultureInfo.InvariantCulture, out generation ) ) return false;
        var tail = rest.Slice( _generationDigits );
        if( !tail.StartsWith( _odExtension, StringComparison.OrdinalIgnoreCase ) ) return false;
        var markSuffix = tail.Slice( _odExtension.Length );
        if( markSuffix.Length == 0 ) return true;
        foreach( GenerationMark mark in Enum.GetValues<GenerationMark>() )
        {
            if( markSuffix.Equals( MarkSuffix( mark ), StringComparison.OrdinalIgnoreCase ) )
            {
                suffix = MarkSuffix( mark );
                return true;
            }
        }
        generation = 0;
        return false;
    }

    // A temporary name is: prefix, 32 hexadecimal digits, ".tmp".
    bool IsTempName( string name )
    {
        if( !name.StartsWith( _prefix, StringComparison.Ordinal ) ) return false;
        var rest = name.AsSpan( _prefix.Length );
        if( rest.Length != _guidDigits + _tmpExtension.Length ) return false;
        foreach( char c in rest.Slice( 0, _guidDigits ) )
        {
            if( !Char.IsAsciiHexDigit( c ) ) return false;
        }
        return rest.Slice( _guidDigits ).Equals( _tmpExtension, StringComparison.Ordinal );
    }
}
