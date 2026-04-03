using CK.Core;
using CK.Observable;

namespace CK.Ng.ObservableDomain.WebSocket.Tests.Drivers;

public sealed class SampleConfigurator : IObservableDomainConfigurator<SampleDomainDriver>
{
    public bool ConfigureDomain( IActivityMonitor monitor, Observable.ObservableDomain observableDomain )
    {
        monitor.Trace( ">>> SampleConfigurator was called!" );
        return true;
    }
}
