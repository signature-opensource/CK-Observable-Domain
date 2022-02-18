using CK.Core;
using System;
using System.Collections.Generic;

namespace CK.Observable
{
    /// <summary>
    /// Restricted interface on the <see cref="SuccessfulTransactionEventArgs"/> 
    /// that is available in the <see cref="IObservableDomainInspector"/>.
    /// <para>
    /// Even if it's restricted, it's still potentially dangerous access: no interaction with the <see cref="Domain"/> 
    /// should be done even if the read lock is held when this is exposed by <see cref="IObservableDomainInspector.OnSuccessfulTransaction"/> event.
    /// </para>
    /// </summary>
    public interface ISuccessfulTransactionEvent
    {
        /// <inheritdoc cref="EventMonitoredArgs.Monitor"/>.
        IActivityMonitor Monitor { get; }

        /// <inheritdoc cref="SuccessfulTransactionEventArgs.StartTimeUtc"/>
        DateTime StartTimeUtc { get; }
        
        /// <inheritdoc cref="SuccessfulTransactionEventArgs.CommitTimeUtc"/>
        DateTime CommitTimeUtc { get; }

        /// <inheritdoc cref="SuccessfulTransactionEventArgs.Domain"/>
        IObservableDomain Domain { get; }

        /// <inheritdoc cref="SuccessfulTransactionEventArgs.Events"/>
        IReadOnlyList<ObservableEvent> Events { get; }

        /// <inheritdoc cref="SuccessfulTransactionEventArgs.HasSaveCommand"/>
        bool HasSaveCommand { get; }

        /// <inheritdoc cref="SuccessfulTransactionEventArgs.TransactionNumber"/>
        int TransactionNumber { get; }
    }
}