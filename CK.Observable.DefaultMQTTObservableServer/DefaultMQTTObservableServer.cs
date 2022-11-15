using CK.Core;
using CK.MQTT.Server;
using CK.Observable.League;
using CK.Observable.MQTTWatcher;
using Microsoft.Extensions.Options;

namespace CK.Observable.DefaultMQTTObservableServer
{
    public class DefaultMQTTObservableServer : MqttObservableServer, ISingletonAutoService
    {
        public DefaultMQTTObservableServer( IOptionsMonitor<MQTTObservableWatcherConfig> config,
                                            LocalMQTTDemiServer server,
                                            DefaultObservableLeague league )
            : base( config, server, league )
        {
        }
    }
}
