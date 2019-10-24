using CK.Core;
using System;
using System.IO;

namespace CK.Observable.Domain.Tests.Clients
{
    public class TestMemoryTransactionProviderClient : MemoryTransactionProviderClient
    {
        private readonly Stream _domainCreatedStream;

        public TestMemoryTransactionProviderClient( Stream domainCreatedStream = null )
        {
            _domainCreatedStream = domainCreatedStream;
        }

        public override void OnDomainCreated( IActivityMonitor monitor, ObservableDomain d, DateTime timeUtc )
        {
            if( _domainCreatedStream != null )
            {
                d.Load( _domainCreatedStream );
            }
            base.OnDomainCreated( monitor, d, timeUtc );
        }

        public MemoryStream CreateStreamFromSnapshot()
        {
            MemoryStream ms = new MemoryStream();
            WriteSnapshotTo( ms );
            ms.Position = 0;
            return ms;
        }
    }
}
