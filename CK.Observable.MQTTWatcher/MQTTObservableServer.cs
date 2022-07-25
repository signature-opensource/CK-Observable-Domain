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
        public async Task<MQTTObservableWatcherStartOrRestartCommandResult> HandleStartOrRestartWatchAsync(IActivityMonitor m,
            IMqttObservableWatcherStartOrRestartCommand command)
        {
            _processNewClientLock.EnterReadLock();
            try
            {
                if( !_watchers.TryGetValue( command.MqttClientId, out var watcher ) ) return new MQTTObservableWatcherStartOrRestartCommandResult()
                {
                    Success = false
                };
                var watchEvent = await watcher.GetStartOrRestartEventAsync( m, command.DomainName, command.TransactionNumber );
                return new MQTTObservableWatcherStartOrRestartCommandResult()
                {
                    JsonExport = watchEvent.JsonExport,
                    Success = true
                };
            }
            finally
            {
                _processNewClientLock.ExitReadLock();
            }
        }

        internal void OnClientConnectionChange( IActivityMonitor m, MqttObservableWatcher watcher, DisconnectReason reason )
        {
            if( reason != DisconnectReason.None )
            {
                // thread safe thanks to the concurrent dictionary, only the one removing it will run this code.
                if( _watchers.TryRemove( watcher.ClientId, out _ ) )
                {
                    watcher.Dispose();
                }
            }
        }

        readonly ReaderWriterLockSlim _processNewClientLock = new();
        bool _processNewClients = true;
        void OnNewClient( IActivityMonitor m, MQTTServerAgent newAgent )
        {
            Throw.CheckNotNullArgument( newAgent.ClientId );
            _processNewClientLock.EnterReadLock();
            if( _processNewClients ) return;
            using( m.OpenInfo( "MQTT Agent connected, pairing it with Observable League." ) )
            {
                _watchers.TryAdd( newAgent.ClientId, new MqttObservableWatcher( this, _config, newAgent, _league ) );
            }
            _processNewClientLock.ExitReadLock(); // we exit lock later because we don't want to do this while StopAsync runs.
        }

        public Task StopAsync( CancellationToken cancellationToken )
        {
            _processNewClientLock.EnterWriteLock();
            _processNewClients = false;
            _processNewClientLock.ExitWriteLock();
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
            return Task.CompletedTask;
        }
    }
}
