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
    /// way handles the commands that domain objects have sent during a transaction (via <see cref="DomainView.SendCommand(in ObservableDomainCommand)"/>),
    /// and on the other way can monitor/react to any number of "external events" and call one of the Modify/ModifiyAsync domain's method to
    /// inform, update, modify the domain objects.
    /// <para>
    /// This is not a singleton for the Dependency Injection since a sidekick is bound to its <see cref="Domain"/> but its dependencies MUST
    /// be singletons.
    /// </para>
    /// <para>
    /// Note to myself:
    /// This is a "ISingletonAutoType" (that doesn't exist yet): a singleton auto type is like a ISingletonAutoService except that it cannot
    /// be instantiated automatically. An explicit contructor should be used with explicit parameters.
    /// A ISingletonAutoType cannot be a dependency.
    /// The concept of "IAutoType" captures only the "most specialized type" resolution mechanism (it's currently missing in the landscape).
    /// The "ISingletonAutoType"/"IScopedAutoType" introduces a constraint on its dependencies that are IAutoService objects: the constructor
    /// parameters that are "contextual" must be defined when a "IAutoType" is defined otherwise those "unknwon" dependencies would be considered
    /// scoped (and they are not).
    /// ...IAutoType is necessarily a Class, not an interface!
    /// So it can be named "IAutoClass". It must be handled like IAutoService but it's NOT a IAutoService.
    /// Constructor parameters can be marked with a [ExplicitParameter] attribute (ie. not from the DI). Or a "record" can capture the
    /// "contextual parameters"? And a standard factory method takes any "record" that are "IAutoClassParameters"?
    /// (I like this last one... But it locks the possibility to extend the "contextual parameters": a record, as a class, can be specialized but
    /// with a single inheritance chain.)
    ///
    /// IPoco should be the answer... A IAutoClass is very much like a IAutoService class with an extra "contextual parameter".
    /// A IAutoClass constructor has any number of services parameters PLUS one IPoco.
    /// This IPoco can be extended by any number of other assemblies but, in fine, a final, non ambiguous
    /// IAutoClass type must be resolved (just like a final non ambiguous IAutoService is resolved by the "class unification" algorithm).
    ///
    /// The magic here is that the "Class unification" handles the IAutoClass selection and the contextual parameters covariance
    /// are handled by the IPoco mechanism.
    /// 
    /// </para>
    /// </summary>
    public abstract class ObservableDomainSidekick
    {
        /// <summary>
        /// Initializes a new sidekick for a domain.
        /// <para>
        /// This is called after an external modification of the domain where an object with a [UseSidekick( ... )] attribute or a <see cref="ISidekickClientObject{TSidekick}"/>
        /// interface marker has been instantiated.
        /// If the sidekick type has not any instance yet, this is called just before sollicitating the <see cref="ObservableDomain.DomainClient"/>.
        /// The domain has the write lock held and this constructor can interact with the domain objects (its interaction is part of the transaction).
        /// </para>
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
        internal protected ObservableDomain Domain { get; }

        /// <summary>
        /// Must register the provided <see cref="InternalObject"/> or <see cref="ObservableObject"/> as a client
        /// of this sidekick.
        /// The semantics of this registration, what a "client" actually means, is specific to each sidekick.
        /// <para>
        /// When this method is called (from <see cref="DomainView.EnsureSidekicks"/> or at the end of a <see cref="ObservableDomain.Modify"/> call
        /// or after the deserialization of the graph by <see cref="ObservableDomain.Load(IActivityMonitor, System.IO.Stream, bool, System.Text.Encoding?, int, bool?)"/>),
        /// the domain lock is held, any interaction can take place.
        /// After that registering phase, interactions must be protected in <see cref="ObservableDomain.AcquireReadLock(int)"/> or one of the Modify method.
        /// </para>
        /// <para>
        /// When a sidekick keeps a reference on a client object, it should either check <see cref="IDestroyableObject.IsDisposed"/> (in the context of
        /// a read or modify lock) or registers to <see cref="IDestroyableObject.Disposed"/> event.
        /// </para>
        /// </summary>
        /// <param name="monitor"></param>
        /// <param name="o"></param>
        protected internal abstract void RegisterClientObject( IActivityMonitor monitor, IDestroyableObject o );

        /// <summary>
        /// Called when a successful transaction has been successfully handled by the <see cref="ObservableDomain.DomainClient"/>.
        /// Default implementation does nothing at this level.
        /// <para>
        /// When this is called, the <see cref="Domain"/>'s lock is held in read mode: objects can be read (but no write/modifications
        /// should occur). A typical implementation is to capture any required domain object's state and use
        /// <see cref="SuccessfulTransactionEventArgs.PostActions"/> or <see cref="SuccessfulTransactionEventArgs.DomainPostActions"/>
        /// to post asynchronous actions (or to send commands thanks to <see cref="SuccessfulTransactionEventArgs.SendCommand(in ObservableDomainCommand)"/>
        /// that will be processed by this or other sidekicks).
        /// </para>
        /// <para>
        /// Exceptions raised by this method are collected in <see cref="TransactionResult.SuccessfulTransactionErrors"/>.
        /// </para>
        /// </summary>
        /// <param name="result">The <see cref="SuccessfulTransactionEventArgs"/> event argument.</param>
        protected internal virtual void OnSuccessfulTransaction( in SuccessfulTransactionEventArgs result )
        {
        }

        /// <summary>
        /// Called when a successful transaction has been successfully handled by the <see cref="ObservableDomain.DomainClient"/>
        /// and <see cref="ObservableDomain.OnSuccessfulTransaction"/> event and <see cref="OnSuccessfulTransaction(in SuccessfulTransactionEventArgs)"/>
        /// did not raise any error.
        /// <para>
        /// When this is called, the <see cref="Domain"/> MUST NOT BE touched in any way: this occurs outside of the domain lock.
        /// </para>
        /// <para>
        /// Exceptions raised by this method are collected in <see cref="TransactionResult.CommandHandlingErrors"/>.
        /// </para>
        /// <para>
        /// Please note that when this method returns false it just means that the command has not been handled by this sidekick.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="command">The command to execute.</param>
        /// <returns>True if the command has been handled, false if the command has been ignored by this handler.</returns>
        protected internal abstract bool ExecuteCommand( IActivityMonitor monitor, in SidekickCommand command );

        /// <summary>
        /// Called when the <see cref="Domain"/> is being cleared, either because it will be reloaded or because it is definitely disposed.
        /// In both case, the domain lock is held.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        protected internal abstract void OnDomainCleared( IActivityMonitor monitor );

    }


}
