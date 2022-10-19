using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using CK.Auth;
using CK.Core;
using CK.Cris;
using CK.Observable.League;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.SignalR;

namespace CK.Observable.SignalRWatcher
{
    public class SignalRWatcherCommandHandler : IAutoService
    {
        readonly IHubContext<ObservableAppHub> _hubContext;

        public SignalRWatcherCommandHandler( IHubContext<ObservableAppHub> hubContext )
        {
            _hubContext = hubContext;
        }
        [CommandHandler]
        public async Task<string> HandleStartOrRestartWatchAsync( IActivityMonitor m, ISignalRObservableWatcherStartOrRestartCommand command, IAuthenticationInfo authenticationInfo, HttpContext  httpContext )
        {
            //httpContext.Items[ObservableAppHub]
            //var client  = _hubContext.Clients.Client( "");

            //if( client. )
            //{
            //    throw new InvalidDataException( $"{command.ClientId} does not exists, or is not identified as you. " );
            //}
            //var watchEvent = await watcher.GetStartOrRestartEventAsync( m, command.DomainName, command.TransactionNumber );
            //return watchEvent.JsonExport;
            return "";
        }
    }

    public class ObservableAppHub : Hub<IObservableAppSignalrClient>
    {
        readonly IHubContext<ObservableAppHub, IObservableAppSignalrClient> _hubCtx;
        readonly IActivityMonitor _monitor;
        readonly DefaultObservableLeague _defaultObservableLeague;

        string WatcherKey => Context.ConnectionId;

        public ObservableAppHub
        (
            IHubContext<ObservableAppHub, IObservableAppSignalrClient> hubCtx,
            IActivityMonitor monitor,
            DefaultObservableLeague defaultObservableLeague
        )
        {
            _hubCtx = hubCtx;
            _monitor = monitor;
            _defaultObservableLeague = defaultObservableLeague;
        }

        internal static object _symbol = typeof( ObservableAppHub );
        public override async Task OnConnectedAsync()
        {
            _monitor.Info( $"Connected to {nameof( ObservableAppHub )}: {Context.ConnectionId}" );
            if( !Context.Items.ContainsKey( _symbol ) )
            {
                var watcher = new HubObservableWatcher( _hubCtx, Context.ConnectionId, _defaultObservableLeague );
                bool couldAdd = Context.Items.TryAdd( _symbol, watcher );

                if( !couldAdd )
                {
                    watcher.Dispose();
                }
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync( Exception? exception )
        {
            _monitor.Info( $"Disconnected from {nameof( ObservableAppHub )}: {Context.ConnectionId}" );
            if( Context.Items.Remove( _symbol, out var watcher ) )
            {
                ((HubObservableWatcher)watcher!).Dispose();
            }

            await base.OnDisconnectedAsync( exception );
        }
    }
}
