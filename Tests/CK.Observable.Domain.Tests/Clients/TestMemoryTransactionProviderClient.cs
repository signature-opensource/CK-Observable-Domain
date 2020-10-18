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

        public override void OnDomainCreated( IActivityMonitor monitor, ObservableDomain d )
        {
            if( _domainCreatedStream != null )
            {
                d.Load( monitor, _domainCreatedStream );
            }
            base.OnDomainCreated( monitor, d );
        }

        public MemoryStream CreateStreamFromSnapshot()
        {
            MemoryStream ms = new MemoryStream();
            WriteSnapshot( ms, skipSnapshotHeader: true );
            ms.Position = 0;
            return ms;
        }

        protected override ObservableDomain DeserializeDomain( IActivityMonitor monitor, Stream stream, Func<ObservableDomain, bool> loadHook )
        {
            throw new NotImplementedException( "This is not called since LoadOrCreateAndInitializeSnapshot is not called here." );
        }
    }
}
