using CK.Core;
using System;
using System.Collections.Generic;

namespace CK.Observable
{
    /// <summary>
    /// Restricted interface on the <see cref="TransactionDoneEventArgs"/> 
    /// that is available in the <see cref="IObservableDomainInspector"/>.
    /// <para>
    /// Even if it's restricted, it's still potentially dangerous access: no interaction with the <see cref="Domain"/> 
    /// should be done even if the read lock is held when this is exposed by <see cref="IObservableDomainInspector.TransactionDone"/> event.
    /// </para>
    /// </summary>
    public interface ITransactionDoneEvent
    {
        /// <inheritdoc cref="EventMonitoredArgs.Monitor"/>.
        IActivityMonitor Monitor { get; }

        /// <inheritdoc cref="TransactionDoneEventArgs.StartTimeUtc"/>
        DateTime StartTimeUtc { get; }
        
        /// <inheritdoc cref="TransactionDoneEventArgs.CommitTimeUtc"/>
        DateTime CommitTimeUtc { get; }

        /// <inheritdoc cref="TransactionDoneEventArgs.Domain"/>
        IObservableDomain Domain { get; }

        /// <inheritdoc cref="TransactionDoneEventArgs.Events"/>
        IReadOnlyList<ObservableEvent> Events { get; }

        /// <inheritdoc cref="TransactionDoneEventArgs.HasSaveCommand"/>
        bool HasSaveCommand { get; }

        /// <inheritdoc cref="TransactionDoneEventArgs.TransactionNumber"/>
        int TransactionNumber { get; }
    }
}
