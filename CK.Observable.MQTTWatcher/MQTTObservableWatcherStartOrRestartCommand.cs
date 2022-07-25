using CK.Cris;
using System.Diagnostics.CodeAnalysis;

namespace CK.Observable.MQTTWatcher
{
    public interface IMqttObservableWatcherStartOrRestartCommand : ICommand<MQTTObservableWatcherStartOrRestartCommandResult>
    {
        public string MqttClientId { get; set; } //Must be removed when auth is implemented.
        public string DomainName { get; set; }
        public int TransactionNumber { get; set; }
    }

    public class MQTTObservableWatcherStartOrRestartCommandResult
    {
        [MemberNotNullWhen(true, nameof(JsonExport))]
        public bool Success { get; set; }

        public string? JsonExport { get; set; }
    }
}
