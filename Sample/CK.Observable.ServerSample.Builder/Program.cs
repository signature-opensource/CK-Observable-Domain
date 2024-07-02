using CK.Core;
using CK.Monitoring;
using CK.Setup;
using System;
using System.Diagnostics;

namespace CKSetup
{
    partial class Program
    {
        static int Main( string[] args )
        {
            return CKomposableAppBuilder.Run( ( monitor, builder ) =>
            {
                var tsBinPath = builder.EnsureDefaultTypeScriptAspectConfiguration();
                tsBinPath.ModuleSystem = TSModuleSystem.CJSAndES6;
            } );
        }

    }
}
