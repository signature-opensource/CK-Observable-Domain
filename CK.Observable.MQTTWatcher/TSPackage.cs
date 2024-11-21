using CK.TypeScript;
using System.Xml.Linq;

namespace CK.Observable.MQTTWatcher;

[TypeScriptPackage]
[TypeScriptImportLibrary("mqtt", "5.10.1", DependencyKind.PeerDependency, ForceUse = true)]
[TypeScriptImportLibrary( "@types/node", "^20.14.2", DependencyKind.DevDependency, ForceUse = true )]
[TypeScriptFile( "MQTTObservableLeagueDomainService.ts", "MQTTObservableLeagueDomainService" )]
public class TSPackage : TypeScriptPackage
{
    void StObjConstruct( CK.ObservableDomain.TSPackage obs ) { }
}
