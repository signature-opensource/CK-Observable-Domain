using System.Collections.Generic;
using System.Threading.Tasks;

namespace CK.Observable.SignalRWatcher
{
    public interface IObservableAppSignalrClient
    {
        Task OnStateEventsAsync( string domainName, string eventsJson );
    }
}
