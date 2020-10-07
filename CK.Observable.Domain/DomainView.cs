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
        /// Gets the domain name. 
        /// </summary>
        public string DomainName => _d.DomainName;

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
            _d.TimeManager.Remind( dueTimeUtc, callback, null, tag );
        }

        /// <summary>
        /// Uses a pooled <see cref="ObservableReminder"/> to call the specified callback at the given time with the
        /// associated <see cref="ObservableTimedEventBase.Tag"/> object, binding this reminder to a <see cref="SuspendableClock"/>.
        /// </summary>
        /// <param name="dueTimeUtc">The due time. Must be in Utc and not <see cref="Util.UtcMinValue"/> or <see cref="Util.UtcMaxValue"/>.</param>
        /// <param name="callback">The callback method. Must not be null.</param>
        /// <param name="clock">The <see cref="SuspendableClock"/> to which the reminder must be bound.</param>
        /// <param name="tag">Optional tag that will be available on event argument's: <see cref="ObservableTimedEventBase.Tag"/>.</param>
        public void Remind( DateTime dueTimeUtc, SafeEventHandler<ObservableReminderEventArgs> callback, SuspendableClock clock, object? tag = null )
        {
            _d.TimeManager.Remind( dueTimeUtc, callback, clock, tag );
        }

        /// <summary>
        /// Gets whether the domain is being deserialized.
        /// </summary>
        public bool IsDeserializing => _d.IsDeserializing;

        /// <summary>
        /// Ensures that required sidekicks are instantiated and that any required <see cref="ObservableDomainSidekick.RegisterClientObject(IActivityMonitor, IDisposableObject)"/>
        /// have been called.
        /// When this method returns false, it means that an error occurred and that the current transaction cannot be commited.
        /// <para>
        /// This should typically called at the end of a final constructor code of a <see cref="ISidekickClientObject{TSidekick}"/> object.
        /// </para>
        /// </summary>
        /// <returns>True on success, false if one required sidekick failed to be instantiated.</returns>
        public bool EnsureSidekicks() => _d.EnsureSidekicks( _o );
    }
}
