using System;
using System.IO;
using CK.BinarySerialization;
using CK.Core;

namespace CK.Observable.Domain.Tests.Clients
{
    class ConcreteMemoryTransactionProviderClient : MemoryTransactionProviderClient
    {
        protected override ObservableDomain DeserializeDomain( IActivityMonitor monitor, RewindableStream stream, bool? startTimer )
        {
            throw new NotSupportedException( "ConcreteMemoryTransactionProviderClient is not a domain manager." );
        }
    }

}

