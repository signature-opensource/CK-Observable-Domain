using CK.Core;
using CK.MQTT.Server;
using CK.Observable.League;
using Microsoft.Extensions.Options;

namespace CK.Observable.MQTTWatcher
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
