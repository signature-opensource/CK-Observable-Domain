using CK.Cris;

namespace CK.Observable.MQTTWatcher
{
    public interface IMQTTObservableWatcherStartOrRestartCommand : ICommand<string>
    {
        public string MqttClientId { get; set; } //Must be removed when auth is implemented.
        public string DomainName { get; set; }
        public int TransactionNumber { get; set; }
    }
}
