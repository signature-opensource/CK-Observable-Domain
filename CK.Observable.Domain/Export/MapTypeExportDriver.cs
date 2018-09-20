using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
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

        public IReadOnlyList<PropertyInfo> ExportableProperties => Array.Empty<PropertyInfo>();

        public void Export( IEnumerable<KeyValuePair<TKey, TValue>> o, int num, ObjectExporter exporter )
        {
            if( exporter == null ) throw new ArgumentNullException( nameof( exporter ) );
            exporter.ExportMap( num, o, _keyExporter, _valueExporter );
        }

        void IObjectExportTypeDriver.Export( object o, int num, ObjectExporter exporter ) => Export( (IEnumerable<KeyValuePair<TKey, TValue>>)o, num, exporter );
    }
}
