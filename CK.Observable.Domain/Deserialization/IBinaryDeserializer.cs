using CK.Core;
using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace CK.Observable
{
    /// <summary>
    /// Extends binary reader to handle objects deserialization.
    /// </summary>
    public interface IBinaryDeserializer : ICKBinaryReader
    {
        /// <summary>
        /// Gets a global serialization version.
        /// This version is handled by the concrete serializer and deserializer and applies
        /// to all "intrinsic" objects that are handled by the code base.
        /// </summary>
        int SerializationVersion { get; }

        /// <summary>
        /// Gets a configurable container of services available for constructor
        /// injection in the deserialized instances.
        /// </summary>
        IServiceProvider Services { get; }

        /// <summary>
        /// Reads an object previously written by <see cref="BinarySerializer.WriteObject(object?)"/>.
        /// </summary>
        /// <returns>The object read, possibly in an intermediate state.</returns>
        object? ReadObject();

        /// <summary>
        /// Reads a <see cref="Type"/> written by <see cref="BinarySerializer.Write(Type)"/>.
        /// </summary>
        /// <param name="throwIfMissing">
        /// By default a <see cref="TypeLoadException"/> is thrown if the Type cannot be resolved.
        /// False to simply return null.
        /// </param>
        /// <returns>The Type.</returns>
        Type? ReadType( bool throwIfMissing = true );

        /// <summary>
        /// Gets a set of low level methods and helpers.
        /// </summary>
        IBinaryDeserializerImpl ImplementationServices { get; }

        /// <summary>
        /// Reads an expected string and throws an <see cref="InvalidDataException"/> if it cannot be read.
        /// This is typically used if (and when) <see cref="IsDebugMode"/> is true but can be used independently.
        /// </summary>
        /// <param name="expected">The expected string to read. It cannot be null, empty or whitespace.</param>
        void ReadString( string expected );

        /// <summary>
        /// Gets whether this deserializer is currently in debug mode.
        /// </summary>
        bool IsDebugMode { get; }

        /// <summary>
        /// Updates the current debug mode that must have been written by <see cref="BinarySerializer.DebugWriteMode(bool?)"/>.
        /// </summary>
        /// <returns>Whether the debug mode is currently active or not.</returns>
        bool DebugReadMode();

        /// <summary>
        /// Checks the existence of a sentinel written by <see cref="BinarySerializer.DebugWriteSentinel"/>.
        /// An <see cref="InvalidDataException"/> is thrown if <see cref="IsDebugMode"/> is true and the sentinel cannot be read.
        /// </summary>
        /// <param name="fileName">Current file name used to build the <see cref="InvalidDataException"/> message if sentinel cannot be read back.</param>
        /// <param name="line">Current line number used to build the <see cref="InvalidDataException"/> message if sentinel cannot be read back.</param>
        void DebugCheckSentinel( [CallerFilePath]string? fileName = null, [CallerLineNumber] int line = 0 );

        /// <summary>
        /// When <see cref="IsDebugMode"/> is true, records the <paramref name="ctx"/> in a stack
        /// that will be dumped on error and returns a disposable to pop the stack.
        /// When <see cref="IsDebugMode"/> is false, returns null.
        /// </summary>
        /// <param name="ctx">The stacked message.</param>
        /// <returns>A disposable that will pop the message or null is not in debug mode.</returns>
        IDisposable? OpenDebugPushContext( string ctx );

    }
}
