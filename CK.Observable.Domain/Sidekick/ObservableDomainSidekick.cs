using CK.Core;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace CK.Observable;


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
/// be instantiated automatically. An explicit constructor should be used with explicit parameters.
/// A ISingletonAutoType cannot be a dependency.
/// The concept of "IAutoType" captures only the "most specialized type" resolution mechanism (it's currently missing in the landscape).
/// The "ISingletonAutoType"/"IScopedAutoType" introduces a constraint on its dependencies that are IAutoService objects: the constructor
/// parameters that are "contextual" must be defined when a "IAutoType" is defined otherwise those "unknown" dependencies would be considered
/// scoped (and they are not).
/// ...IAutoType is necessarily a Class, not an interface!
/// So it can be named "IAutoClass". It must be handled like IAutoService but it's NOT a IAutoService.
/// Constructor parameters can be marked with a [ExplicitParameter] attribute (saying "I'm not from the DI"). Or a "record" can capture the
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
public abstract class ObservableDomainSidekick : ISidekickLocator
{
    /// <summary>
    /// Initializes a new sidekick for a domain.
    /// <para>
    /// This is called when an object with a [UseSidekick( ... )] attribute or a <see cref="ISidekickClientObject{TSidekick}"/>
    /// interface marker has been instantiated and we are in a regular transaction.
    /// The domain has the write lock held and this constructor can interact with the domain objects (its interaction is part of the transaction).
    /// </para>
    /// <para>
    /// A <see cref="IActivityMonitor"/> can appear in the parameters of the constructor (and must be used only in the constructor and not kept) and
    /// all other parameters MUST be singletons services.
    /// </para>
    /// </summary>
    /// <param name="manager">The domain's sidekick manager.</param>
    protected ObservableDomainSidekick( IObservableDomainSidekickManager manager )
    {
        Manager = manager;
        Domain = manager.Domain;
    }

    /// <summary>
    /// Gets the domain's sidekick manager.
    /// </summary>
    protected IObservableDomainSidekickManager Manager { get; }

    /// <summary>
    /// Gets the domain.
    /// <para>
    /// Caution: this is the real domain object, not the restricted <see cref="DomainView"/> that is accessible
    /// from Observable or Internal objects.
    /// </para>
    /// </summary>
    internal protected ObservableDomain Domain { get; }

    ObservableDomainSidekick ISidekickLocator.Sidekick => this;

    /// <summary>
    /// Must register the provided <see cref="InternalObject"/> or <see cref="ObservableObject"/> as a client
    /// of this sidekick.
    /// <para>
    /// The semantics of this registration and what a "client" actually means, is specific to each sidekick.
    /// </para>
    /// <para>
    /// When this method is called the domain lock is held, any interaction can take place.
    /// After that registering phase, interactions must be protected in one of the Read or ModifyAsync methods.
    /// </para>
    /// <para>
    /// When a sidekick keeps a reference on a client object, it should either check its IsDestroyed or registers
    /// to <see cref="IDestroyable.Destroyed"/> event or, even better if the objects share a base class, implement
    /// internal OnDestroyed callbacks (the ObservableDeviceObject and ObservableDeviceSidekick do this).
    /// </para>
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="o">The object that uses this sidekick.</param>
    protected internal abstract void RegisterClientObject( IActivityMonitor monitor, IDestroyable o );

    /// <summary>
    /// Called when a successful transaction has been successfully handled by the <see cref="ObservableDomain.DomainClient"/>.
    /// Default implementation does nothing at this level.
    /// <para>
    /// When this is called, the <see cref="Domain"/>'s lock is held in read mode: objects can be read (but no write/modifications
    /// should occur). A typical implementation is to capture any required domain object's state and use
    /// <see cref="TransactionDoneEventArgs.PostActions"/> or <see cref="TransactionDoneEventArgs.DomainPostActions"/>
    /// to post asynchronous actions (or to send commands thanks to <see cref="TransactionDoneEventArgs.SendCommand(in ObservableDomainCommand)"/>
    /// that will be processed by this or other sidekicks).
    /// </para>
    /// <para>
    /// Exceptions raised by this method are collected in <see cref="TransactionResult.TransactionDoneErrors"/>.
    /// </para>
    /// </summary>
    /// <param name="result">The <see cref="TransactionDoneEventArgs"/> event argument.</param>
    protected internal virtual void OnTransactionResult( TransactionDoneEventArgs result )
    {
    }

    /// <summary>
    /// Called when a successful transaction has been successfully handled by the <see cref="ObservableDomain.DomainClient"/>
    /// and <see cref="ObservableDomain.TransactionDone"/> event and <see cref="OnTransactionResult(TransactionDoneEventArgs)"/>
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
    /// <para>
    /// The reason is available in <see cref="DomainView.CurrentTransactionStatus">Domain.CurrentTransactionStatus</see>.
    /// </para>
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    protected internal abstract void OnUnload( IActivityMonitor monitor );

}
