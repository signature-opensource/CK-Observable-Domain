using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
    /// used to <see cref="IObservableDomainLoader.LoadAsync(IActivityMonitor, bool?)"/> this shell.
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
        /// <inheritdoc cref="ObservableDomain.ModifyAsync(IActivityMonitor, Action?, bool, int, bool, bool, bool)"/>
        Task<TransactionResult> ModifyAsync( IActivityMonitor monitor,
                                             Action<IActivityMonitor, IObservableDomain<T1, T2, T3, T4>> actions,
                                             bool throwException,
                                             int millisecondsTimeout,
                                             bool considerRolledbackAsFailure,
                                             bool parallelDomainPostActions,
                                             bool waitForDomainPostActionsCompletion );

        /// <inheritdoc cref="ObservableDomain.ModifyThrowAsync(IActivityMonitor, Action?, int, bool, bool, bool)"/>
        Task<TransactionResult> ModifyThrowAsync( IActivityMonitor monitor,
                                                  Action<IActivityMonitor, IObservableDomain<T1, T2, T3, T4>> actions,
                                                  int millisecondsTimeout = -1,
                                                  bool considerRolledbackAsFailure = true,
                                                  bool parallelDomainPostActions = true,
                                                  bool waitForDomainPostActionsCompletion = false );


        /// <inheritdoc cref="ObservableDomain.ModifyThrowAsync{TResult}(IActivityMonitor, Func{TResult}, int, bool, bool, bool)"/>
        Task<TResult> ModifyThrowAsync<TResult>( IActivityMonitor monitor,
                                                 Func<IActivityMonitor, IObservableDomain<T1, T2, T3, T4>, TResult> actions,
                                                 int millisecondsTimeout = -1,
                                                 bool considerRolledbackAsFailure = true,
                                                 bool parallelDomainPostActions = true,
                                                 bool waitForDomainPostActionsCompletion = false );

        /// <inheritdoc cref="ObservableDomain.ModifyNoThrowAsync(IActivityMonitor, Action?, int, bool, bool, bool)"/>
        Task<TransactionResult> ModifyNoThrowAsync( IActivityMonitor monitor,
                                                    Action<IActivityMonitor, IObservableDomain<T1, T2, T3, T4>> actions,
                                                    int millisecondsTimeout = -1,
                                                    bool considerRolledbackAsFailure = true,
                                                    bool parallelDomainPostActions = true,
                                                    bool waitForDomainPostActionsCompletion = false );

        /// <inheritdoc cref="IObservableDomainShell.TryRead(IActivityMonitor, Action{IActivityMonitor, IObservableDomain}, int)"/>
        bool TryRead( IActivityMonitor monitor, Action<IActivityMonitor, IObservableDomain<T1, T2, T3, T4>> reader, int millisecondsTimeout = -1 );

        /// <inheritdoc cref="ObservableDomain.TryRead{T}(IActivityMonitor, Func{T}, out T, int)"/>
        bool TryRead<TInfo>( IActivityMonitor monitor, Func<IActivityMonitor, IObservableDomain<T1, T2, T3, T4>, TInfo> reader, [MaybeNullWhen( false )] out TInfo result, int millisecondsTimeout = -1 );

    }
}
