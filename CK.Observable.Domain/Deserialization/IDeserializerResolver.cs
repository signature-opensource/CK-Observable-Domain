using System;

namespace CK.Observable
{
    public interface IDeserializerResolver
    {
        /// <summary>
        /// Tries to find a deserialization driver for a type name or, as a last resort,
        /// from a Type that may be resolved locally and for which a driver can be built automatically.
        /// Returns null if not found.
        /// </summary>
        /// <param name="name">Name to resolve.</param>
        /// <param name="lastResort">
        /// Optional function that may provide a locally available Type.
        /// If this function resturns null, the returned deserialization driver will be null.
        /// </param>
        /// <returns>Null or the deserialization driver to use.</returns>
        IDeserializationDriver FindDriver( string name, Func<Type> lastResort = null );

        /// <summary>
        /// Tries to find a deserialization driver for a local type.
        /// Returns null if not found.
        /// </summary>
        /// <typeparam name="T">Type for which a deserialization driver must be found.</typeparam>
        /// <returns>Null or the deserialization driver to use.</returns>
        IDeserializationDriver<T> FindDriver<T>();
    }
}
