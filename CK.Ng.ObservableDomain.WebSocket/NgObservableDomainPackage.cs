using CK.Core;
using CK.Ng.Cris.AspNet;
using CK.Ng.AspNet.WebSocketChannel;
using CK.Observable.WebSocketWatcher;
using CK.ObservableDomain;
using CK.TS.Angular;
using CK.TypeScript;

namespace CK.Ng.ObservableDomain.WebSocket;

/// <summary>
/// TypeScript package that register a provider for the <c>ObservableDomainClient</c> to generated TypeScript clients.
/// <para>
/// It requires <see cref="NgWebSocketChannelPackage"/>: the client watches domains through the one
/// WSConnection of the application, it does not open a socket of its own.
/// </para>
/// </summary>
[TypeScriptPackage]
[Requires<CrisAspNetPackage, WebSocketWatcherPackage, TSObservableDomainDualCasingPackage, NgWebSocketChannelPackage>]
[TypeScriptFile( "observable-domain-provider.ts", "initializeObservableDomainClient", "OBSERVABLE_DOMAIN_CLIENT_CONFIGURATION" )]
[NgProviderImport( "initializeObservableDomainClient" )]
[NgProviderImport( "ObservableDomainClient" )]
[NgProvider( "{ provide: ObservableDomainClient, useFactory: initializeObservableDomainClient }")]
public class NgObservableDomainPackage : TypeScriptPackage
{

}
