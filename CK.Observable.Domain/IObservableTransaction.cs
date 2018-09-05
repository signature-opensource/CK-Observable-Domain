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
    }
}
