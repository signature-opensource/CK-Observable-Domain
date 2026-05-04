using CK.Core;
using CK.TypeScript;

namespace CK.ObservableDomain;

[TypeScriptPackage]
[Requires<TSObservableDomainPackage>]
[TypeScriptFile( "DualCasingProxy.ts", "wrapDualCasing", "wrapDeep", "setDeprecationWarningsEnabled", "isPlainObject", "toCamel", "toPascal" )]
public class TSObservableDomainDualCasingPackage : TypeScriptPackage
{
}
