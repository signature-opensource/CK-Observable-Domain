using CK.Core;
using CK.Setup;
using System;

// Gives the ".../bin" instead of the file name (I forgot the path root handling :().
var caller = new NormalizedPath( AppContext.BaseDirectory ).RemoveLastPart( 2 );
return CKomposableAppBuilder.Run( programFilePath: caller );
