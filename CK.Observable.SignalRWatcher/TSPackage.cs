using CK.StObj.TypeScript;

namespace CK.Observable.SignalRWatcher
{
    [TypeScriptPackage]
    [ImportTypeScriptLibrary( "@microsoft/signalr", ">=6.0.23", DependencyKind.Dependency, ForceUse = true )]
    [TypeScriptFile( "Res/SignalRObservableLeagueDomainService.ts" )]
    public class TSPackage : TypeScriptPackage
    {
        void StObjConstruct( CK.ObservableDomain.TSPackage obs ) { }
    }
}
