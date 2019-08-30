using System;

namespace CK.Observable
{
    public interface ISerializerResolver
    {
        /// <summary>
        /// Finds a serialization driver for a Type.
        /// </summary>
        /// <typeparam name="T">The type for which a driver must be found.</typeparam>
        /// <returns>Null if the type is null, the driver otherwise.</returns>
        ITypeSerializationDriver<T> FindDriver<T>();

        /// <summary>
        /// Finds a serialization driver for a Type.
        /// </summary>
        /// <param name="t">The type for which a driver must be found. Can be null: null is returned.</param>
        /// <returns>Null if the type is null, the driver otherwise.</returns>
        ITypeSerializationDriver FindDriver( Type t );
    }
}
