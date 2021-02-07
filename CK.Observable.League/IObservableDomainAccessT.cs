using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable.League
{
    /// <summary>
    /// Defines the accessors to a typed <see cref="IObservableDomain{T}"/>.
    /// <para>
    /// This interface exposes only the typed Modify/Read methods of a <see cref="IObservableDomainShell"/> and
    /// this is used to expose the <see cref="ObservableLeague.Coordinator"/> without the base <see cref="IObservableDomainShellBase"/>
    /// that supports disposal of the object.
    /// There is no IObservableDomainAccess{T1,T2} (or more) since we only need to protect the coordinator root like this: support for
    /// more than one root is directly defined on the corresponding shell (like <see cref="IObservableDomainShell{T1, T2}"/>).
    /// </para>
    /// </summary>
    /// <typeparam name="T">The observable root type.</typeparam>
    public interface IObservableDomainAccess<out T>
        where T : ObservableRootObject
    {
        /// <summary>
        /// Modifies this ObservableDomain in a transaction (any unhandled errors automatically
        /// trigger a rollback and the <see cref="TransactionResult.Success"/> is false) and then executes any pending post-actions.
        /// <para>
        /// Any exceptions raised by <see cref="IObservableDomainClient.OnTransactionStart(IActivityMonitor,ObservableDomain, DateTime)"/> (at the start of the process)
        /// and by <see cref="SuccessfulTransactionEventArgs.PostActions"/> (after the successful commit or the failure) are thrown by this method.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="actions">
        /// The actions to execute on the <see cref="IObservableDomain"/> protected by a transaction (any unhandled errors automatically
        /// trigger a rollback and the <see cref="TransactionResult.Success"/> is false).
        /// </param>
        /// <param name="millisecondsTimeout">
        /// The maximum number of milliseconds to wait for a read access before giving up.
        /// Wait indefinitely by default.
        /// </param>
        /// <param name="parallelDomainPostActions">
        /// False to wait for the success of the <see cref="SuccessfulTransactionEventArgs.PostActions"/> before
        /// allowing the <see cref="SuccessfulTransactionEventArgs.DomainPostActions"/> to run: when PostActions fail, all domain post actions are skipped.
        /// <para>
        /// By default, post actions are executed and domain post actions can immediately be executed by the <see cref="ObservableDomainPostActionExecutor"/> (as
        /// soon as all previous transaction's domain post actions have ran of course).
        /// </para>
        /// </param>
        /// <returns>
        /// The transaction result from <see cref="ObservableDomain.Modify"/>. <see cref="TransactionResult.Empty"/> when the
        /// lock has not been taken before <paramref name="millisecondsTimeout"/>.
        /// </returns>
        Task<TransactionResult> ModifyAsync( IActivityMonitor monitor, Action<IActivityMonitor, IObservableDomain<T>> actions, int millisecondsTimeout = -1, bool parallelDomainPostActions = true );

        /// <inheritdoc cref="ObservableDomain.ModifyThrowAsync(IActivityMonitor, Action, int, bool)"/>
        Task<TransactionResult> ModifyThrowAsync( IActivityMonitor monitor, Action<IActivityMonitor, IObservableDomain<T>> actions, int millisecondsTimeout = -1, bool parallelDomainPostActions = true );

        /// <summary>
        /// Same as <see cref="ModifyThrowAsync"/> with a returned value. Using this (when errors must be thrown) is easier and avoids a closure.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="actions">The actions to execute on the <see cref="IObservableDomain"/> that return a value.</param>
        /// <param name="millisecondsTimeout">
        /// The maximum number of milliseconds to wait for a read access before giving up. Wait indefinitely by default.
        /// </param>
        /// <param name="parallelDomainPostActions">
        /// False to wait for the success of the <see cref="SuccessfulTransactionEventArgs.PostActions"/> before
        /// allowing the <see cref="SuccessfulTransactionEventArgs.DomainPostActions"/> to run: when PostActions fail, all domain post actions are skipped.
        /// <para>
        /// By default, post actions are executed and domain post actions can immediately be executed by the <see cref="ObservableDomainPostActionExecutor"/> (as
        /// soon as all previous transaction's domain post actions have ran of course).
        /// </para>
        /// </param>
        /// <returns>
        /// The result and the transaction result from <see cref="ObservableDomain.Modify"/> (<see cref="TransactionResult.Empty"/> when the
        /// lock has not been taken before <paramref name="millisecondsTimeout"/>).
        /// This is necessarily a successful <see cref="TransactionResult"/> since otherwise an exception is thrown (note that the domain post actions
        /// are executed later by the <see cref="ObservableDomainPostActionExecutor"/>).
        /// </returns>
        Task<(TResult, TransactionResult)> ModifyThrowAsync<TResult>( IActivityMonitor monitor, Func<IActivityMonitor, IObservableDomain<T>, TResult> actions, int millisecondsTimeout = -1, bool parallelDomainPostActions = true );

        /// <summary>
        /// Same as <see cref="ModifyAsync"/> except that it will never throw: any exception raised
        /// by <see cref="IObservableDomainClient.OnTransactionStart(IActivityMonitor, ObservableDomain, DateTime)"/>
        /// or <see cref="TransactionResult.ExecutePostActionsAsync(IActivityMonitor, bool)"/> is logged and returned.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="actions">
        /// The actions to execute on the <see cref="IObservableDomain"/>.
        /// </param>
        /// <param name="millisecondsTimeout">
        /// The maximum number of milliseconds to wait for a read access before giving up. Wait indefinitely by default.
        /// </param>
        /// <param name="parallelDomainPostActions">
        /// False to wait for the success of the <see cref="SuccessfulTransactionEventArgs.PostActions"/> before
        /// allowing the <see cref="SuccessfulTransactionEventArgs.DomainPostActions"/> to run: when PostActions fail, all domain post actions are skipped.
        /// <para>
        /// By default, post actions are executed and domain post actions can immediately be executed by the <see cref="ObservableDomainPostActionExecutor"/> (as
        /// soon as all previous transaction's domain post actions have ran of course).
        /// </para>
        /// </param>
        /// <returns>
        /// Returns any initial exception, the transaction result (that may be <see cref="TransactionResult.Empty"/>).
        /// </returns>
        Task<(Exception? OnStartTransactionError, TransactionResult Transaction)> ModifyNoThrowAsync( IActivityMonitor monitor, Action<IActivityMonitor, IObservableDomain<T>> actions, int millisecondsTimeout = -1, bool parallelDomainPostActions = true );

        /// <summary>
        /// Reads the domain by protecting the <paramref name="reader"/> function in a <see cref="ObservableDomain.AcquireReadLock(int)"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="reader">The reader function.</param>
        /// <param name="millisecondsTimeout">
        /// The maximum number of milliseconds to wait for a read access before giving up. Wait indefinitely by default.
        /// </param>
        void Read( IActivityMonitor monitor, Action<IActivityMonitor, IObservableDomain<T>> reader, int millisecondsTimeout = -1 );

        /// <summary>
        /// Reads the domain by protecting the <paramref name="reader"/> function in a <see cref="ObservableDomain.AcquireReadLock(int)"/>.
        /// </summary>
        /// <typeparam name="TInfo">The type of the information to read.</typeparam>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="reader">The reader function that projects read information into a <typeparamref name="TInfo"/>.</param>
        /// <param name="millisecondsTimeout">
        /// The maximum number of milliseconds to wait for a read access before giving up. Wait indefinitely by default.
        /// </param>
        /// <returns>The information.</returns>
        TInfo Read<TInfo>( IActivityMonitor monitor, Func<IActivityMonitor, IObservableDomain<T>, TInfo> reader, int millisecondsTimeout = -1 );

    }
}
