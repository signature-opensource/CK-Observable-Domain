using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{

    public interface IObservableTransactionManager
    {
        /// <summary>
        /// Called before a transaction starts.
        /// </summary>
        /// <param name="d">The associated domain.</param>
        void OnTransactionStart( ObservableDomain d );

        /// <summary>
        /// Called when a transaction ends successfully.
        /// </summary>
        /// <param name="d">The associated domain.</param>
        /// <param name="events">The events of the transaction. Can be empty.</param>
        void OnTransactionCommit( ObservableDomain d, IReadOnlyList<IObservableEvent> events );

        /// <summary>
        /// Called when an error occured in a transaction. 
        /// </summary>
        /// <param name="d">The associated domain.</param>
        void OnTransactionFailure( ObservableDomain d );
    }
}
