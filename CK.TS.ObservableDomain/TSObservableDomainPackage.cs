using CK.TypeScript;

namespace CK.ObservableDomain;

[TypeScriptPackage]
[TypeScriptImportLibrary( "rxjs", ">=7.5.6", DependencyKind.Dependency )]
[TypeScriptFile( "GraphSerializer.ts", "ISerializeOptions", "IDeserializeOptions", "serialize", "deserialize" )]
[TypeScriptFile( "IObservableDomainConnection.ts", "IObservableDomainConnection" )]
[TypeScriptFile( "ObservableDomain.ts", "ObservableDomain", "WatchEvent" )]
[TypeScriptFile( "ObservableDomainClient.ts", "ObservableDomainClient", "ObservableDomainClientConnectionState" )]
public class TSObservableDomainPackage : TypeScriptPackage
{
}
