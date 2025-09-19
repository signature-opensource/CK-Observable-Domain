using CK.Core;
using CK.ObservableDomain;
using CK.TypeScript;

namespace CK.Observable.MQTTWatcher;

[TypeScriptPackage]
[TypeScriptImportLibrary( "mqtt", "5.10.1", DependencyKind.PeerDependency )]
[TypeScriptImportLibrary( "@types/node", "^20", DependencyKind.DevDependency )]
[TypeScriptFile( "MQTTObservableLeagueDomainService.ts", "MQTTObservableLeagueDomainService" )]
[Requires<TSObservableDomainPackage>]
public class ObservableMQTTPackage : TypeScriptPackage
{
}
