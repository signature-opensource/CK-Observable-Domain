using CK.Cris;
using CK.TypeScript;

namespace CK.Observable.MQTTWatcher;

[TypeScript]
public interface IMQTTObservableWatcherStartOrRestartCommand : ICommand<string>
{
    public string MqttClientId { get; set; } //Must be removed when auth is implemented.
    public string DomainName { get; set; }
    public int TransactionNumber { get; set; }
}
