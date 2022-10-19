using CK.Cris;

namespace CK.Observable.SignalRWatcher
{
    public interface ISignalRObservableWatcherStartOrRestartCommand : ICommand<string>
    {
        public string DomainName { get; set; }
        public int TransactionNumber { get; set; }
    }
}
