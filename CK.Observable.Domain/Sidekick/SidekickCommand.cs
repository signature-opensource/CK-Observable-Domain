using CK.Core;
using System;

namespace CK.Observable
{
    /// <summary>
    /// Command to be executed by <see cref="ObservableDomainSidekick.ExecuteCommand"/>.
    /// </summary>
    public readonly struct SidekickCommand
    {
        /// <summary>
        /// Gets the start time (UTC) of the transaction.
        /// </summary>
        public DateTime StartTimeUtc { get; }

        /// <summary>
        /// Gets the time (UTC) of the transaction commit.
        /// </summary>
        public DateTime CommitTimeUtc { get; }

        /// <summary>
        /// Gets the command payload .
        /// </summary>
        public object Command { get; }

        /// <summary>
        /// Registrar for actions that will be executed sequentially but independently of any other
        /// actions issued by other transactions.
        /// </summary>
        public IActionRegistrar<PostActionContext> PostActions { get; }

        /// <summary>
        /// Registrar for actions (that can be synchronous as well as asynchronous) that must be executed after
        /// the transaction itself, respecting the order of other set of <see cref="DomainPostActions"/> submitted by
        /// other (concurrent) transactions on this domain.
        /// </summary>
        public IActionRegistrar<PostActionContext> DomainPostActions { get; }

        internal SidekickCommand( DateTime s, DateTime c, object cmd, IActionRegistrar<PostActionContext> local, IActionRegistrar<PostActionContext> domain )
        {
            StartTimeUtc = s;
            CommitTimeUtc = c;
            Command = cmd;
            PostActions = local;
            DomainPostActions = domain;
        }
    }

}
