using System;

namespace CK.Observable
{
    /// <summary>
    /// Unified driver of type gives access to Serialization, Deserialization and Export drivers for a type.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IUnifiedTypeDriver<T> : IUnifiedTypeDriver
    {
        /// <summary>
        /// Gets the type handled.
        /// </summary>
        Type Type { get; }

        /// <summary>
        /// Gets the serialization driver.
        /// Null if no serialization driver is available.
        /// </summary>
        new ITypeSerializationDriver<T>? SerializationDriver { get; }

        /// <summary>
        /// Gets the deserialization driver.
        /// Null if no deserialization driver is available.
        /// </summary>
        new IDeserializationDriver<T>? DeserializationDriver { get; }

        /// <summary>
        /// Gets the export driver.
        /// Null if no export driver is available.
        /// </summary>
        new IObjectExportTypeDriver<T>? ExportDriver { get; }
    }
}
