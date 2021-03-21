using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable
{
    /// <summary>
    /// Extends <see cref="EventMonitoredArgs"/> to add the <see cref="Domain"/>.
    /// One internal instance is allocated per domain and reused as much as possible but
    /// this event can be specialized if more data is required (see the <see cref="ObservableTimedEventArgs"/> for instance).
    /// </summary>
    public class ObservableDomainEventArgs : EventMonitoredArgs
    {
        /// <summary>
        /// Instanciates a new <see cref="ObservableDomainEventArgs"/>.
        /// </summary>
        /// <param name="d">The owning domain.</param>
        public ObservableDomainEventArgs( ObservableDomain d )
        {
            Domain = d;
        }

        /// <summary>
        /// Gets the monitor that must be used while handling the event: it is the monitor
        /// of the current transaction.
        /// </summary>
        public override IActivityMonitor Monitor => Domain.CurrentMonitor;

        /// <summary>
        /// Gets the domain.
        /// </summary>
        public ObservableDomain Domain { get; }
    }
}
