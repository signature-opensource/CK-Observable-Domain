using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using CK.AspNet;
using CK.Auth;
using CK.Core;
using CK.Cris;
using CK.Observable.League;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.SignalR;

namespace CK.Observable.SignalRWatcher
{
    public class SignalRWatcherManager : ISingletonAutoService
    {
        readonly Dictionary<string, HubObservableWatcher> _watchers = new();

        [CommandHandler]
        public async Task<string> HandleStartOrRestartWatchAsync( IActivityMonitor m, ISignalRObservableWatcherStartOrRestartCommand command, IAuthenticationInfo authenticationInfo, ScopedHttpContext httpContext )
        {
            HubObservableWatcher? val;
            lock( _watchers )
            {
                if( !_watchers.TryGetValue( command.ClientId, out val ) )
                {
                    throw new InvalidDataException( $"{command.ClientId} does not exists, or is not identified as you. " );
                }
            }
            var watchEvent = await val.GetStartOrRestartEventAsync( m, command.DomainName, command.TransactionNumber );
            return watchEvent.JsonExport;
        }

        internal void AddWatcher( string key, HubObservableWatcher watcher )
        {
            lock( _watchers )
            {
                _watchers.Add( key, watcher );
            }
        }

        internal HubObservableWatcher RemoveWatcher( string key )
        {
            lock( _watchers )
            {
                var val = _watchers[key];
                _watchers.Remove( key );
                return val;
            }
        }
    }

    public class ObservableAppHub : Hub<IObservableAppSignalrClient>
    {
        readonly SignalRWatcherManager _watcherManager;
        readonly IHubContext<ObservableAppHub, IObservableAppSignalrClient> _hubCtx;
        readonly IActivityMonitor _monitor;
        readonly DefaultObservableLeague _defaultObservableLeague;

        string WatcherKey => Context.ConnectionId;

        public ObservableAppHub
        (
            SignalRWatcherManager watcherManager,
            IHubContext<ObservableAppHub, IObservableAppSignalrClient> hubCtx,
            IActivityMonitor monitor,
            DefaultObservableLeague defaultObservableLeague
        )
        {
            _watcherManager = watcherManager;
            _hubCtx = hubCtx;
            _monitor = monitor;
            _defaultObservableLeague = defaultObservableLeague;
        }

        internal static object _symbol = typeof( ObservableAppHub );
        public override async Task OnConnectedAsync()
        {
            _monitor.Info( $"Connected to {nameof( ObservableAppHub )}: {Context.ConnectionId}" );
            _watcherManager.AddWatcher( Context.ConnectionId, new HubObservableWatcher( _hubCtx, Context.ConnectionId, _defaultObservableLeague ) );
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync( Exception? exception )
        {
            _monitor.Info( $"Disconnected from {nameof( ObservableAppHub )}: {Context.ConnectionId}" );
            var removedWatcher = _watcherManager.RemoveWatcher( Context.ConnectionId );
            removedWatcher.Dispose();

            await base.OnDisconnectedAsync( exception );
        }
    }
}
