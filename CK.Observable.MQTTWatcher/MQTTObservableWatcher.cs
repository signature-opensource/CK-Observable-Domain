using CK.Core;
using CK.MQTT;
using CK.Observable.League;
using System.Text;
using Microsoft.Extensions.Options;
using CK.MQTT.Packets;
using System.IO.Pipelines;
using System.Buffers.Binary;
using CK.MQTT.Server;
using CommunityToolkit.HighPerformance;
using System.Threading.Tasks;
using System;
using System.Threading;
using System.IO;

namespace CK.Observable.MQTTWatcher;

sealed class MqttObservableWatcher : ObservableWatcher
{
    readonly MqttObservableServer _parent;
    readonly IOptionsMonitor<MQTTObservableWatcherConfig> _config;
    readonly MQTTServerAgent _mqttAgent;
    readonly IObservableLeague _league;

    public MqttObservableWatcher( MqttObservableServer parent,
                                  IOptionsMonitor<MQTTObservableWatcherConfig> config,
                                  MQTTServerAgent mqttAgent,
                                  IObservableLeague league )
        : base( league )
    {
        _parent = parent;
        _config = config;
        _mqttAgent = mqttAgent;
        _league = league;
        _mqttAgent.OnConnectionChange.Sync += OnConnectionChange;
    }

    void OnConnectionChange( IActivityMonitor m, DisconnectReason e ) => _parent.OnClientConnectionChange( m, this, e );

    sealed class ObsMqttMessage : OutgoingMessage
    {
        readonly string _domainName;
        readonly int _domainNameSize;
        readonly string _jsonExport;
        readonly int _jsonExportSize;
        static readonly Encoding _utf8 = new UTF8Encoding( false );

        public ObsMqttMessage( MQTTObservableWatcherConfig config, string domainName, string jsonExport )
            : base( Path.Combine( config.Topic, domainName ), QualityOfService.AtMostOnce, false )
        {
            _domainName = domainName;
            _jsonExport = jsonExport;
            _domainNameSize = _utf8.GetByteCount( _domainName );
            _jsonExportSize = _utf8.GetByteCount( _jsonExport );
        }

        protected override uint PayloadSize => (uint)(4 + 4 + _domainNameSize + _jsonExportSize);

        public override ValueTask DisposeAsync() => default;

        protected override ValueTask WritePayloadAsync( PipeWriter pw, CancellationToken cancellationToken )
        {
            var size = _domainNameSize + 4 + 4 + _jsonExportSize;
            Span<byte> span = pw.GetSpan( size );
            BinaryPrimitives.WriteInt32LittleEndian( span, _domainNameSize );
            _utf8.GetBytes( _domainName, span[4..] );
            span = span[(4+ _domainNameSize)..];
            BinaryPrimitives.WriteInt32LittleEndian( span, _jsonExportSize );
            _utf8.GetBytes( _jsonExport, span[4..] );
            pw.Advance( size );
            return default;
        }
    }

    protected override async ValueTask HandleEventAsync( IActivityMonitor monitor, WatchEvent e )
    {
        await _mqttAgent.PublishAsync( new ObsMqttMessage( _config.CurrentValue, e.DomainName, e.JsonExport ) );
    }

    public string ClientId => _mqttAgent.ClientId!;

    protected override string WatcherId => $"{nameof( MqttObservableServer )}-{_mqttAgent}";

}
