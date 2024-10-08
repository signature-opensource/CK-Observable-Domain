using System;

namespace CK.Observable;

/// <summary>
/// Specify compression kind.
/// This is used as a simple marker at the start of the snapshots managed
/// by <see cref="MemoryTransactionProviderClient"/>.
/// </summary>
public enum CompressionKind
{
    /// <summary>
    /// The stream contains the actual, uncompressed, data.
    /// </summary>
    None = 0,

    /// <summary>
    /// The stream is a gziped stream of the actual data.
    /// </summary>
    GZiped = 1
}
