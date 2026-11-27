using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using NUnit.Framework;
using Shouldly;

namespace CK.Observable.PersistentDomainDriverTests;

[TestFixture]
public class SnapshotFileFormatTests
{
    static readonly DateTime _commitTime = new DateTime( 2026, 10, 27, 8, 30, 15, DateTimeKind.Utc );

    static byte[] WriteRoundTripFile()
    {
        using var output = new MemoryStream();
        SnapshotFileFormat.Write( output, 7, 42, _commitTime, new byte[] { 1, 2, 3 } );
        return output.ToArray();
    }

    static SnapshotReadStatus Read( byte[] file, out SnapshotFileHeader header, out byte[]? payload )
    {
        using var input = new MemoryStream( file );
        return SnapshotFileFormat.Read( input, out header, out payload );
    }

    // A damaged file gives the Damaged status, no payload and the default header.
    static void ShouldBeDamaged( byte[] file )
    {
        Read( file, out var header, out var payload ).ShouldBe( SnapshotReadStatus.Damaged );
        payload.ShouldBeNull();
        header.ShouldBe( default );
    }

    [Test]
    public void round_trip_returns_the_payload_and_the_header()
    {
        var file = WriteRoundTripFile();
        file.Length.ShouldBe( SnapshotFileFormat.HeaderLength + 3 );

        Read( file, out var header, out var payload ).ShouldBe( SnapshotReadStatus.Valid );

        payload.ShouldBe( new byte[] { 1, 2, 3 } );
        header.Generation.ShouldBe( 7 );
        header.TransactionSerialNumber.ShouldBe( 42 );
        header.CommitTimeUtc.ShouldBe( _commitTime );
        header.CommitTimeUtc.Kind.ShouldBe( DateTimeKind.Utc );
        header.PayloadLength.ShouldBe( 3 );
    }

    [Test]
    public void zero_filled_file_of_the_right_size_is_damaged()
    {
        ShouldBeDamaged( new byte[SnapshotFileFormat.HeaderLength + 3] );
    }

    [Test]
    public void truncated_file_is_damaged()
    {
        var file = WriteRoundTripFile();
        ShouldBeDamaged( file.AsSpan( 0, file.Length - 1 ).ToArray() );
    }

    [Test]
    public void changed_payload_byte_is_damaged()
    {
        var file = WriteRoundTripFile();
        file[^1] ^= 0x01;
        ShouldBeDamaged( file );
    }

    [Test]
    public void changed_length_field_is_damaged_without_large_allocation()
    {
        var file = WriteRoundTripFile();
        BinaryPrimitives.WriteInt64LittleEndian( file.AsSpan( 28, 8 ), long.MaxValue / 2 );
        Should.NotThrow( () => ShouldBeDamaged( file ) );
    }

    [Test]
    public void length_field_out_of_range_with_a_valid_header_checksum_is_damaged()
    {
        // The header checksum is valid. The length is larger than Array.MaxLength, so the range check rejects it.
        var file = WriteRoundTripFile();
        SetPayloadLength( file, long.MaxValue / 2 );
        Should.NotThrow( () => ShouldBeDamaged( file ) );
    }

    [Test]
    public void length_field_in_range_but_larger_than_the_stream_is_damaged_before_the_payload_read()
    {
        // The header checksum is valid and the length is in range (1 GiB). Only the stream length check rejects the file.
        // The stub throws on any read after the header: the test fails if the payload read or its allocation happens.
        var file = WriteRoundTripFile();
        SetPayloadLength( file, 1L << 30 );
        ReadWithoutPayloadRead( file ).ShouldBe( SnapshotReadStatus.Damaged );
    }

    [Test]
    public void file_with_trailing_bytes_is_damaged_before_the_payload_read()
    {
        // Header and payload are valid. Only the stream length check rejects the extra byte.
        var file = WriteRoundTripFile().Append( (byte)0 ).ToArray();
        ReadWithoutPayloadRead( file ).ShouldBe( SnapshotReadStatus.Damaged );
    }

    static void SetPayloadLength( byte[] file, long payloadLength )
    {
        BinaryPrimitives.WriteInt64LittleEndian( file.AsSpan( 28, 8 ), payloadLength );
        BinaryPrimitives.WriteUInt64LittleEndian( file.AsSpan( 44, 8 ), XxHash64.HashToUInt64( file.AsSpan( 0, 44 ) ) );
    }

