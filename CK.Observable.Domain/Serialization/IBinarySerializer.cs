using CK.Core;
using System;
using System.Runtime.CompilerServices;

namespace CK.Observable
{
    public interface IBinarySerializer : ICKBinaryWriter
    {
        /// <summary>
        /// Writes an object that can be null and of any type.
        /// </summary>
        /// <param name="o">The object to write.</param>
        /// <returns>
        /// True if it has been written, false if the object has already been
        /// written and only a reference has been written.
        /// </returns>
        bool WriteObject( object? o );

        /// <summary>
        /// Writes a type.
        /// </summary>
        /// <param name="t">The type to write. Can be null.</param>
        void Write( Type? t );

        /// <summary>
        /// Gets a set of low level methods and helpers.
        /// </summary>
        IBinarySerializerImpl ImplementationServices { get; }

        /// <summary>
        /// Gets whether this serializer is currently in debug mode.
        /// Initially defaults to false.
        /// </summary>
        bool IsDebugMode { get; }

        /// <summary>
        /// Activates or deactivates the debug mode. This is cumulative so that scoped activations are handled:
        /// activation/deactivation should be paired and <see cref="IsDebugMode"/> must be used to
        /// know whether debug mode is actually active.
        /// </summary>
        /// <param name="active">Whether the debug mode should be activated, deactivated or (when null) be left as it is.</param>
        void DebugWriteMode( bool? active );

        /// <summary>
        /// Writes a sentinel that must be read back by <see cref="IBinaryDeserializer.DebugCheckSentinel"/>.
        /// If <see cref="IsDebugMode"/> is false, nothing is written.
        /// </summary>
        /// <param name="fileName">Current file name that wrote the data. Used to build the <see cref="InvalidDataException"/> message if sentinel cannot be read back.</param>
        /// <param name="line">Current line number that wrote the data. Used to build the <see cref="InvalidDataException"/> message if sentinel cannot be read back.</param>
        void DebugWriteSentinel( [CallerFilePath] string? fileName = null, [CallerLineNumber] int line = 0 );

    }
}
