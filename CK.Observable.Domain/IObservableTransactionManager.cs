using CK.Core;
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
        /// <param name="timeUtc">The date time utc of the commit.</param>
        /// <param name="events">The events of the transaction. Can be empty.</param>
        void OnTransactionCommit( ObservableDomain d, DateTime timeUtc, IReadOnlyList<ObservableEvent> events );

        /// <summary>
        /// Called when an error occurred in a transaction.
        /// </summary>
        /// <param name="d">The associated domain.</param>
        /// <param name="errors">
        /// A necessarily non null list of errors with at least one error.
        /// 
        /// </param>
        void OnTransactionFailure( ObservableDomain d, IReadOnlyList<CKExceptionData> errors );
    }
}
