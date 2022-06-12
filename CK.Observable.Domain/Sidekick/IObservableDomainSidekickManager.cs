using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    /// <summary>
    /// Manager of <see cref="Domain"/> <see cref="ObservableDomainSidekick"/> objects.
    /// </summary>
    public interface IObservableDomainSidekickManager
    {
        /// <summary>
        /// Gets the domain.
        /// <para>
        /// Caution: this is the real domain object, not the restricted <see cref="DomainView"/> that is accessible
        /// from Observable or Internal objects.
        /// </para>
        /// </summary>
        ObservableDomain Domain { get; }

        /// <summary>
        /// Exposed by <see cref="IObservableDomainSidekickManager.DeserializationInfo"/>.
        /// </summary>
        public interface IDeserializationInfo
        {
            /// <summary>
            /// Gets the amount of time during which the domain has been unloaded.
            /// This is DateTime.UtcNow minus <see cref="ObservableDomain.TransactionCommitTimeUtc"/>.
            /// </summary>
            TimeSpan InactiveDelay { get; }

            /// <summary>
            /// Gets whether the domain has been restored from its last snapshot because an error occurred
            /// during the last transaction or from an older one (unfortunately the last transaction didn't
            /// trigger a snapshot).
            /// </summary>
            bool IsRollback { get; }

            /// <summary>
            /// Gets whether the domain has been restored from its last snapshot because an error occurred:
            /// from the sidekick point of view, its state is unchanged since no commands have been emitted.
            /// </summary>
            bool IsSafeRollback { get; }

            /// <summary>
            /// Gets whether the domain has been restored from an old snapshot: the domain state is not 
            /// necessarily synchronized with the last actions that have been executed.
            /// </summary>
            bool IsDangerousRollback { get; }

        }

        /// <summary>
        /// Gets non null information when the domain has just been deserialized and may require some special
        /// handling to resynchronize with the external world.
        /// </summary>
        IDeserializationInfo? DeserializationInfo { get; }
    }
}
