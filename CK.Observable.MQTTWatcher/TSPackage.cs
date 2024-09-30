using CK.StObj.TypeScript;
using System.Xml.Linq;

namespace CK.Observable.MQTTWatcher
{
    [TypeScriptPackage]
    [ImportTypeScriptLibrary("mqtt", "5.10.1", DependencyKind.PeerDependency, ForceUse = true)]
    [ImportTypeScriptLibrary( "@types/node", "^20.14.2", DependencyKind.DevDependency, ForceUse = true )]
    [TypeScriptFile( "MQTTObservableLeagueDomainService.ts" )]
    public class TSPackage : TypeScriptPackage
    {
        void StObjConstruct( CK.ObservableDomain.TSPackage obs ) { }
    }
}
