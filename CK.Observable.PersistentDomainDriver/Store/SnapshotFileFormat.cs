using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using CK.Core;

namespace CK.Observable;

/// <summary>
/// The fields that a snapshot file header carries.
/// </summary>
/// <param name="Generation">The generation of the snapshot.</param>
/// <param name="TransactionSerialNumber">The transaction serial number of the domain at the commit.</param>
/// <param name="CommitTimeUtc">The UTC time of the commit.</param>
/// <param name="PayloadLength">The length of the payload in bytes.</param>
/// <param name="PayloadChecksum">The XxHash64 checksum of the payload.</param>
internal readonly record struct SnapshotFileHeader( long Generation,
                                                    int TransactionSerialNumber,
                                                    DateTime CommitTimeUtc,
                                                    long PayloadLength,
                                                    ulong PayloadChecksum );

/// <summary>
/// The result of <see cref="SnapshotFileFormat.Read"/>.
/// </summary>
internal enum SnapshotReadStatus
{
    /// <summary>The file is valid: the payload is available.</summary>
    Valid,

    /// <summary>The file fails the checks of the format.</summary>
    Damaged,

    /// <summary>
    /// The file has the magic of the format, a valid header checksum, and a format version that is newer
    /// than the version of this code. A newer package wrote it. The file is not damaged.
    /// </summary>
    UnsupportedVersion
}

/// <summary>
/// Reads and writes a snapshot file: a fixed header, then the payload.
/// The header holds a magic, a format version, the snapshot identity, the payload length
/// and two XxHash64 checksums: one for the payload and one for the header itself.
/// All numbers are little-endian.
/// <para>
/// Format rule for every version: a file starts with at least <see cref="HeaderLength"/> bytes.
/// Bytes 0 to 3 are the magic, bytes 4 and 5 are the format version, and bytes 44 to 51 are the
/// XxHash64 checksum of bytes 0 to 43. A newer version can change the other fields, but not these three.
/// So the reader compares the header checksum before it interprets the version: a version field with a
/// changed bit is damage, not a file of a newer package.
/// </para>
/// </summary>
internal static class SnapshotFileFormat
{
    /// <summary>The length of the header in bytes.</summary>
    public const int HeaderLength = 52;

    const ushort _formatVersion = 1;

    // Offsets of the header fields.
    const int _versionOffset = 4;
    const int _reservedOffset = 6;
    const int _generationOffset = 8;
    const int _serialOffset = 16;
    const int _commitTimeOffset = 20;
    const int _payloadLengthOffset = 28;
    const int _payloadChecksumOffset = 36;
    const int _headerChecksumOffset = 44;

    static ReadOnlySpan<byte> Magic => "CKOD"u8;

    /// <summary>
    /// Writes the header, then the payload, to the output stream.
    /// </summary>
    /// <param name="output">The stream to write to.</param>
    /// <param name="generation">The generation of the snapshot.</param>
    /// <param name="transactionSerialNumber">The transaction serial number of the domain.</param>
    /// <param name="commitTimeUtc">The UTC time of the commit.</param>
    /// <param name="payload">The payload to write.</param>
    public static void Write( Stream output, long generation, int transactionSerialNumber, DateTime commitTimeUtc, ReadOnlySpan<byte> payload )
    {
        Throw.CheckNotNullArgument( output );
        Span<byte> header = stackalloc byte[HeaderLength];
        header.Clear();
        Magic.CopyTo( header );
        BinaryPrimitives.WriteUInt16LittleEndian( header.Slice( _versionOffset ), _formatVersion );
        BinaryPrimitives.WriteUInt16LittleEndian( header.Slice( _reservedOffset ), 0 );
        BinaryPrimitives.WriteInt64LittleEndian( header.Slice( _generationOffset ), generation );
        BinaryPrimitives.WriteInt32LittleEndian( header.Slice( _serialOffset ), transactionSerialNumber );
        BinaryPrimitives.WriteInt64LittleEndian( header.Slice( _commitTimeOffset ), commitTimeUtc.Ticks );
        BinaryPrimitives.WriteInt64LittleEndian( header.Slice( _payloadLengthOffset ), payload.Length );
        BinaryPrimitives.WriteUInt64LittleEndian( header.Slice( _payloadChecksumOffset ), XxHash64.HashToUInt64( payload ) );
        BinaryPrimitives.WriteUInt64LittleEndian( header.Slice( _headerChecksumOffset ), XxHash64.HashToUInt64( header.Slice( 0, _headerChecksumOffset ) ) );
        output.Write( header );
        output.Write( payload );
    }

