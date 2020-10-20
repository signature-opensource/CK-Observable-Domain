using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable.League
{
    /// <summary>
    /// Provides a tracking reference and a isolation shell on a loaded typed <see cref="IObservableDomain{T1,T2,T3,T4}"/>
    /// in a <see cref="ObservableLeague"/>.
    /// <para>
    /// The <see cref="IObservableDomainShellBase.DisposeAsync(IActivityMonitor)"/> must be called once this domain is no more required.
    /// </para>
    /// <para>
    /// Note that this <see cref="IAsyncDisposable.DisposeAsync"/> implementation will use the activity monitor that has been
    /// used to <see cref="IObservableDomainLoader.LoadAsync(IActivityMonitor)"/> this shell.
    /// </para>
    /// </summary>
    /// <typeparam name="T1">The type of the first observable root.</typeparam>
    /// <typeparam name="T2">The type of the second observable root.</typeparam>
    /// <typeparam name="T3">The type of the third observable root.</typeparam>
    /// <typeparam name="T4">The type of the fourth observable root.</typeparam>
    public interface IObservableDomainShell<out T1, out T2, out T3, out T4> : IObservableDomainShell
        where T1 : ObservableRootObject
        where T2 : ObservableRootObject
        where T3 : ObservableRootObject
        where T4 : ObservableRootObject
    {
        /// <summary>
        /// Modifies this ObservableDomain in a transaction (any unhandled errors automatically
        /// trigger a rollback and the <see cref="TransactionResult.Success"/> is false) and then executes any pending post-actions.
        /// <para>
        /// Any exceptions raised by <see cref="IObservableDomainClient.OnTransactionStart(IActivityMonitor,ObservableDomain, DateTime)"/> (at the start of the process)
        /// and by any <see cref="SuccessfulTransactionEventArgs.PostActions"/> (after the successful commit or the failure) are thrown by this method.
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
        /// <returns>
        /// The transaction result from <see cref="ObservableDomain.Modify"/>. <see cref="TransactionResult.Empty"/> when the
        /// lock has not been taken before <paramref name="millisecondsTimeout"/>.
        /// </returns>
        Task<TransactionResult> ModifyAsync( IActivityMonitor monitor, Action<IActivityMonitor, IObservableDomain<T1, T2, T3, T4>> actions, int millisecondsTimeout = -1 );

        /// <summary>
        /// Same as <see cref="ModifyAsync"/> except that it will always throw on any error.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="actions">The actions to execute on the <see cref="IObservableDomain{T1,T2,T3,T4}"/>.
        /// </param>
        /// <param name="millisecondsTimeout">
        /// The maximum number of milliseconds to wait for a read access before giving up. Wait indefinitely by default.
        /// </param>
        /// <returns>The awaitable.</returns>
        Task ModifyThrowAsync( IActivityMonitor monitor, Action<IActivityMonitor, IObservableDomain<T1, T2, T3, T4>> actions, int millisecondsTimeout = -1 );

        /// <summary>
        /// Same as <see cref="ModifyThrowAsync"/> with a returned value. Using this (when errors must be thrown) is easier and avoids a closure.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="actions">The actions to execute on the <see cref="IObservableDomain{T1,T2,T3,T4}"/> that return a value.</param>
        /// <param name="millisecondsTimeout">
        /// The maximum number of milliseconds to wait for a read access before giving up. Wait indefinitely by default.
        /// </param>
        /// <returns>The result.</returns>
        Task<TResult> ModifyThrowAsync<TResult>( IActivityMonitor monitor, Func<IActivityMonitor, IObservableDomain<T1, T2, T3, T4>, TResult> actions, int millisecondsTimeout = -1 );

        /// <summary>
        /// Same as <see cref="ModifyAsync"/> except that it Will never throw: any exception raised
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
        /// <returns>
        /// Returns the transaction result (that may be <see cref="TransactionResult.Empty"/>) and any exception outside of the observable transaction itself.
        /// </returns>
        Task<(TransactionResult, Exception)> ModifyNoThrowAsync( IActivityMonitor monitor, Action<IActivityMonitor, IObservableDomain<T1, T2, T3, T4>> actions, int millisecondsTimeout = -1 );

        /// <summary>
        /// Reads the domain by protecting the <paramref name="reader"/> function in a <see cref="ObservableDomain.AcquireReadLock(int)"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="reader">The reader function.</param>
        /// <param name="millisecondsTimeout">
        /// The maximum number of milliseconds to wait for a read access before giving up. Wait indefinitely by default.
        /// </param>
        void Read( IActivityMonitor monitor, Action<IActivityMonitor, IObservableDomain<T1, T2, T3, T4>> reader, int millisecondsTimeout = -1 );

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
        TInfo Read<TInfo>( IActivityMonitor monitor, Func<IActivityMonitor, IObservableDomain<T1, T2, T3, T4>, TInfo> reader, int millisecondsTimeout = -1 );

    }
}
