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
                                 Action<IActivityMonitor, ObservableDomain>? initializer,
                                 Shell shell,
                                 IObservableDomainClient? next = null )
                : base( domainName, store, initializer, next )
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

            protected override ObservableDomain DoDeserializeDomain( IActivityMonitor monitor, Stream stream, Func<ObservableDomain, bool> loadHook )
            {
                return _shell.DeserializeDomain( monitor, stream, loadHook );
            }
        }
    }
}