    /// <summary>
    /// Reads a snapshot file. The method checks the header first and the length of the stream second.
    /// It allocates and reads the payload only when both are valid. A damaged length field
    /// never causes a large allocation or a read past the end of the stream.
    /// <para>
    /// The method checks the magic and the header checksum before the version (see the format rule of this class).
    /// A file with a valid header checksum and a format version above the version of this code gives
    /// <see cref="SnapshotReadStatus.UnsupportedVersion"/>. The method then reads no other field.
    /// Format version 0 does not exist: it gives <see cref="SnapshotReadStatus.Damaged"/>.
    /// </para>
    /// </summary>
    /// <param name="input">The stream to read. It must support <see cref="Stream.Length"/>.</param>
    /// <param name="header">The header. It is the default value when the file is not valid.</param>
    /// <param name="payload">The payload. It is null when the file is not valid.</param>
    /// <returns>The status of the file. IO errors propagate.</returns>
    public static SnapshotReadStatus Read( Stream input, out SnapshotFileHeader header, out byte[]? payload )
    {
        Throw.CheckNotNullArgument( input );
        header = default;
        payload = null;
        Span<byte> raw = stackalloc byte[HeaderLength];
        if( input.ReadAtLeast( raw, HeaderLength, throwOnEndOfStream: false ) < HeaderLength ) return SnapshotReadStatus.Damaged;

        if( !raw.Slice( 0, Magic.Length ).SequenceEqual( Magic ) ) return SnapshotReadStatus.Damaged;
        // The format rule: the header checksum has the same place and covers the same bytes in every version.
        ulong expectedHeaderChecksum = BinaryPrimitives.ReadUInt64LittleEndian( raw.Slice( _headerChecksumOffset ) );
        if( XxHash64.HashToUInt64( raw.Slice( 0, _headerChecksumOffset ) ) != expectedHeaderChecksum ) return SnapshotReadStatus.Damaged;
        ushort version = BinaryPrimitives.ReadUInt16LittleEndian( raw.Slice( _versionOffset ) );
        if( version > _formatVersion ) return SnapshotReadStatus.UnsupportedVersion;
        if( version != _formatVersion ) return SnapshotReadStatus.Damaged;

        long payloadLength = BinaryPrimitives.ReadInt64LittleEndian( raw.Slice( _payloadLengthOffset ) );
        // The header checksum is valid, but the length can still be wrong or out of range.
        if( payloadLength < 0 || payloadLength > Array.MaxLength ) return SnapshotReadStatus.Damaged;
        if( input.Length != HeaderLength + payloadLength ) return SnapshotReadStatus.Damaged;

        var bytes = new byte[payloadLength];
        if( input.ReadAtLeast( bytes, bytes.Length, throwOnEndOfStream: false ) < bytes.Length ) return SnapshotReadStatus.Damaged;

        ulong payloadChecksum = BinaryPrimitives.ReadUInt64LittleEndian( raw.Slice( _payloadChecksumOffset ) );
        if( XxHash64.HashToUInt64( bytes ) != payloadChecksum ) return SnapshotReadStatus.Damaged;

        header = new SnapshotFileHeader( BinaryPrimitives.ReadInt64LittleEndian( raw.Slice( _generationOffset ) ),
                                         BinaryPrimitives.ReadInt32LittleEndian( raw.Slice( _serialOffset ) ),
                                         new DateTime( BinaryPrimitives.ReadInt64LittleEndian( raw.Slice( _commitTimeOffset ) ), DateTimeKind.Utc ),
                                         payloadLength,
                                         payloadChecksum );
        payload = bytes;
        return SnapshotReadStatus.Valid;
    }
}
