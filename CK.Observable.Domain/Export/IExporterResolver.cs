using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    public interface IExporterResolver
    {
        /// <summary>
        /// Finds an export driver for a Type or null if the type is not exportable.
        /// </summary>
        /// <param name="t">The type for which a driver must be found. Can be null: null is returned.</param>
        /// <returns>The driver or null if the type is not exportable.</returns>
        IObjectExportTypeDriver FindDriver( Type t );

        /// <summary>
        /// Finds an export driver for a Type or null if the type is not exportable.
        /// </summary>
        /// <typeparam name="T">The type for which a driver must be found.</typeparam>
        /// <returns>The driver or null if the type is not exportable.</returns>
        IObjectExportTypeDriver<T> FindDriver<T>();

    }
}
