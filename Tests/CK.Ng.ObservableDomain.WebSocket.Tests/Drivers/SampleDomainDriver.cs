using CK.Core;
using CK.Observable;

namespace CK.Ng.ObservableDomain.WebSocket.Tests.Drivers;

[RealObject( RequiredBy = [ typeof( ObservableDomainDriverHost ) ] )]
public sealed class SampleDomainDriver : TransientDomainDriver<SampleDomainDriver>
{
    public SampleDomainDriver()
        : base( domainName: null, autoStartTimer: false )
    {
    }

    protected override void PreConfigure( IActivityMonitor monitor, Observable.ObservableDomain domain )
    {
        monitor.Trace( ">>> SampleDomainDriver.PreConfigure was called!" );
    }

    protected override void PostConfigure( IActivityMonitor monitor, Observable.ObservableDomain domain )
    {
        monitor.Trace( ">>> SampleDomainDriver.PostConfigure was called!" );
    }
}
