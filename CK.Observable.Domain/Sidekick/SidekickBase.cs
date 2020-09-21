using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace CK.Observable
{

    /// <summary>
    /// Sidekicks of a domain interact with the external world.
    /// A sidekick is not serializable (it is the role of the <see cref="InternalObject"/> or <see cref="ObservableObject"/> to
    /// maintain state), a sidekick is an interface to the world (typically an asynchronous world of interactions) that in one
    /// way handles the commands that domain objects have sent during a transaction (via <see cref="DomainView.SendCommand(object)"/>),
    /// and on the other way can monitor/react to any number of "external events" and call one of the Modify/ModifiyAsync domain's method to
    /// inform the domain objects. 
    /// </summary>
    public abstract class SidekickBase
    {
        /// <summary>
        /// Initializes a new sidekick for a domain.
        /// <para>
        /// This is called after an external modification of the domain where an object with a [UseSidekick( ... )] attribute has been instantiated.
        /// If the sidekick type has not any instance yet, this is called just before sollicitating the <see cref="ObservableDomain.DomainClient"/>.
        /// The domain has the write lock held and this constructor can interact with the domain objects (its interaction is part of the transaction). 
        /// </summary>
        /// <param name="domain">The domain.</param>
        protected SidekickBase( ObservableDomain domain )
        {
            Domain = domain;
        }

        /// <summary>
        /// Gets the domain.
        /// </summary>
        protected ObservableDomain Domain { get; }

        /// <summary>
        /// Called when a successful transaction has been successfully handled by the <see cref="ObservableDomain.DomainClient"/>.
        /// When this is called, the <see cref="Domain"/> MUST NOT BE touched in any way: this occurs outside of the domain lock.
        /// <para>
        /// Exceptions raised by this method are collected in <see cref="TransactionResult.CommandErrors"/>.
        /// </para>
        /// <para>
        /// Please note that when this method returns false it just means that the command has not been handled by this sidekick.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="command">The command to execute.</param>
        /// <returns>True if the command has been handled, false if the command has been ignored by this handler.</returns>
        protected internal abstract bool ExecuteCommand( IActivityMonitor monitor, in SidekickCommand command );

    }


}
