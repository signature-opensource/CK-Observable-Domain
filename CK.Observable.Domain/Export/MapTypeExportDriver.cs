using CK.Core;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace CK.Observable;

class MapTypeExportDriver<TKey,TValue> : IObjectExportTypeDriver<IEnumerable<KeyValuePair<TKey,TValue>>>
{
    readonly IObjectExportTypeDriver<TKey> _keyExporter;
    readonly IObjectExportTypeDriver<TValue> _valueExporter;

    public MapTypeExportDriver( IObjectExportTypeDriver<TKey> keyExporter, IObjectExportTypeDriver<TValue> valueExporter )
    {
        _keyExporter = keyExporter;
        _valueExporter = valueExporter;
    }

    public Type BaseType => typeof( IEnumerable<KeyValuePair<TKey, TValue>> );

    public bool IsDefaultBehavior => false;

    public IReadOnlyList<PropertyInfo> ExportableProperties => Array.Empty<PropertyInfo>();

    public void Export( IEnumerable<KeyValuePair<TKey, TValue>> o, int num, ObjectExporter exporter )
    {
        Throw.CheckNotNullArgument( exporter );
        exporter.ExportMap( num, o, null, null );
    }

    void IObjectExportTypeDriver.Export( object o, int num, ObjectExporter exporter ) => Export( (IEnumerable<KeyValuePair<TKey, TValue>>)o, num, exporter );
}
