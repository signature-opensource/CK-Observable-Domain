using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable
{
    /// <summary>
    /// Captures any <see cref="Command"/> payload along with hints on how it must be handled.
    /// </summary>
    public readonly struct ObservableDomainCommand 
    {
        /// <summary>
        /// Gets the command payload.
        /// </summary>
        public object Command { get; }

        /// <summary>
        /// Gets the optional known <see cref="ObservableDomainSidekick"/> or the type of the <see cref="ObservableDomainSidekick"/>
        /// that must handle this command.
        /// When set, the sidekick instance associated to the current domain will be the only one that will receive this command.
        /// When let to null, all existing sidekicks will have the opportunity to handle it. In this "braodcast mode", if
        /// all <see cref="ObservableDomainSidekick.ExecuteCommand(Core.IActivityMonitor, in SidekickCommand)"/> returned false,
        /// the command is considered unhandled: see <see cref="IsOptionalExecution"/>.
        /// </summary>
        public object? KnownTarget { get; }

        /// <summary>
        /// Gets whether not finding a target that executes this command is a Warning rather than an error.
        /// When <see cref="KnownTarget"/> is null ("broadcast mode"), at least one sidekick must have handled the command
        /// (at least one <see cref="ObservableDomainSidekick.ExecuteCommand(Core.IActivityMonitor, in SidekickCommand)"/> must return true).
        /// When the <see cref="KnownTarget"/> is set, it must exist AND its ExecuteCommand method must have returned true.
        /// <para>
        /// This defaults to false: an unhandled command is an error.
        /// </para>
        /// </summary>
        public bool IsOptionalExecution { get; }

        /// <summary>
        /// Initializes a new <see cref="ObservableDomainCommand"/>.
        /// </summary>
        /// <param name="command">The command payload.</param>
        /// <param name="targetSidekick">The optional target sidekick.</param>
        /// <param name="isOptionalExecution">See <see cref="IsOptionalExecution"/>.</param>
        public ObservableDomainCommand( object command, object? knownTarget = null, bool isOptionalExecution = false )
        {
            Command = command ?? throw new ArgumentNullException( nameof( command ) );
            KnownTarget = knownTarget;
            IsOptionalExecution = isOptionalExecution;
        }
    }
}
