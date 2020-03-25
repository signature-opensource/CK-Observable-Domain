using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CK.Observable.League
{
    class CoordinatorClient : IObservableDomainClient
    {
        public void OnDomainCreated( IActivityMonitor monitor, ObservableDomain d )
        {
        }

        public void OnTransactionCommit( in SuccessfulTransactionContext context )
        {
        }

        public void OnTransactionFailure( IActivityMonitor monitor, ObservableDomain d, IReadOnlyList<CKExceptionData> errors )
        {
        }

        public void OnTransactionStart( IActivityMonitor monitor, ObservableDomain d, DateTime timeUtc )
        {
        }
    }
}
