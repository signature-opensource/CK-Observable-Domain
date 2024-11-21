using System;
using System.Diagnostics;

namespace CK.Observable;

/// <summary>
/// Encapsulates Signature/json-graph-serializer npm package prefix option.
/// For C# objects, the <see cref="EmptyPrefix"/> default is fine.
/// </summary>
public class JSONExportTargetOptions
{
    /// <summary>
    /// The best option to use: no prefix.
    /// </summary>
    public static readonly JSONExportTargetOptions EmptyPrefix = new JSONExportTargetOptions( String.Empty ); 

    string[] _pTypeFormats;

    /// <summary>
    /// Initializes a new option.
    /// </summary>
    /// <param name="prefix">
    /// Defaults to the default of @Signature/json-graph-serializer npm package.
    /// For C# objects, the <see cref="EmptyPrefix"/> is fine.
    /// </param>
    public JSONExportTargetOptions( string prefix = "~$£€" )
    {
        if( prefix == null ) prefix = String.Empty;
        Prefix = prefix;

        prefix = '"' + prefix;
        var typePrefix = prefix + "þ";

        ObjectReferencePrefix = '{' + prefix + ">\":";

        ObjectNumberPrefix = '{' + prefix + "°\":";

        var listFormat = "[{{" + typePrefix + "\":[{0},\"A\"]}}";
        var mapFormat = "[{{" + typePrefix + "\":[{0},\"M\"]}}";
        var setFormat = "[{{" + typePrefix + "\":[{0},\"S\"]}}";
        _pTypeFormats = new string[]
            {
                listFormat,
                mapFormat,
                setFormat
            };
    }

    /// <summary>
    /// Get the prefix.
    /// </summary>
    string Prefix { get; }

    internal string ObjectNumberPrefix { get; }

    internal string ObjectReferencePrefix { get; }

    internal string GetPrefixTypeFormat( ObjectExportedKind kind )
    {
        Debug.Assert( kind >= ObjectExportedKind.List );
        Debug.Assert( (int)ObjectExportedKind.List == 2 );
        Debug.Assert( (int)ObjectExportedKind.Map == 3 );
        Debug.Assert( (int)ObjectExportedKind.Set == 4 );
        return _pTypeFormats[(int)kind - 2];
    }



}
