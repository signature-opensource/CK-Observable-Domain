using CK.Core;
using CK.Ng.Cris.AspNet;
using CK.Observable.WebSocketWatcher;
using CK.ObservableDomain;
using CK.TS.Angular;
using CK.TypeScript;

namespace CK.Ng.ObservableDomain.WebSocket;

/// <summary>
/// TypeScript package that register a provider for the <c>ObservableDomainClient</c> to generated TypeScript clients.
/// </summary>
[TypeScriptPackage]
[Requires<CrisAspNetPackage, WebSocketWatcherPackage, TSObservableDomainDualCasingPackage>]
[TypeScriptFile( "observable-domain-provider.ts", "initializeObservableDomainClient", "OBSERVABLE_DOMAIN_CLIENT_CONFIGURATION" )]
[NgProviderImport( "initializeObservableDomainClient" )]
[NgProviderImport( "ObservableDomainClient" )]
[NgProvider( "{ provide: ObservableDomainClient, useFactory: initializeObservableDomainClient }")]
public class NgObservableDomainPackage : TypeScriptPackage
{

}
