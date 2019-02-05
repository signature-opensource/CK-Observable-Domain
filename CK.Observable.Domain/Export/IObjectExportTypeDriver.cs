using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    /// <summary>
    /// Export driver.
    /// </summary>
    public interface IObjectExportTypeDriver
    {
        /// <summary>
        /// Gets the base type that this driver handles.
        /// </summary>
        Type BaseType { get; }

        /// <summary>
        /// Gets whether the export is done by exporting the <see cref="ExportableProperties"/>.
        /// </summary>
        bool IsDefaultBehavior { get; }

        /// <summary>
        /// Exports an instance.
        /// </summary>
        /// <param name="o">The object instance. Must not ne null.</param>
        /// <param name="num">The reference number for this object. -1 for value type.</param>
        /// <param name="exporter">The exporter.</param>
        void Export( object o, int num, ObjectExporter exporter );

        /// <summary>
        /// Gets the property that must be exported.
        /// This is empty for basic types and for Type that has a private Export( int, <see cref="ObjectExporter"/> )
        /// method that takes control of the export.
        /// </summary>
        IReadOnlyList<PropertyInfo> ExportableProperties { get; }
    }
}
