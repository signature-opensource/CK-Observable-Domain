using CK.AspNet.WebSocketChannel;
using CK.Core;
using CK.ObservableDomain;
using CK.TypeScript;

namespace CK.Observable.WebSocketWatcher;

/// <summary>
/// TypeScript package that exposes the <c>WebSocketObservableDomainConnection</c> to generated TypeScript clients.
/// <para>
/// It adapts the application-wide <c>WSConnection</c> to <c>IObservableDomainConnection</c>: the socket
/// belongs to the channel, this only claims the <see cref="ObservableDomainWatcherManager.Topic"/> topic on it.
/// </para>
/// </summary>
[TypeScriptPackage]
[TypeScriptFile( "WebSocketObservableDomainConnection.ts", "WebSocketObservableDomainConnection" )]
[Requires<TSObservableDomainPackage, WebSocketChannelPackage>]
public class WebSocketWatcherPackage : TypeScriptPackage
{
}
