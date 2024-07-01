using CK.Core;
using System.Runtime.CompilerServices;

namespace CKSetup
{
    partial class Program
    {
        sealed class CKomposableAppBuilder
        {
            readonly string[] _args;
            readonly NormalizedPath _callerFile;

            public CKomposableAppBuilder( string[] args, [CallerFilePath]string? callerFile = null )
            {
                _args = args;
                _callerFile = callerFile;
            }

            public NormalizedPath GetAppBinPath( string appName )
            {
                return _callerFile.RemoveLastPart().Combine( _args[0] ).AppendPart( appName );
            }

            internal NormalizedPath GetOutputPath( string hostName )
            {
                return _callerFile.RemoveLastPart().AppendPart( hostName ) );
            }
        }

    }
}
