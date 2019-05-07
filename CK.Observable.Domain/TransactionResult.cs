using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    /// <summary>
    /// Encapsulates the result of a <see cref="ObservableDomain.Transaction.Commit"/>
    /// and <see cref="ObservableDomain.Modify(Action)"/>.
    /// </summary>
    public readonly struct TransactionResult
    {
        /// <summary>
        /// The empty transaction result with no events and no commands: both lists are empty.
        /// </summary>
        public static readonly TransactionResult Empty = new TransactionResult( Array.Empty<CKExceptionData>() );

        /// <summary>
        /// Gets the events that the transaction generated (all <see cref="ObservableObject"/> changes).
        /// Can be empty (and always empty if there are <see cref="Errors"/>).
        /// </summary>
        public readonly IReadOnlyList<ObservableEvent> Events;

        /// <summary>
        /// Gets the commands that the transaction generated (all the commands
        /// sent via <see cref="ObservableObject.SendCommand"/>.
        /// Can be empty (and always empty if there are <see cref="Errors"/>).
        /// </summary>
        public readonly IReadOnlyList<object> Commands;

        /// <summary>
        /// Gets the errors that actually aborted the transaction.
        /// This is empty on success.
        /// </summary>
        public readonly IReadOnlyList<CKExceptionData> Errors;

        internal TransactionResult( IReadOnlyList<ObservableEvent> e, IReadOnlyList<object> c )
        {
            Events = e;
            Commands = c;
            Errors = Array.Empty<CKExceptionData>();
        }

        internal TransactionResult( IReadOnlyList<CKExceptionData> errors )
        {
            Errors = errors;
            Events = Array.Empty<ObservableEvent>();
            Commands = Array.Empty<object>();
        }

    }
}
