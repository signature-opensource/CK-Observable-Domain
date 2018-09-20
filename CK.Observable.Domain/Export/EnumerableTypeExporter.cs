using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    class EnumerableTypeExporter<T> : IObjectExportTypeDriver<IEnumerable<T>>
    {
        readonly IObjectExportTypeDriver<T> _itemExporter;

        public EnumerableTypeExporter( IObjectExportTypeDriver<T> itemExporter )
        {
            _itemExporter = itemExporter;
        }

        public Type BaseType => typeof( IEnumerable<T> );

        public IReadOnlyList<PropertyInfo> ExportableProperties => Array.Empty<PropertyInfo>();

        public void Export( IEnumerable<T> o, int num, ObjectExporter exporter )
        {
            if( _itemExporter != null )
            {
                exporter.Target.EmitStartObject( num, ObjectExportedKind.List );
                foreach( var item in o ) exporter.Export( item, _itemExporter );
                exporter.Target.EmitEndObject( num, ObjectExportedKind.List );
            }
            else exporter.ExportList( num, o );
        }

        void IObjectExportTypeDriver.Export( object o, int num, ObjectExporter exporter ) => Export( (IEnumerable<T>)o, num, exporter );
    }
}
