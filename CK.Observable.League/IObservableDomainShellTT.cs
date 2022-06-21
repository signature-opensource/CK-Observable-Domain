using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
    /// used to <see cref="IObservableDomainLoader.LoadAsync(IActivityMonitor, bool?)"/> this shell.
    /// </para>
    /// </summary>
    /// <typeparam name="T1">Type of the first root object.</typeparam>
    /// <typeparam name="T2">Type of the second root object.</typeparam>
    public interface IObservableDomainShell<out T1, out T2> : IObservableDomainShell
        where T1 : ObservableRootObject
        where T2 : ObservableRootObject
    {
        /// <inheritdoc cref="ObservableDomain.ModifyAsync(IActivityMonitor, Action?, bool, int, bool, bool, bool)"/>
        Task<TransactionResult> ModifyAsync( IActivityMonitor monitor,
                                             Action<IActivityMonitor, IObservableDomain<T1,T2>> actions,
                                             bool throwException,
                                             int millisecondsTimeout,
                                             bool considerRolledbackAsFailure,
                                             bool parallelDomainPostActions,
                                             bool waitForDomainPostActionsCompletion );

        /// <inheritdoc cref="ObservableDomain.ModifyThrowAsync(IActivityMonitor, Action?, int, bool, bool, bool)"/>
        Task<TransactionResult> ModifyThrowAsync( IActivityMonitor monitor,
                                                  Action<IActivityMonitor, IObservableDomain<T1, T2>> actions,
                                                  int millisecondsTimeout = -1,
                                                  bool considerRolledbackAsFailure = true,
                                                  bool parallelDomainPostActions = true,
                                                  bool waitForDomainPostActionsCompletion = false );


        /// <inheritdoc cref="ObservableDomain.ModifyThrowAsync{TResult}(IActivityMonitor, Func{TResult}, int, bool, bool)"/>
        Task<TResult> ModifyThrowAsync<TResult>( IActivityMonitor monitor,
                                                 Func<IActivityMonitor, IObservableDomain<T1, T2>, TResult> actions,
                                                 int millisecondsTimeout = -1,
                                                 bool parallelDomainPostActions = true,
                                                 bool waitForDomainPostActionsCompletion = false );

        /// <inheritdoc cref="ObservableDomain.TryModifyAsync(IActivityMonitor, Action?, int, bool, bool, bool)"/>
        Task<TransactionResult> ModifyNoThrowAsync( IActivityMonitor monitor,
                                                    Action<IActivityMonitor, IObservableDomain<T1, T2>> actions,
                                                    int millisecondsTimeout = -1,
                                                    bool considerRolledbackAsFailure = true,
                                                    bool parallelDomainPostActions = true,
                                                    bool waitForDomainPostActionsCompletion = false );

        /// <inheritdoc cref="IObservableDomainShell.TryRead(IActivityMonitor, Action{IActivityMonitor, IObservableDomain}, int)"/>
        bool TryRead( IActivityMonitor monitor, Action<IActivityMonitor, IObservableDomain<T1, T2>> reader, int millisecondsTimeout = -1 );

        /// <inheritdoc cref="ObservableDomain.TryRead{T}(IActivityMonitor, Func{T}, out T, int)"/>
        bool TryRead<TInfo>( IActivityMonitor monitor, Func<IActivityMonitor, IObservableDomain<T1, T2>, TInfo> reader, [MaybeNullWhen( false )] out TInfo result, int millisecondsTimeout = -1 );

    }
}
