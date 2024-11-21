using CK.TypeScript;

namespace CK.Observable.SignalRWatcher;

[TypeScriptPackage]
[TypeScriptImportLibrary( "@microsoft/signalr", ">=6.0.23", DependencyKind.PeerDependency, ForceUse = true )]
[TypeScriptFile( "SignalRObservableLeagueDomainService.ts", "SignalRObservableLeagueDomainService" )]
public class TSPackage : TypeScriptPackage
{
    void StObjConstruct( CK.ObservableDomain.TSPackage obs ) { }
}
