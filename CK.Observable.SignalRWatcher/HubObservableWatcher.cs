using System;
using CK.Core;
using CK.Observable;
using CK.Observable.League;
using Microsoft.AspNetCore.SignalR;

namespace CK.Observable.SignalRWatcher
{
    public sealed class HubObservableWatcher : ObservableWatcher
    {
        readonly IHubContext<ObservableAppHub, IObservableAppSignalrClient> _hub;
        readonly string _connectionId;

        public HubObservableWatcher( IHubContext<ObservableAppHub, IObservableAppSignalrClient> hub, string connectionId, IObservableLeague league )
            : base( league )
        {
            _hub = hub;
            _connectionId = connectionId;
        }

        protected override async ValueTask HandleEventAsync( IActivityMonitor monitor, WatchEvent e )
        {
            try
            {
                var c = _hub.Clients.Client( _connectionId );
                await c.OnStateEventsAsync( e.DomainName, e.JsonExport );
            }
            catch( Exception ex )
            {
                monitor.Error( ex );
            }
        }

        protected override string WatcherId => $"{nameof( HubObservableWatcher )}-{_connectionId}";
    }
}
