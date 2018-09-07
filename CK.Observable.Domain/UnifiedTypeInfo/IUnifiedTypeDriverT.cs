using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    public interface IUnifiedTypeDriver<T> : IUnifiedTypeDriver, ITypeSerializationDriver<T>, IDeserializationDriver<T>, IObjectExportTypeDriver<T>
    {
        /// <summary>
        /// Gets the serialization driver.
        /// Null if no serialization driver is available.
        /// </summary>
        new ITypeSerializationDriver<T> SerializationDriver { get; }

        /// <summary>
        /// Gets the deserialization driver.
        /// Null if no deserialization driver is available.
        /// </summary>
        new IDeserializationDriver<T> DeserializationDriver { get; }

        /// <summary>
        /// Gets the export driver.
        /// Null if no export driver is available.
        /// </summary>
        new IObjectExportTypeDriver<T> ExportDriver { get; }
    }
}
