using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    public interface IObjectExportTypeDriver<in T> : IObjectExportTypeDriver
    {
        /// <summary>
        /// Exports an instance.
        /// </summary>
        /// <param name="o">The object instance. Must not ne null.</param>
        /// <param name="num">The reference number for this object. -1 for value type.</param>
        /// <param name="exporter">The exporter.</param>
        void Export( T o, int num, ObjectExporter exporter );
    }
}
