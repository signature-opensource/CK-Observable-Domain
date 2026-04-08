using CK.Core;
using CK.ObservableDomain;
using CK.TypeScript;

namespace CK.Observable.WebSocketWatcher;

/// <summary>
/// TypeScript package that exposes the <c>WebSocketObservableDomainConnection</c> to generated TypeScript clients.
/// </summary>
[TypeScriptPackage]
[TypeScriptFile( "WebSocketObservableDomainConnection.ts", "WebSocketObservableDomainConnection" )]
[Requires<TSObservableDomainPackage>]
public class WebSocketWatcherPackage : TypeScriptPackage
{
}
