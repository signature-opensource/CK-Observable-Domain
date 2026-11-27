using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CK.Core;

namespace CK.Observable;

/// <summary>
/// The result of <see cref="DomainLoader.Load"/>.
/// </summary>
internal sealed class DomainLoadResult
{
    internal DomainLoadResult( PersistentDomainLoadOutcome outcome, ObservableDomain? domain, PersistentDomainClient? client )
    {
        Outcome = outcome;
        Domain = domain;
        Client = client;
    }

    /// <summary>Gets the outcome.</summary>
    public PersistentDomainLoadOutcome Outcome { get; }

    /// <summary>Gets the domain. Null when <see cref="PersistentDomainLoadOutcome.Kind"/> is Failed.</summary>
    public ObservableDomain? Domain { get; }

    /// <summary>Gets the client of <see cref="Domain"/>. Null when the kind is Failed.</summary>
    public PersistentDomainClient? Client { get; }
}

/// <summary>
/// Runs the recovery chain at start.
/// <see cref="Load"/> changes no file. The driver applies the marks of <see cref="GetMarks"/> just before
/// the rename of the first file that the start writes.
/// </summary>
internal static class DomainLoader
{
    /// <summary>
    /// Tries the newest generation, then up to <see cref="PersistentDomainOptions.FallbackGenerationCount"/> older ones.
    /// The method never creates, renames or deletes a file.
    /// </summary>
    /// <param name="monitor">The monitor.</param>
    /// <param name="store">The store of the domain.</param>
    /// <param name="options">The options of the domain.</param>
    /// <param name="clientFactory">Creates a new client. The method calls it once for each attempt, because a failed load leaves its client unusable.</param>
    /// <returns>The domain and the outcome.</returns>
    public static DomainLoadResult Load( IActivityMonitor monitor, GenerationStore store, PersistentDomainOptions options, Func<PersistentDomainClient> clientFactory )
    {
        Throw.CheckNotNullArgument( monitor );
        Throw.CheckNotNullArgument( store );
        Throw.CheckNotNullArgument( options );
        Throw.CheckNotNullArgument( clientFactory );

        var files = store.ListGenerations();
        if( files.Count == 0 )
        {
            return CreateNew( monitor, store, clientFactory, PersistentDomainLoadKind.Created, Array.Empty<SkippedSnapshotFile>(), Array.Empty<GenerationFile>() );
        }

        int attempts = Math.Min( 1 + Math.Max( 0, options.FallbackGenerationCount ), files.Count );
        var skipped = new List<SkippedSnapshotFile>();
        int tried = 0;
        while( tried < attempts )
        {
            var file = files[tried++];

            byte[]? payload;
            SnapshotFileHeader header;
            SnapshotReadStatus status;
            try
            {
                // An antivirus can scan the newest file at boot. A short block of the file is not a read error.
                (status, header, payload) = TransientFileRetry.Run( () =>
                {
                    using var input = File.OpenRead( file.Path );
                    var s = SnapshotFileFormat.Read( input, out var h, out var p );
                    return (s, h, p);
                }, ( ex, wait ) => monitor.Trace( $"Cannot read the file '{file.Path}'. The driver tries again in {wait} ms. Error: {ex.Message}" ) );
            }
            catch( Exception ex ) when( ex is IOException or UnauthorizedAccessException )
            {
                // A read error is not damage. The start fails whatever the options.
                monitor.Warn( $"Cannot read the file '{file.Path}'. The start fails and no file changes.", ex );
                skipped.Add( new SkippedSnapshotFile( file.Path, file.Generation, SnapshotFileResult.ReadError, ex ) );
                return Failed( store, skipped );
            }
            if( status == SnapshotReadStatus.UnsupportedVersion )
            {
                // A newer package wrote the file. It is not damaged, and the older files can be older than the downgrade.
                // As for an unloadable file, the fallback stops and the retention never deletes the file.
                var ex = new InvalidDataException( $"The file '{file.Path}' has a format version that is newer than the version of this code. A newer package wrote it." );
                monitor.Warn( ex.Message );
                skipped.Add( new SkippedSnapshotFile( file.Path, file.Generation, SnapshotFileResult.Unloadable, ex ) );
                break;
            }
            if( payload == null )
            {
                monitor.Warn( $"The file '{file.Path}' is damaged." );
                skipped.Add( new SkippedSnapshotFile( file.Path, file.Generation, SnapshotFileResult.Damaged, null ) );
                continue;
            }

            // A failed load leaves its client unusable. Each attempt has a new client.
            var client = clientFactory();
            try
            {
                var domain = client.Load( monitor, payload );
                var kind = tried == 1 ? PersistentDomainLoadKind.Loaded : PersistentDomainLoadKind.LoadedFromFallback;
                var outcome = new PersistentDomainLoadOutcome( kind, store.Folder, file.Generation, header.TransactionSerialNumber, skipped, Array.Empty<GenerationFile>() );
                monitor.Info( $"Domain '{store.DomainName}': {kind} from generation {file.Generation} in the folder '{store.Folder}'." );
                return new DomainLoadResult( outcome, domain, client );
            }
            catch( Exception ex )
            {
                // The file is valid, so older files fail for the same reason. The fallback stops.
                monitor.Warn( $"The file '{file.Path}' is valid but does not load.", ex );
                skipped.Add( new SkippedSnapshotFile( file.Path, file.Generation, SnapshotFileResult.Unloadable, ex ) );
                break;
            }
        }

        if( options.CreateOnLoadError )
        {
            var superseded = files.Skip( tried ).ToArray();
            return CreateNew( monitor, store, clientFactory, PersistentDomainLoadKind.CreatedAfterLoadFailure, skipped, superseded );
        }
        monitor.Warn( $"Domain '{store.DomainName}': no allowed file loads and CreateOnLoadError is false. The start fails and no file changes." );
        return Failed( store, skipped );
    }

