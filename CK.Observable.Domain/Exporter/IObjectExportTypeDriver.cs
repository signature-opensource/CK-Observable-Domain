using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    public interface IObjectExportTypeDriver
    {
        /// <summary>
        /// Gets the type that this driver handles.
        /// </summary>
        Type Type { get; }

        /// <summary>
        /// Gets whether <see cref="Export"/> can be called: this driver knows hox to export
        /// instances of its type.
        /// </summary>
        bool IsExportable { get; }

        /// <summary>
        /// Exports an instance. <see cref="IsExportable"/> must be true otherwise a <see cref="NotSupportedException"/>
        /// must be thrown.
        /// </summary>
        /// <param name="o">The object instance. Must not ne null.</param>
        /// <param name="num">The reference number for this object. -1 for value type.</param>
        /// <param name="exporter">The exporter.</param>
        void Export( object o, int num, ObjectExporter exporter );
    }
}
