using CK.Cris;
using CK.StObj.TypeScript;

namespace CK.Observable.SignalRWatcher;

[TypeScript]
public interface ISignalRObservableWatcherStartOrRestartCommand : ICommand<string>
{
    public string ClientId { get; set; }
    public string DomainName { get; set; }
    public int TransactionNumber { get; set; }
}
