using CK.BinarySerialization;
using CK.Core;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Domain.Tests.Clients;

/// <summary>
/// Exposes the protected snapshot API of the <see cref="MemoryTransactionProviderClient"/>.
/// </summary>
sealed class SnapshotTestClient : MemoryTransactionProviderClient
{
    readonly string _domainName;

    public SnapshotTestClient( string domainName )
    {
        _domainName = domainName;
    }

    public ObservableDomain<TestObservableRootObject> Load( byte[] snapshot )
    {
        ObservableDomain? d = null;
        LoadOrCreateAndInitializeSnapshot( TestHelper.Monitor, ref d, new MemoryStream( snapshot ), startTimer: false );
        return (ObservableDomain<TestObservableRootObject>)d;
    }

    public byte[] WriteSnapshotToArray( bool skipSnapshotHeader = false )
    {
        var m = new MemoryStream();
        WriteSnapshot( m, skipSnapshotHeader );
        return m.ToArray();
    }

    public async Task<byte[]> WriteSnapshotToArrayAsync()
    {
        var m = new MemoryStream();
        await WriteSnapshotAsync( m );
        return m.ToArray();
    }

    public (byte[] Bytes, int HeaderLength, int SerialNumber) CopySnapshotToArray()
    {
        var c = CopySnapshot();
        return (c.Bytes.ToArray(), c.HeaderLength, c.SerialNumber);
    }

    protected override ObservableDomain DeserializeDomain( IActivityMonitor monitor, RewindableStream stream, bool? startTimer )
    {
        return new ObservableDomain<TestObservableRootObject>( monitor, _domainName, this, stream, startTimer: startTimer );
    }

    /// <summary>
    /// Deserializes a snapshot written with its header in a new domain (without client).
    /// </summary>
    /// <param name="domainName">The domain name.</param>
    /// <param name="snapshot">The snapshot bytes, header included.</param>
    /// <returns>The serial number and the <see cref="TestObservableRootObject.Prop1"/> of the deserialized domain.</returns>
    public static (int SerialNumber, string? Prop1) ReadSnapshot( string domainName, byte[] snapshot )
    {
        var m = new MemoryStream( snapshot );
        var (kind, _) = ReadSnapshotHeader( m );
        var stream = kind == CompressionKind.GZiped
                        ? RewindableStream.FromGZipStream( new GZipStream( m, CompressionMode.Decompress ) )
                        : RewindableStream.FromStream( m );
        using var d = new ObservableDomain<TestObservableRootObject>( TestHelper.Monitor, domainName, null, stream, startTimer: false );
        return (d.TransactionSerialNumber, d.Root.Prop1);
    }
}
