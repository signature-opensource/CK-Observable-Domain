using CK.StObj.TypeScript;
using System.Xml.Linq;

namespace CK.Observable.MQTTWatcher
{
    [TypeScriptPackage]
    [ImportTypeScriptLibrary( "mqtt", "5.7.0", DependencyKind.Dependency )]
    [ImportTypeScriptLibrary( "@types/node", "^20.14.2", DependencyKind.DevDependency )]
    [TypeScriptFile( "MQTTObservableLeagueDomainService.ts" )]
    public class TSPackage : TypeScriptPackage
    {
        void StObjConstruct( CK.ObservableDomain.TSPackage obs ) { }
    }
}
