using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable;

/// <summary>
/// The <see cref="IObservableDomain.AllObjects"/> property's.
/// <para>
/// This collection is not exposed directly as a collection (you must use <see cref="Items"/>) and is not exposed as all on the
/// <see cref="DomainView"/>, this is because enumerating the whole set of Observable Objects should be done only in tests, there
/// should always be a "natural path" to retrieve one object or a set of objects.
/// </para>
/// </summary>
public interface IObservableAllObjectsCollection
{
    /// <summary>
    /// Gets the number of elements in the collection.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets all the observable objects.
    /// <para>
    /// If you are using this with where filtering or other for each enumeration, then something
    /// is wrong in your object model: objects should always be accessible from a domain's root
    /// (or roots) through list, set or dictionaries.
    /// </para>
    /// </summary>
    IReadOnlyCollection<ObservableObject> Items { get; }

    /// <summary>
    /// Tries to get the <see cref="ObservableObject"/> if it exists (null otherwise).
    /// </summary>
    /// <param name="id">The object identifier.</param>
    /// <returns>The object or null if not found.</returns>
    ObservableObject? Get( ObservableObjectId id );

    /// <inheritdoc cref="Get(ObservableObjectId)"/>
    ObservableObject? Get( long id );

    /// <inheritdoc cref="Get(ObservableObjectId)"/>
    ObservableObject? Get( double id );

    /// <summary>
    /// Gets the <see cref="ObservableObject"/> or throws a <see cref="KeyNotFoundException"/>.
    /// </summary>
    /// <param name="id">The object identifier.</param>
    /// <returns>The object.</returns>
    ObservableObject GetRequired( ObservableObjectId id );

    /// <inheritdoc cref="GetRequired(ObservableObjectId)"/>
    ObservableObject GetRequired( long id );

    /// <inheritdoc cref="GetRequired(ObservableObjectId)"/>
    ObservableObject GetRequired( double id );

    /// <summary>
    /// Gets the typed <see cref="ObservableObject"/> if it exists (null otherwise).
    /// If the type is not the expected one, a <see cref="InvalidCastException"/> is thrown by default.
    /// </summary>
    /// <typeparam name="T">The expected object type.</typeparam>
    /// <param name="id">The object identifier.</param>
    /// <param name="throwOnTypeMismacth">
    /// False to return null if the object exists but its type is not <typeparamref name="T"/>.
    /// By default an exception is thrown if the object exists.
    /// </param>
    /// <returns>The object or null if not found.</returns>
    T? Get<T>( ObservableObjectId id, bool throwOnTypeMismacth = true ) where T : ObservableObject;

    /// <inheritdoc cref="Get{T}(ObservableObjectId, bool)"/>
    T? Get<T>( long id, bool throwOnTypeMismacth = true ) where T : ObservableObject;

    /// <inheritdoc cref="Get{T}(ObservableObjectId, bool)"/>
    T? Get<T>( double id, bool throwOnTypeMismacth = true ) where T : ObservableObject;

    /// <summary>
    /// Tries to get the typed <see cref="ObservableObject"/> if it exists (null if it doesn't exist
    /// or if the type doesn't match).
    /// </summary>
    /// <typeparam name="T">The expected object type.</typeparam>
    /// <param name="id">The object identifier.</param>
    /// <returns>The object or null if not found or the type doesn't match.</returns>
    T? Get<T>( ObservableObjectId id ) where T : ObservableObject;

    /// <inheritdoc cref="Get{T}(ObservableObjectId)"/>
    T? Get<T>( long id ) where T : ObservableObject;

    /// <inheritdoc cref="Get{T}(ObservableObjectId)"/>
    T? Get<T>( double id ) where T : ObservableObject;

    /// <summary>
    /// Gets the typed <see cref="ObservableObject"/> or throws:
    /// <list type="bullet">
    ///   <item><see cref="KeyNotFoundException"/> if the object doesn't exist.</item>
    ///   <item><see cref="InvalidCastException"/> if the object exists but type doesn't match.</item>
    /// </list>.
    /// </summary>
    /// <typeparam name="T">The expected object type.</typeparam>
    /// <param name="id">The object identifier.</param>
    /// <returns>The typed object.</returns>
    T GetRequired<T>( ObservableObjectId id ) where T : ObservableObject;

    /// <inheritdoc cref="GetRequired{T}(ObservableObjectId)"/>
    T GetRequired<T>( long id ) where T : ObservableObject;

    /// <inheritdoc cref="GetRequired{T}(ObservableObjectId)"/>
    T GetRequired<T>( double id ) where T : ObservableObject;
}