    static SnapshotReadStatus ReadWithoutPayloadRead( byte[] file )
    {
        using var input = new NoPayloadReadStream( file );
        var status = SnapshotFileFormat.Read( input, out _, out var payload );
        payload.ShouldBeNull();
        return status;
    }

    // A MemoryStream that throws on any read that starts at or after the end of the header.
    sealed class NoPayloadReadStream : MemoryStream
    {
        public NoPayloadReadStream( byte[] buffer ) : base( buffer ) { }

        public override int Read( byte[] buffer, int offset, int count )
        {
            CheckPosition();
            return base.Read( buffer, offset, count );
        }

        public override int Read( Span<byte> buffer )
        {
            CheckPosition();
            return base.Read( buffer );
        }

        void CheckPosition()
        {
            if( Position >= SnapshotFileFormat.HeaderLength ) throw new InvalidOperationException( "The payload read must not happen." );
        }
    }

    [Test]
    public void a_valid_file_reads_as_Valid()
    {
        using var input = new MemoryStream( WriteRoundTripFile() );
        SnapshotFileFormat.Read( input, out var header, out var payload ).ShouldBe( SnapshotReadStatus.Valid );
        payload.ShouldBe( new byte[] { 1, 2, 3 } );
        header.Generation.ShouldBe( 7 );
    }

    [Test]
    public void known_magic_with_a_newer_format_version_is_unsupported_not_damaged()
    {
        // Every version keeps the header checksum at the same place over the same prefix.
        // A newer version with a valid header checksum is intact: the file is not damaged.
        var file = WriteRoundTripFile();
        BinaryPrimitives.WriteUInt16LittleEndian( file.AsSpan( 4, 2 ), 2 );
        RewriteHeaderChecksum( file );
        using var input = new MemoryStream( file );
        SnapshotFileFormat.Read( input, out var header, out var payload ).ShouldBe( SnapshotReadStatus.UnsupportedVersion );
        payload.ShouldBeNull();
        header.ShouldBe( default );
    }

    [Test]
    public void a_newer_format_version_with_a_bad_header_checksum_is_damaged()
    {
        var file = WriteRoundTripFile();
        BinaryPrimitives.WriteUInt16LittleEndian( file.AsSpan( 4, 2 ), 2 );
        using var input = new MemoryStream( file );
        SnapshotFileFormat.Read( input, out _, out var payload ).ShouldBe( SnapshotReadStatus.Damaged );
        payload.ShouldBeNull();
    }

    [TestCase( 4, 0x02 )]
    [TestCase( 4, 0x80 )]
    [TestCase( 5, 0x01 )]
    [TestCase( 5, 0x80 )]
    public void a_bit_flip_in_the_version_field_is_damaged_not_unsupported( int index, int bit )
    {
        // Each flip gives a version above 1. The header checksum detects it.
        var file = WriteRoundTripFile();
        file[index] ^= (byte)bit;
        BinaryPrimitives.ReadUInt16LittleEndian( file.AsSpan( 4, 2 ) ).ShouldBeGreaterThan( (ushort)1 );
        using var input = new MemoryStream( file );
        SnapshotFileFormat.Read( input, out _, out var payload ).ShouldBe( SnapshotReadStatus.Damaged );
        payload.ShouldBeNull();
    }

    static void RewriteHeaderChecksum( byte[] file )
    {
        BinaryPrimitives.WriteUInt64LittleEndian( file.AsSpan( 44, 8 ), XxHash64.HashToUInt64( file.AsSpan( 0, 44 ) ) );
    }

    [Test]
    public void format_version_zero_is_damaged()
    {
        var file = WriteRoundTripFile();
        BinaryPrimitives.WriteUInt16LittleEndian( file.AsSpan( 4, 2 ), 0 );
        using var input = new MemoryStream( file );
        SnapshotFileFormat.Read( input, out _, out var payload ).ShouldBe( SnapshotReadStatus.Damaged );
        payload.ShouldBeNull();
    }

    [Test]
    public void empty_stream_is_damaged()
    {
        ShouldBeDamaged( Array.Empty<byte>() );
    }
}
