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
    /// <para>
    /// This is not a singleton for the Dependency Injection since a sidekick is bound to its <see cref="Domain"/> but its dependencies MUST
    /// be singletons.
    /// </para>
    /// <para>
    /// This is a "ISingletonAutoType" (that doesn't exist yet): a singleton auto type is like a ISingletonAutoService except that it cannot
    /// be instantiated automatically. An explicit contructor should be used with explicit parameters.
    /// A ISingletonAutoType cannot be a dependency.
    /// The concept of "IAutoType" captures the "most specialized type" resolution mechanism (it's currently missing in the landscape).
    /// The "ISingletonAutoType"/"IScopedAutoType" introduces a constraint on its dependencies that are IAutoService objects: the constructor
    /// parameters that are "contextual" must be defined when a "IAutoType" is defined otherwise those "unknwon" dependencies would be considered
    /// scoped (and they are not).
    /// ...IAutoType is necessarily a Class, not an interface!
    /// So it can be named "IAutoClass". It must be handled like IAutoService but its NOT a IAutoService.
    /// Constructor parameters can be marked with a [ExplicitParameter] attribute (ie. not from the DI).
    /// </para>
    /// </summary>
    public abstract class ObservableDomainSidekick
    {
        /// <summary>
        /// Initializes a new sidekick for a domain.
        /// <para>
        /// This is called after an external modification of the domain where an object with a [UseSidekick( ... )] attribute has been instantiated.
        /// If the sidekick type has not any instance yet, this is called just before sollicitating the <see cref="ObservableDomain.DomainClient"/>.
        /// The domain has the write lock held and this constructor can interact with the domain objects (its interaction is part of the transaction).
        /// <para>
        /// A <see cref="IActivityMonitor"/> can appear in the parameters (and must be used only in the constructor and not kept) of the constructor and
        /// all other parameters MUST be singletons.
        /// </para>
        /// </summary>
        /// <param name="domain">The domain.</param>
        protected ObservableDomainSidekick( ObservableDomain domain )
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