    /// <summary>
    /// Gets the marks of a successful outcome: ".damaged", ".unloadable" and ".superseded".
    /// A file with a read error gets no mark.
    /// </summary>
    /// <param name="outcome">The outcome of <see cref="Load"/>. It must not be a Failed outcome.</param>
    /// <returns>The marks to apply.</returns>
    public static IReadOnlyList<GenerationFileMark> GetMarks( PersistentDomainLoadOutcome outcome )
    {
        Throw.CheckNotNullArgument( outcome );
        Throw.CheckArgument( outcome.Kind != PersistentDomainLoadKind.Failed );
        var marks = new List<GenerationFileMark>();
        foreach( var skipped in outcome.SkippedFiles )
        {
            GenerationMark mark;
            switch( skipped.Result )
            {
                case SnapshotFileResult.Damaged: mark = GenerationMark.Damaged; break;
                case SnapshotFileResult.Unloadable: mark = GenerationMark.Unloadable; break;
                default: continue;
            }
            marks.Add( new GenerationFileMark( new GenerationFile( skipped.Generation, skipped.Path ), mark ) );
        }
        foreach( var file in outcome.SupersededGenerations )
        {
            marks.Add( new GenerationFileMark( file, GenerationMark.Superseded ) );
        }
        return marks;
    }

    static DomainLoadResult CreateNew( IActivityMonitor monitor,
                                       GenerationStore store,
                                       Func<PersistentDomainClient> clientFactory,
                                       PersistentDomainLoadKind kind,
                                       IReadOnlyList<SkippedSnapshotFile> skipped,
                                       IReadOnlyList<GenerationFile> superseded )
    {
        var client = clientFactory();
        var domain = client.CreateNew( monitor );
        var outcome = new PersistentDomainLoadOutcome( kind, store.Folder, null, null, skipped, superseded );
        if( kind == PersistentDomainLoadKind.CreatedAfterLoadFailure )
        {
            // The state of the domain is lost: this is an error for the operator, also when the options allow it.
            monitor.Error( $"Domain '{store.DomainName}': {kind} in the folder '{store.Folder}'. No allowed file loads and the driver creates a new empty domain. "
                           + $"The first save marks {skipped.Count} tried file(s) and {superseded.Count} superseded file(s)." );
        }
        else
        {
            monitor.Info( $"Domain '{store.DomainName}': {kind} in the folder '{store.Folder}'." );
        }
        return new DomainLoadResult( outcome, domain, client );
    }

    static DomainLoadResult Failed( GenerationStore store, List<SkippedSnapshotFile> skipped )
    {
        var outcome = new PersistentDomainLoadOutcome( PersistentDomainLoadKind.Failed, store.Folder, null, null, skipped, Array.Empty<GenerationFile>() );
        return new DomainLoadResult( outcome, null, null );
    }
}
