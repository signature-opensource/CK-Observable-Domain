using CK.StObj.TypeScript;
using System.Xml.Linq;

namespace CK.Observable.MQTTWatcher
{
    [TypeScriptPackage]
    [ImportTypeScriptLibrary("mqtt", "5.7.0", DependencyKind.Dependency, ForceUse = true)]
    [ImportTypeScriptLibrary( "@types/node", "^20.14.2", DependencyKind.DevDependency, ForceUse = true )]
    [TypeScriptFile( "Res/MQTTObservableLeagueDomainService.ts" )]
    public class TSPackage : TypeScriptPackage
    {
        void StObjConstruct( CK.ObservableDomain.TSPackage obs ) { }
    }
}
