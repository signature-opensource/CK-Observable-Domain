using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable
{
    /// <summary>
    /// The <see cref="ObservableDomain.AllObjects"/> property's type is a <see cref="IReadOnlyCollection{ObjectCollection}"/>
    /// that offers indexed access by <see cref="this[long]"/> or <see cref="this[ObservableObjectId]"/> and with a typed filter.
    /// </summary>
    public interface IObservableAllObjectsCollection : IReadOnlyCollection<ObservableObject>
    {
        /// <summary>
        /// Finds the <see cref="ObservableObject"/> by its <see cref="ObservableObject.OId"/>.
        /// Null if not found.
        /// </summary>
        /// <param name="id">The object identifier.</param>
        /// <returns>The object or null.</returns>
        ObservableObject? this[long id] { get; }

        /// <summary>
        /// Finds the <see cref="ObservableObject"/> by its <see cref="ObservableObject.OId"/>.
        /// Null if not found.
        /// </summary>
        /// <param name="id">The object identifier.</param>
        /// <returns>The object or null.</returns>
        ObservableObject? this[ObservableObjectId id] { get; }

        /// <inheritdoc cref="this[long]"/>
        ObservableObject? this[double id] { get; }

        /// <summary>
        /// Gets the typed <see cref="ObservableObject"/> if it exists (null otherwise).
        /// If the type is not the expected one, an exception is thrown by default.
        /// </summary>
        /// <typeparam name="T">The expected object type.</typeparam>
        /// <param name="id">The object identifier.</param>
        /// <param name="throwOnTypeMismacth">
        /// False to return null if the object exists but its type is not <typeparamref name="T"/>.
        /// By default an exception is thrown if the object exists.
        /// </param>
        /// <returns>The object or null if not found.</returns>
        T? Get<T>( long id, bool throwOnTypeMismacth = true ) where T : ObservableObject;

        /// <inheritdoc cref="Get{T}(long, bool)"/>
        T? Get<T>( double id, bool throwOnTypeMismacth = true ) where T : ObservableObject;

        /// <summary>
        /// Gets the typed <see cref="ObservableObject"/> if it exists (null otherwise).
        /// If the type is not the expected one, an exception is thrown by default.
        /// </summary>
        /// <typeparam name="T">The expected object type.</typeparam>
        /// <param name="id">The object identifier.</param>
        /// <param name="throwOnTypeMismacth">
        /// False to return null if the object exists but its type is not <typeparamref name="T"/>.
        /// By default an exception is thrown if the object exists.
        /// </param>
        /// <returns>The object or null if not found.</returns>
        T? Get<T>( ObservableObjectId id, bool throwOnTypeMismacth = true ) where T : ObservableObject;
    }
}
