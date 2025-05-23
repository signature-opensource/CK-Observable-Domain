using CK.BinarySerialization;
using CK.Core;
using System;
using System.IO;

namespace CK.Observable.Domain.Tests.Clients;

public class TestMemoryTransactionProviderClient : MemoryTransactionProviderClient
{
    private readonly MemoryStream? _domainCreatedStream;

    public TestMemoryTransactionProviderClient( MemoryStream? domainCreatedStream = null )
    {
        _domainCreatedStream = domainCreatedStream;
    }

    public MemoryStream CreateStreamFromSnapshot()
    {
        MemoryStream ms = new MemoryStream();
        WriteSnapshot( ms, skipSnapshotHeader: true );
        ms.Position = 0;
        return ms;
    }

    protected override ObservableDomain DeserializeDomain( IActivityMonitor monitor, RewindableStream stream, bool? startTimer )
    {
        throw new NotImplementedException( "This is not called since LoadOrCreateAndInitializeSnapshot is not called here." );
    }
}
