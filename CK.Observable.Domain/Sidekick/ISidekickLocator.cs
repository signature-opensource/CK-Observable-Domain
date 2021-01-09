using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable
{
    /// <summary>
    /// This interface supports easy targeting of command handling by <see cref="DomainView.SendCommand(object, ISidekickLocator, bool)"/>
    /// and <see cref="SuccessfulTransactionEventArgs.SendCommand(object, ISidekickLocator, bool)"/>.
    /// <para>
    /// It is totally optional but it helps the developer to target a command handler instead of using command broadcast (to all available sidekicks).
    /// </para>
    /// </summary>
    public interface ISidekickLocator
    {
        /// <summary>
        /// Gets the associated sidekick object.
        /// </summary>
        ObservableDomainSidekick Sidekick { get; }
    }

}
