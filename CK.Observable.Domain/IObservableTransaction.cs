using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    /// <summary>
    /// Obtaining this interface is required to modify a <see cref="ObservableDomain"/>.
    /// </summary>
    public interface IObservableTransaction : IDisposable
    {
        /// <summary>
        /// Commits all changes and retrieves the event list.
        /// </summary>
        /// <returns>The event list.</returns>
        IReadOnlyList<ObservableEvent> Commit();

        /// <summary>
        /// Gets any errors that have been added by <see cref="AddError"/>.
        /// </summary>
        IReadOnlyList<CKExceptionData> Errors { get; }

        /// <summary>
        /// Adds an error to this transaction.
        /// This prevents this transaction to be successfully committed; calling <see cref="Commit"/>
        /// will be the same as disposing this transaction without committing.
        /// </summary>
        /// <param name="d">An exception data.</param>
        void AddError( CKExceptionData d );
    }
}
