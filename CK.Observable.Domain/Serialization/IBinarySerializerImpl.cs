using CK.Core;
using System;
using System.Runtime.CompilerServices;

namespace CK.Observable
{
    public interface IBinarySerializerImpl
    {
        /// <summary>
        /// Gets the serialization drivers.
        /// </summary>
        ISerializerResolver Drivers { get; }

        /// <summary>
        /// Registers a reference: returns true if it's a new one that must be written.
        /// If the object has already been written, this writes the reference marker and returns false.
        /// </summary>
        /// <typeparam name="T">Type must be a non null reference type.</typeparam>
        /// <param name="o">The object.</param>
        /// <returns>
        /// True if the object must been written, false if the object has already been
        /// written (a reference has been written).
        /// </returns>
        bool WriteNewObject<T>( T o ) where T : class;

        /// <summary>
        /// Called by <see cref="AutoTypeRegistry"/> serialization drivers when a disposed <see cref="IDestroyable"/> has been
        /// written.
        /// <para>
        /// This should clearly be on "ImplementationServices" or any other of this writer extensions. But currently, the
        /// serialization is embedded inside the Observable library, so we don't care.
        /// Note that if a IDestroyableObject { bool IsDestroyed { get; } } basic interface (without Destroyed event) in the "generic" serialization library
        /// (or deeper? "System.ComponentModel.IDestroyableObject, CK.Core"?), then this could remain this way. 
        /// </para>
        /// </summary>
        Action<IDestroyable>? DisposedTracker { get; }

    }
}
