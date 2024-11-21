using System;
using System.Collections.Generic;
using System.Reflection;

namespace CK.Observable;

class EnumerableTypeExporter<T> : IObjectExportTypeDriver<IEnumerable<T>>
{
    public EnumerableTypeExporter( IObjectExportTypeDriver<T> itemExporter )
    {
    }

    public Type BaseType => typeof( IEnumerable<T> );

    public bool IsDefaultBehavior => false;

    public IReadOnlyList<PropertyInfo> ExportableProperties => Array.Empty<PropertyInfo>();

    public void Export( IEnumerable<T> o, int num, ObjectExporter exporter )
    {
        exporter.ExportList( num, o );
    }

    void IObjectExportTypeDriver.Export( object o, int num, ObjectExporter exporter ) => Export( (IEnumerable<T>)o, num, exporter );
}
