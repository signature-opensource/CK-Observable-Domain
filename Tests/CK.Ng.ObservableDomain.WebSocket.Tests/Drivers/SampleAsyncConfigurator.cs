using System.Threading.Tasks;
using CK.Core;
using CK.Observable;

namespace CK.Ng.ObservableDomain.WebSocket.Tests.Drivers;

public sealed class SampleAsyncConfigurator : IObservableDomainAsyncConfigurator
{
    public Task<bool> ConfigureHostAsync( IActivityMonitor monitor, ObservableDomainDriverHost host )
    {
        monitor.Trace( ">>> SampleAsyncConfigurator was called!" );
        return Task.FromResult( true );
    }
}
