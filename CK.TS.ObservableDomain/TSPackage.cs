using CK.StObj.TypeScript;

namespace CK.ObservableDomain
{
    [TypeScriptPackage]
    [ImportTypeScriptLibrary( "rxjs", ">=7.5.6", DependencyKind.Dependency, ForceUse = true )]
    [TypeScriptFile( "Res/GraphSerializer.ts" )]
    [TypeScriptFile( "Res/IObservableDomainLeagueDriver.ts", "IObservableDomainLeagueDriver" )]
    [TypeScriptFile( "Res/ObservableDomain.ts", "ObservableDomain", "WatchEvent" )]
    [TypeScriptFile( "Res/ObservableDomainClient.ts", "ObservableDomainClient", "ObservableDomainClientConnectionState" )]
    public class TSPackage : TypeScriptPackage
    {
    }
}
