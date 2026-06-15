using System;
using System.Collections.Generic;
using System.Reflection;
using CK.Core;

namespace CK.Observable;

class SetTypeExportDriver<TItem> : IObjectExportTypeDriver<IObservableReadOnlySet<TItem>>
{
    readonly IObjectExportTypeDriver<TItem> _itemExporter;

    public SetTypeExportDriver( IObjectExportTypeDriver<TItem> itemExporter )
    {
        _itemExporter = itemExporter;
    }

    public Type BaseType => typeof( IObservableReadOnlySet<TItem> );

    public bool IsDefaultBehavior => false;

    public IReadOnlyList<PropertyInfo> ExportableProperties => [];

    public void Export( IObservableReadOnlySet<TItem> o, int num, ObjectExporter exporter )
    {
        Throw.CheckNotNullArgument( exporter );
        exporter.ExportSet( num, o, null );
    }

    void IObjectExportTypeDriver.Export( object o, int num, ObjectExporter exporter ) => Export( (IObservableReadOnlySet<TItem>)o, num, exporter );
}
