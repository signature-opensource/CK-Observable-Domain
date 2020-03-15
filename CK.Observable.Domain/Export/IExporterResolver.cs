using System;

namespace CK.Observable
{
    /// <summary>
    /// Finds <see cref="IObjectExportTypeDriver"/> for a type.
    /// </summary>
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
