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
        readonly List<Func<IActivityMonitor, Task>> _postActions;

        /// <summary>
        /// Gets the observable domain.
        /// </summary>
        public ObservableDomain ObservableDomain { get; }

        /// <summary>
        /// Gets the time (UTC) of the transaction commit.
        /// </summary>
        public DateTime TimeUtc { get; }

        /// <summary>
        /// Gets the events that the transaction generated (all <see cref="ObservableObject"/> changes).
        /// Can be empty.
        /// </summary>
        public IReadOnlyList<ObservableEvent> Events { get; }

        /// <summary>
        /// Gets the commands that the transaction generated (all the commands
        /// sent via <see cref="ObservableObject.SendCommand"/>.
        /// </summary>
        public IReadOnlyList<ObservableCommand> Commands { get; }

        /// <summary>
        /// Exposes all post actions that must be executed and has been registered so far.
        /// </summary>
        public IReadOnlyList<Func<IActivityMonitor, Task>> PostActions => _postActions;

        /// <summary>
        /// Registers a new action that must be executed.
        /// </summary>
        /// <param name="action"></param>
        public void AddPostAction( Func<IActivityMonitor, Task> action )
        {
            if( action == null ) throw new ArgumentNullException( nameof( action ) );
            _postActions.Add( action );
        }

        internal SuccessfulTransactionContext( ObservableDomain d, IReadOnlyList<ObservableEvent> e, IReadOnlyList<ObservableCommand> c )
        {
            _postActions = new List<Func<IActivityMonitor, Task>>();
            ObservableDomain = d;
            TimeUtc = DateTime.UtcNow;
            Events = e;
            Commands = c;
        }

    }
}
