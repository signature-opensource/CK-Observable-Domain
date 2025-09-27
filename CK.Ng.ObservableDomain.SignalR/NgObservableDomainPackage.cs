using CK.Core;
using CK.Ng.Cris.AspNet;
using CK.TypeScript;
using CK.TS.Angular;
using CK.Observable.SignalRWatcher;

namespace CK.Ng.ObservableDomain;

[TypeScriptPackage]
[Requires<CrisAspNetPackage, ObservableSignalRPackage>]
[TypeScriptFile( "observable-domain-provider.ts", "initializeObservableDomainClient" )]
[NgProviderImport( "initializeObservableDomainClient", From = "@local/ck-gen/CK/Ng/ObservableDomain/observable-domain-provider" )]
[NgProviderImport( "ObservableDomainClient", From = "@local/ck-gen/CK/ObservableDomain/ObservableDomainClient" )]
[NgProvider( "{ provide: ObservableDomainClient, useFactory: initializeObservableDomainClient }" )]
public class NgObservableDomainPackage : TypeScriptPackage
{
}
