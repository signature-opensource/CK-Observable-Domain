using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable
{
    /// <summary>
    /// This optional interface for <see cref="ObservableObject"/> or <see cref="InternalObject"/> enables
    /// domain objects to react to main actions on the domain.
    /// </summary>
    public interface IObservableDomainActionTracker : IDestroyable
    {
        /// <summary>
        /// Called before the <see cref="ObservableDomain.Modify(IActivityMonitor, Action, int)"/> action execution
        /// (but after all <see cref="ObservableTimer"/> or <see cref="ObservableReminder"/> elapsed events).
        /// </summary>
        /// <param name="monitor">The monitor of the current transaction.</param>
        /// <param name="time">The <see cref="TransactionResult.StartTimeUtc"/>.</param>
        void BeforeModify( IActivityMonitor monitor, DateTime time );

        /// <summary>
        /// Called after the <see cref="ObservableDomain.Modify(IActivityMonitor, Action, int)"/> action execution
        /// (but before any <see cref="ObservableTimer"/> or <see cref="ObservableReminder"/> events that may trigger).
        /// </summary>
        /// <param name="monitor">The monitor of the current transaction.</param>
        /// <param name="startTime">The <see cref="TransactionResult.StartTimeUtc"/>.</param>
        /// <param name="actionDuration">The time span of the execution.</param>
        void AfterModify( IActivityMonitor monitor, DateTime startTime, TimeSpan actionDuration );
    }
}
