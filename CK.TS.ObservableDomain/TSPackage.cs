using CK.StObj.TypeScript;

namespace CK.ObservableDomain
{
    [TypeScriptPackage]
    [TypeScriptImportLibrary( "rxjs", ">=7.5.6", DependencyKind.Dependency, ForceUse = true )]
    [TypeScriptFile( "GraphSerializer.ts" )]
    [TypeScriptFile( "IObservableDomainLeagueDriver.ts", "IObservableDomainLeagueDriver" )]
    [TypeScriptFile( "ObservableDomain.ts", "ObservableDomain", "WatchEvent" )]
    [TypeScriptFile( "ObservableDomainClient.ts", "ObservableDomainClient", "ObservableDomainClientConnectionState" )]
    public class TSPackage : TypeScriptPackage
    {
    }
}
