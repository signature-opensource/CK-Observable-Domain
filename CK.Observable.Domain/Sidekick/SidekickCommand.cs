using CK.Core;
using System;

namespace CK.Observable
{
    /// <summary>
    /// Command to be executed by <see cref="SidekickBase.ExecuteCommand"/>.
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
        /// Registrar for actions (that can be synchronous as well as asynchronous) that must be executed.
        /// </summary>
        public IActionRegistrar<PostActionContext> PostActions { get; }

        internal SidekickCommand( DateTime s, DateTime c, object cmd, IActionRegistrar<PostActionContext> a )
        {
            StartTimeUtc = s;
            CommitTimeUtc = c;
            Command = cmd;
            PostActions = a;
        }
    }

}
