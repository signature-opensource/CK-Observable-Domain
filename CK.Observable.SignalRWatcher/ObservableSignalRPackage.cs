using CK.Core;
using CK.ObservableDomain;
using CK.TypeScript;

namespace CK.Observable.SignalRWatcher;

[TypeScriptPackage]
[TypeScriptImportLibrary( "@microsoft/signalr", "^8.0.17", DependencyKind.Dependency )]
[TypeScriptFile( "SignalRObservableLeagueDomainService.ts", "SignalRObservableLeagueDomainService" )]
[Requires<TSObservableDomainPackage>]
public class ObservableSignalRPackage : TypeScriptPackage
{
}
