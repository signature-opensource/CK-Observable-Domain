using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable
{
    /// <summary>
    /// Offers a protected view on its <see cref="ObservableDomain"/> from the point of view
    /// of domain objects: This is exposed by the protected <see cref="ObservableObject.Domain"/>
    /// and <see cref="InternalObject.Domain"/>.
    /// </summary>
    public readonly struct DomainView
    {
        readonly IDisposableObject _o;
        readonly ObservableDomain _d;

        internal DomainView( IDisposableObject o, ObservableDomain d )
        {
            _o = o;
            _d = d;
        }

        /// <summary>
        /// Gives access to the monitor to use.
        /// </summary>
        public IActivityMonitor Monitor => _d.CurrentMonitor;

        /// <summary>
        /// Sends a command to the external world. Commands are enlisted
        /// into <see cref="TransactionResult.Commands"/> (when the transaction succeeds)
        /// and can be processed by any <see cref="IObservableDomainClient"/> or by a <see cref="ObservableDomainSidekick"/>.
        /// </summary>
        /// <param name="command">Any command description.</param>
        public void SendCommand( object command )
        {
            _d.SendCommand( _o, command );
        }

        /// <summary>
        /// Gets a preallocated reusable event argument. 
        /// </summary>
        public ObservableDomainEventArgs DefaultEventArgs => _d.DefaultEventArgs;

        /// <summary>
        /// Uses a pooled <see cref="ObservableReminder"/> to call the specified callback at the given time with the
        /// associated <see cref="ObservableTimedEventBase.Tag"/> object.
        /// </summary>
        /// <param name="dueTimeUtc">The due time. Must be in Utc and not <see cref="Util.UtcMinValue"/> or <see cref="Util.UtcMaxValue"/>.</param>
        /// <param name="callback">The callback method. Must not be null.</param>
        /// <param name="tag">Optional tag that will be available on event argument's: <see cref="ObservableTimedEventBase.Tag"/>.</param>
        public void Remind( DateTime dueTimeUtc, SafeEventHandler<ObservableReminderEventArgs> callback, object? tag = null )
        {
            _d.TimeManager.Remind( dueTimeUtc, callback, tag );
        }

        /// <summary>
        /// Gets whether the domain is being deserialized.
        /// </summary>
        public bool IsDeserializing => _d.IsDeserializing;

        /// <summary>
        /// Raised when a new <see cref="ObservableDomainSidekick"/> is available.
        /// </summary>
        public event SafeEventHandler<SidekickActivatedEventArgs> SidekickActivated
        {
            add => _d.AddOrRemoveSidekickActivatedHandler( _o, true, value );
            remove => _d.AddOrRemoveSidekickActivatedHandler( _o, false, value );
        }

    }
}
