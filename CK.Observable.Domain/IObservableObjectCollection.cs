using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable
{
    public interface IObservableObjectCollection : IReadOnlyCollection<ObservableObject>
    {
        /// <summary>
        /// Finds the <see cref="ObservableObject"/> by its <see cref="ObservableObject.OId"/>.
        /// Null if not found.
        /// </summary>
        /// <param name="id">The object identifier.</param>
        /// <returns>The object or null.</returns>
        ObservableObject this[long id] { get; }

        /// <summary>
        /// Gets the typed <see cref="ObservableObject"/> if it exists (null otherwise).
        /// If the type is not the expected one, an exception is thrown by default.
        /// </summary>
        /// <typeparam name="T">The expected object type.</typeparam>
        /// <param name="id">The object identifier.</param>
        /// <returns>The object or null if not found.</returns>
        T Get<T>( long id, bool throwOnTypeMismacth = true ) where T : ObservableObject;
    }
}
