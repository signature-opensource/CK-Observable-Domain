using CK.StObj.TypeScript;

namespace CK.Observable.MQTTWatcher
{
    [TypeScriptPackage]
    [ImportTypeScriptLibrary("mqtt", "5.7.0", DependencyKind.Dependency)]
    [TypeScriptFile( "MQTTObservableLeagueDomainService.ts" )]
    public class TSPackage : TypeScriptPackage
    {
        void StObjConstruct( CK.ObservableDomain.TSPackage obs ) { }
    }
}
