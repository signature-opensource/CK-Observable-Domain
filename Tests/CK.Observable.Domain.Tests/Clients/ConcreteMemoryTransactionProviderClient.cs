using System;
using System.IO;
using CK.Core;

namespace CK.Observable.Domain.Tests.Clients
{
    class ConcreteMemoryTransactionProviderClient : MemoryTransactionProviderClient
    {
        protected override ObservableDomain DeserializeDomain( IActivityMonitor monitor, Stream stream, Func<ObservableDomain, bool> loadHook )
        {
            throw new NotSupportedException( "ConcreteMemoryTransactionProviderClient is not a domain manager." );
        }
    }

}

