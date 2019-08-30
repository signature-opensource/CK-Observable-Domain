using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace CK.Observable
{
    class EnumTypeExporter<T, TU> : IObjectExportTypeDriver<T>
        where T : Enum
    {
        readonly IObjectExportTypeDriver<TU> _underlyingType;

        public EnumTypeExporter( IObjectExportTypeDriver<TU> underlyingType )
        {
            Debug.Assert( underlyingType != null );
            Debug.Assert( Enum.GetUnderlyingType( typeof( T ) ) == underlyingType.BaseType );
            _underlyingType = underlyingType;
        }

        public Type BaseType => typeof( T );

        public bool IsDefaultBehavior => false;

        IReadOnlyList<PropertyInfo> IObjectExportTypeDriver.ExportableProperties => Array.Empty<PropertyInfo>();

        public void Export( object o, int num, ObjectExporter exporter ) => _underlyingType.Export( (TU)o, num, exporter );

        void IObjectExportTypeDriver<T>.Export( T o, int num, ObjectExporter exporter ) => Export( o, num, exporter );

    }
}
