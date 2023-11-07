using System;
using System.Threading.Tasks;
using CK.Core;
using CK.Observable.League;
using Microsoft.AspNetCore.SignalR;

namespace CK.Observable.SignalRWatcher
{
    public sealed class ObservableAppHub : Hub<IObservableAppSignalrClient>
    {
        readonly SignalRWatcherManager _watcherManager;
        readonly IHubContext<ObservableAppHub, IObservableAppSignalrClient> _hubCtx;
        readonly IActivityMonitor _monitor;
        readonly DefaultObservableLeague _defaultObservableLeague;

        public ObservableAppHub( SignalRWatcherManager watcherManager,
                                 IHubContext<ObservableAppHub, IObservableAppSignalrClient> hubCtx,
                                 IActivityMonitor monitor,
                                 DefaultObservableLeague defaultObservableLeague )
        {
            _watcherManager = watcherManager;
            _hubCtx = hubCtx;
            _monitor = monitor;
            _defaultObservableLeague = defaultObservableLeague;
        }

        public override async Task OnConnectedAsync()
        {
            _monitor.Info( $"Connected to {nameof( ObservableAppHub )}: {Context.ConnectionId}" );
            _watcherManager.AddWatcher( Context.ConnectionId, new HubObservableWatcher( _hubCtx, Context.ConnectionId, _defaultObservableLeague ) );
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync( Exception? exception )
        {
            _monitor.Info( $"Disconnected from {nameof( ObservableAppHub )}: {Context.ConnectionId}" );
            _watcherManager.ReleaseWatcher( Context.ConnectionId );
            await base.OnDisconnectedAsync( exception );
        }
    }
}
