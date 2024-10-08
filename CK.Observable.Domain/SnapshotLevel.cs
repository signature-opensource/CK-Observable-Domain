namespace CK.Observable;

/// <summary>
/// Defines if a snapshot must be taken and if it must be persisted
/// if possible.
/// </summary>
public enum SnapshotLevel
{
    /// <summary>
    /// No snapshot required.
    /// </summary>
    None,

    /// <summary>
    /// A snapshot is required.
    /// </summary>
    Snapshot,

    /// <summary>
    /// A snapshot is required and if possible be persisted.
    /// </summary>
    SnapshotAndPersist
}
