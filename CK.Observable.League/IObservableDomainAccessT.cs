using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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

        /// <inheritdoc cref="ObservableDomain.ModifyAsync(IActivityMonitor, Action?, bool, int, bool, bool, bool)"/>
        Task<TransactionResult> ModifyAsync( IActivityMonitor monitor,
                                             Action<IActivityMonitor, IObservableDomain<T>> actions,
                                             bool throwException,
                                             int millisecondsTimeout,
                                             bool considerRolledbackAsFailure,
                                             bool parallelDomainPostActions,
                                             bool waitForDomainPostActionsCompletion );

        /// <inheritdoc cref="ObservableDomain.ModifyThrowAsync(IActivityMonitor, Action?, int, bool, bool, bool)"/>
        Task<TransactionResult> ModifyThrowAsync( IActivityMonitor monitor,
                                                  Action<IActivityMonitor, IObservableDomain<T>> actions,
                                                  int millisecondsTimeout = -1,
                                                  bool considerRolledbackAsFailure = true,
                                                  bool parallelDomainPostActions = true,
                                                  bool waitForDomainPostActionsCompletion = false );


        /// <inheritdoc cref="ObservableDomain.ModifyThrowAsync{TResult}(IActivityMonitor, Func{TResult}, int, bool, bool)"/>
        Task<TResult> ModifyThrowAsync<TResult>( IActivityMonitor monitor,
                                                 Func<IActivityMonitor, IObservableDomain<T>, TResult> actions,
                                                 int millisecondsTimeout = -1,
                                                 bool parallelDomainPostActions = true,
                                                 bool waitForDomainPostActionsCompletion = false );

        /// <inheritdoc cref="ObservableDomain.TryModifyAsync(IActivityMonitor, Action?, int, bool, bool, bool)"/>
        Task<TransactionResult> TryModifyAsync( IActivityMonitor monitor,
                                                Action<IActivityMonitor, IObservableDomain<T>> actions,
                                                int millisecondsTimeout = -1,
                                                bool considerRolledbackAsFailure = true,
                                                bool parallelDomainPostActions = true,
                                                bool waitForDomainPostActionsCompletion = false );

        /// <inheritdoc cref="IObservableDomainShell.TryRead(IActivityMonitor, Action{IActivityMonitor, IObservableDomain}, int)"/>
        bool TryRead( IActivityMonitor monitor, Action<IActivityMonitor, IObservableDomain<T>> reader, int millisecondsTimeout );

        /// <inheritdoc cref="ObservableDomain.TryRead{T}(IActivityMonitor, Func{T}, out T, int)"/>
        bool TryRead<TInfo>( IActivityMonitor monitor,
                             Func<IActivityMonitor, IObservableDomain<T>, TInfo> reader,
                             [MaybeNullWhen( false )] out TInfo result,
                             int millisecondsTimeout );

        /// <inheritdoc cref="ObservableDomain.Read{T}(IActivityMonitor, Func{T})"/>
        TInfo Read<TInfo>( IActivityMonitor monitor, Func<IActivityMonitor, IObservableDomain<T>, TInfo> reader );
    }
}
