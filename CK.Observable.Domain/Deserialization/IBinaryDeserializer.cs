using CK.Core;
using System;

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
        /// Reads a <see cref="Type"/> written by <see cref="BinarySerializer.Write(Type)"/>.
        /// </summary>
        /// <param name="throwIfMissing">
        /// By default a <see cref="TypeLoadException"/> is thrown if the Type cannot be resolved.
        /// False to simply return null.
        /// </param>
        /// <returns>The Type.</returns>
        Type ReadType( bool throwIfMissing = true );

        /// <summary>
        /// Reads an object previously written by <see cref="BinarySerializer.Write{T}(T,ITypeSerializationDriver{T})"/>.
        /// </summary>
        /// <param name="driver">The deserialization driver.</param>
        /// <returns>The object read, possibly in an intermediate state.</returns>
        T Read<T>( IDeserializationDriver<T> driver );

        /// <summary>
        /// Gets a set of low level methods and helpers.
        /// </summary>
        IBinaryDeserializerImpl ImplementationServices { get; }
    }
}
