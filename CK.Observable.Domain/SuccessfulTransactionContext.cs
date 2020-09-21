using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace CK.Observable
{
    /// <summary>
    /// Encapsulates the result of a successful <see cref="ObservableDomain.Transaction.Commit"/>.
    /// </summary>
    public readonly struct SuccessfulTransactionContext
    {
        readonly ObservableDomain _domain;
        internal readonly ActionRegistrar<PostActionContext> _postActions;

        /// <summary>
        /// Gets the monitor to use.
        /// </summary>
        public IActivityMonitor Monitor => _domain.CurrentMonitor;

        /// <summary>
        /// Gets the observable domain.
        /// </summary>
        public IObservableDomain Domain => _domain;

        /// <summary>
        /// Gets the start time (UTC) of the transaction.
        /// </summary>
        public DateTime StartTimeUtc { get; }

        /// <summary>
        /// Gets the time (UTC) of the transaction commit.
        /// </summary>
        public DateTime CommitTimeUtc { get; }

        /// <summary>
        /// Gets the next due time (UTC) of the <see cref="ObservableTimedEventBase"/>.
        /// </summary>
        public DateTime NextDueTimeUtc { get; }

        /// <summary>
        /// Gets the events that the transaction generated (all <see cref="ObservableObject"/> changes).
        /// Can be empty.
        /// </summary>
        public IReadOnlyList<ObservableEvent> Events { get; }

        /// <summary>
        /// Gets the commands that the transaction generated (all the commands
        /// sent via <see cref="DomainView.SendCommand(object)"/>.
        /// </summary>
        public IReadOnlyList<object> Commands { get; }

        /// <summary>
        /// Registrar for actions (that can be synchronous as well as asynchronous) that must be executed.
        /// </summary>
        public IActionRegistrar<PostActionContext> PostActions => _postActions;

        internal SuccessfulTransactionContext( ObservableDomain d, IReadOnlyList<ObservableEvent> e, IReadOnlyList<object> c, DateTime startTime, DateTime nextDueTime )
        {
            _postActions = new ActionRegistrar<PostActionContext>();
            _domain = d;
            NextDueTimeUtc = nextDueTime;
            StartTimeUtc = startTime;
            CommitTimeUtc = DateTime.UtcNow;
            Events = e;
            Commands = c;
        }

    }
}
