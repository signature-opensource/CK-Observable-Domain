using System;
using System.Collections.Generic;
using System.Linq;

namespace CK.Observable;

/// <summary>
/// The result of a start, from the point of view of the files.
/// </summary>
public enum PersistentDomainLoadKind
{
    /// <summary>The folder had no snapshot file. The driver created a new domain.</summary>
    Created,

    /// <summary>The newest snapshot file loaded.</summary>
    Loaded,

    /// <summary>The newest file failed. An older generation loaded.</summary>
    LoadedFromFallback,

    /// <summary>
    /// No allowed file loaded and <see cref="PersistentDomainOptions.CreateOnLoadError"/> is true.
    /// The driver created a new domain.
    /// </summary>
    CreatedAfterLoadFailure,

    /// <summary>No allowed file loaded, or a file had a read error. The start failed. The driver changed no file.</summary>
    Failed
}

/// <summary>
/// The result of one snapshot file that the driver tried and did not load.
/// </summary>
public enum SnapshotFileResult
{
    /// <summary>The file failed the checks of the file format.</summary>
    Damaged,

    /// <summary>The file is valid, but the domain code cannot deserialize it.</summary>
    Unloadable,

    /// <summary>The driver cannot open or read the file, for example because the file is locked.</summary>
    ReadError
}

/// <summary>
/// A snapshot file that the driver tried and did not load.
/// </summary>
/// <param name="Path">The full path of the file.</param>
/// <param name="Generation">The generation number from the name of the file.</param>
/// <param name="Result">Why the driver did not load the file.</param>
/// <param name="Error">
/// The exception for <see cref="SnapshotFileResult.Unloadable"/> and <see cref="SnapshotFileResult.ReadError"/>.
/// Null for <see cref="SnapshotFileResult.Damaged"/>.
/// </param>
public sealed record SkippedSnapshotFile( string Path, long Generation, SnapshotFileResult Result, Exception? Error );

/// <summary>
/// What happened when the driver loaded the domain. This object does not change.
/// </summary>
public sealed class PersistentDomainLoadOutcome
{
    readonly IReadOnlyList<GenerationFile> _supersededGenerations;

    internal PersistentDomainLoadOutcome( PersistentDomainLoadKind kind,
                                          string folderPath,
                                          long? loadedGeneration,
                                          int? loadedTransactionSerialNumber,
                                          IReadOnlyList<SkippedSnapshotFile> skippedFiles,
                                          IReadOnlyList<GenerationFile> supersededGenerations )
    {
        Kind = kind;
        FolderPath = folderPath;
        LoadedGeneration = loadedGeneration;
        LoadedTransactionSerialNumber = loadedTransactionSerialNumber;
        // The outcome keeps copies: a later change to the lists of the caller does not change the outcome.
        SkippedFiles = skippedFiles.ToArray();
        _supersededGenerations = supersededGenerations.ToArray();
        SupersededFiles = _supersededGenerations.Select( f => f.Path ).ToArray();
    }

    /// <summary>Gets the kind of the outcome.</summary>
    public PersistentDomainLoadKind Kind { get; }

    /// <summary>Gets the path of the folder of the domain.</summary>
    public string FolderPath { get; }

    /// <summary>Gets the generation of the loaded file. Null if the driver created a new domain or the start failed.</summary>
    public long? LoadedGeneration { get; }

    /// <summary>Gets the transaction serial number of the loaded file. Null if the driver created a new domain or the start failed.</summary>
    public int? LoadedTransactionSerialNumber { get; }

    /// <summary>Gets the files that the driver tried and did not load, newest first.</summary>
    public IReadOnlyList<SkippedSnapshotFile> SkippedFiles { get; }

    /// <summary>
    /// Gets the paths of the files that the driver did not try and that it marks ".superseded".
    /// The driver did not read these files, so it does not know if they are valid.
    /// The list is empty unless <see cref="Kind"/> is <see cref="PersistentDomainLoadKind.CreatedAfterLoadFailure"/>.
    /// </summary>
    public IReadOnlyList<string> SupersededFiles { get; }

    /// <summary>Gets whether the domain is new: <see cref="Kind"/> is Created or CreatedAfterLoadFailure.</summary>
    public bool IsNewDomain => Kind is PersistentDomainLoadKind.Created or PersistentDomainLoadKind.CreatedAfterLoadFailure;

    internal IReadOnlyList<GenerationFile> SupersededGenerations => _supersededGenerations;
}
