using CK.Core;
using System;
using System.Collections.Generic;

namespace CK.Observable
{
    /// <summary>
    /// Extends binary reader to handle objects deserialization.
    /// </summary>
    public interface IBinaryDeserializer : ICKBinaryReader
    {
        /// <summary>
        /// Gets a configurable container of services available for constructor
        /// injection in the deserialized instances.
        /// </summary>
       IServiceProvider Services { get; }

        /// <summary>
        /// Reads an object previously written by <see cref="BinarySerializer.WriteObject(object)"/>.
        /// </summary>
        /// <returns>The object read, possibly in an intermediate state.</returns>
        object ReadObject();

        /// <summary>
        /// Reads an array of objects that have been previously written
        /// by <see cref="BinarySerializer.WriteObjects(int, System.Collections.IEnumerable)"/>.
        /// </summary>
        /// <returns>The object array.</returns>
        T[] ReadObjectArray<T>();

        /// <summary>
        /// Reads a list of objects that have been previously written
        /// by <see cref="BinarySerializer.WriteObjects(int, System.Collections.IEnumerable)"/>.
        /// </summary>
        /// <returns>The object list.</returns>
        List<T> ReadObjectList<T>();

        /// <summary>
        /// Gets a set of low level methods and helpers.
        /// </summary>
        IBinaryDeserializerImpl ImplementationServices { get; }
    }
}
