using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable.League
{
    public partial class ObservableLeague
    {
        class DomainClient : StreamStoreClient
        {
            readonly Shell _shell;

            public DomainClient( string domainName, IStreamStore store, Action<IActivityMonitor, ObservableDomain>? loadHook, Shell shell, IObservableDomainClient? next = null )
                : base( domainName, store, loadHook, next )
            {
                _shell = shell;
            }

            public override void OnTransactionCommit( in SuccessfulTransactionEventArgs c )
            {
                base.OnTransactionCommit( c );
                bool hasActiveTimedEvents = c.Domain.TimeManager.ActiveTimedEventsCount > 0;
                c.PostActions.Add( ctx =>
                {
                    // If a post action has been added after this one and eventually fails, there is nothing to compensate:
                    // the domain has or hasn't timed events and the shell has to know this.
                    return _shell.SynchronizeOptionsAsync( ctx.Monitor, options: null, hasActiveTimedEvents );
                } );
            }
        }
    }
}
