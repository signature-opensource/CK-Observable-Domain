using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable.League
{
    /// <summary>
    /// Provides a tracking reference and a isolation shell on a loaded typed <see cref="IObservableDomain{T1,T2}"/>
    /// in a <see cref="ObservableLeague"/>.
    /// <para>
    /// The <see cref="IObservableDomainShellBase.DisposeAsync(IActivityMonitor)"/> must be called once this domain is no more required.
    /// </para>
    /// <para>
    /// Note that this <see cref="IAsyncDisposable.DisposeAsync"/> implementation will use the activity monitor that has been
    /// used to <see cref="IObservableDomainLoader.LoadAsync(IActivityMonitor)"/> this shell.
    /// </para>
    /// </summary>
    /// <typeparam name="T">The observable root type.</typeparam>
    public interface IObservableDomainShell<out T1, out T2> : IObservableDomainShell
        where T1 : ObservableRootObject
        where T2 : ObservableRootObject
    {
        /// <summary>
        /// Modifies this ObservableDomain in a transaction (any unhandled errors automatically
        /// trigger a rollback and the <see cref="TransactionResult.Success"/> is false) and then executes any pending post-actions.
        /// <para>
        /// Any exceptions raised by <see cref="IObservableDomainClient.OnTransactionStart(IActivityMonitor,ObservableDomain, DateTime)"/> (at the start of the process)
        /// and by <see cref="TransactionResult.PostActions"/> (after the successful commit or the failure) are thrown by this method.
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="actions">
        /// The actions to execute on the <see cref="IObservableDomain"/> protected by a transaction (any unhandled errors automatically
        /// trigger a rollback and the <see cref="TransactionResult.Success"/> is false).
        /// Can be null: only pending timed events are executed if any.
        /// </param>
        /// <param name="millisecondsTimeout">
        /// The maximum number of milliseconds to wait for a read access before giving up.
        /// Wait indefinitely by default.
        /// </param>
        /// <returns>
        /// The transaction result from <see cref="ObservableDomain.Modify"/>. <see cref="TransactionResult.Empty"/> when the
        /// lock has not been taken before <paramref name="millisecondsTimeout"/>.
        /// </returns>
        Task<TransactionResult> ModifyAsync( IActivityMonitor monitor, Action<IActivityMonitor, IObservableDomain<T1,T2>>? actions, int millisecondsTimeout = -1 );

        /// <summary>
        /// Same as <see cref="ModifyAsync"/> except that it Will never throw: any exception raised
        /// by <see cref="IObservableDomainClient.OnTransactionStart(IActivityMonitor, ObservableDomain, DateTime)"/>
        /// or <see cref="TransactionResult.ExecutePostActionsAsync(IActivityMonitor, bool)"/> is logged and returned.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="actions">
        /// The actions to execute on the <see cref="IObservableDomain"/>. Can be null: only pending timed events are executed if any.
        /// </param>
        /// <param name="millisecondsTimeout">
        /// The maximum number of milliseconds to wait for a read access before giving up. Wait indefinitely by default.
        /// </param>
        /// <returns>
        /// Returns the transaction result (that may be <see cref="TransactionResult.Empty"/>) and any exception outside of the observable transaction itself.
        /// </returns>
        Task<(TransactionResult, Exception)> SafeModifyAsync( IActivityMonitor monitor, Action<IActivityMonitor, IObservableDomain<T1,T2>>? actions, int millisecondsTimeout = -1 );

        /// <summary>
        /// Reads the domain by protecting the <paramref name="reader"/> function in a <see cref="ObservableDomain.AcquireReadLock(int)"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="reader">The reader function.</param>
        /// <param name="millisecondsTimeout">
        /// The maximum number of milliseconds to wait for a read access before giving up. Wait indefinitely by default.
        /// </param>
        void Read( IActivityMonitor monitor, Action<IActivityMonitor, IObservableDomain<T1,T2>> reader, int millisecondsTimeout = -1 );

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
        TInfo Read<TInfo>( IActivityMonitor monitor, Func<IActivityMonitor, IObservableDomain<T1,T2>, TInfo> reader, int millisecondsTimeout = -1 );

    }
}
