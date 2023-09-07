using CK.BinarySerialization;
using CK.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CK.Observable.League
{
    public partial class ObservableLeague
    {
        class DomainClient : StreamStoreClient
        {
            readonly Shell _shell;

            public DomainClient( string domainName,
                                 IStreamStore store,
                                 Shell shell,
                                 IObservableDomainClient? next = null )
                : base( domainName, store, next )
            {
                _shell = shell;
            }

            public override void OnTransactionCommit( TransactionDoneEventArgs c )
            {
                base.OnTransactionCommit( c );
                // To update the shell knowledge of the NextActiveTime, knowing if we have rolled back or not
                // is useless.
                DateTime nextActiveTime = c.Domain.TimeManager.NextActiveTime;
                c.DomainPostActions.Add( ctx =>
                {
                    // If a post action has been added after this one and eventually fails, there is nothing to compensate:
                    // the domain has or hasn't timed events and the shell has to know this.
                    return _shell.SynchronizeOptionsAsync( ctx.Monitor, options: null, nextActiveTime );
                } );
            }

            protected override ObservableDomain DoDeserializeDomain( IActivityMonitor monitor, RewindableStream stream, bool? startTimer )
            {
                return _shell.DeserializeDomain( monitor, stream, startTimer );
            }
        }
    }
}
