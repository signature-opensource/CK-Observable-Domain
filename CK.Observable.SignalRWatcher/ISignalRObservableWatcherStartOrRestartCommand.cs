using CK.Cris;
using CK.TypeScript;

namespace CK.Observable.SignalRWatcher;

[TypeScriptType]
public interface ISignalRObservableWatcherStartOrRestartCommand : ICommand<string>
{
    public string ClientId { get; set; }
    public string DomainName { get; set; }
    public int TransactionNumber { get; set; }
}
