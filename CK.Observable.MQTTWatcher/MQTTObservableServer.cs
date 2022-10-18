using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CK.Core;
using CK.Cris;
using CK.MQTT;
using CK.MQTT.Server;
using CK.MQTT.Server.Server;
using CK.Observable.League;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CK.Observable.MQTTWatcher
{
    public class MqttObservableServer : IHostedService
    {
        readonly IOptionsMonitor<MQTTObservableWatcherConfig> _config;
        readonly MQTTDemiServer _server;
        readonly IObservableLeague _league;

        readonly ConcurrentDictionary<string, MqttObservableWatcher> _watchers = new();
        public MqttObservableServer( IOptionsMonitor<MQTTObservableWatcherConfig> config, MQTTDemiServer server, DefaultObservableLeague league )
        {
            _config = config;
            _server = server;
            _league = league;
        }

        public Task StartAsync( CancellationToken cancellationToken )
        {
            _server.OnNewClient.Sync += OnNewClient;
            return Task.CompletedTask;
        }

        [CommandHandler]
        public async Task<string> HandleStartOrRestartWatchAsync( IActivityMonitor m, IMQTTObservableWatcherStartOrRestartCommand command )
        {
            await _processNewClientLock.WaitAsync();
            try
            {
                if( !_watchers.TryGetValue( command.MqttClientId, out var watcher ) )
                {
                    throw new InvalidDataException( $"{command.MqttClientId} does not exists, or is not identified as you. " );
                }
                var watchEvent = await watcher.GetStartOrRestartEventAsync( m, command.DomainName, command.TransactionNumber );
                return watchEvent.JsonExport;
            }
            finally
            {
                _processNewClientLock.Release();
            }
        }

        internal void OnClientConnectionChange( IActivityMonitor m, MqttObservableWatcher watcher, DisconnectReason reason )
        {
            if( reason != DisconnectReason.None )
            {
                // thread safe thanks to the concurrent dictionary, only the one removing it will run this code.
                if( _watchers.TryRemove( watcher.ClientId, out _ ) )
                {
                    m.Info($"Removed MQTT Agent {watcher.ClientId} from OD watcher.");
                    watcher.Dispose();
                }
            }
        }
        readonly SemaphoreSlim _processNewClientLock = new( 1 );//TODO: upgrade to read/Write lock with async support.
        bool _processNewClients = true;
        void OnNewClient( IActivityMonitor m, MQTTServerAgent newAgent )
        {
            Throw.CheckNotNullArgument( newAgent.ClientId );
            _processNewClientLock.Wait();
            if( _processNewClients )
            {
                using( m.OpenInfo( $"MQTT Agent '{newAgent.ClientId}' connected, pairing it with Observable League." ) )
                {
                    _watchers.TryAdd( newAgent.ClientId, new MqttObservableWatcher( this, _config, newAgent, _league ) );
                }
            }
            _processNewClientLock.Release(); // we exit lock later because we don't want to do this while StopAsync runs.
        }

        public async Task StopAsync( CancellationToken cancellationToken )
        {
            await _processNewClientLock.WaitAsync( cancellationToken );
            _processNewClients = false;
            _processNewClientLock.Release();
            _server.OnNewClient.Sync -= OnNewClient;
            ActivityMonitor? m = null;
            foreach( var clientId in _watchers.Keys.ToArray() ) //capture keys
            {
                if( _watchers.TryRemove( clientId, out var watcher ) )
                {
                    watcher.Dispose();
                }
                else
                {
                    (m ??= new()).Error( "While cleaning the dictionary" );
                }
            }
            _watchers.Clear();
        }
    }
}
