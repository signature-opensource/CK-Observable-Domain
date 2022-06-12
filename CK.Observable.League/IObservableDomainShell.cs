using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable.League
{
    /// <summary>
    /// Provides a tracking reference and a isolation shell on a loaded, untyped, <see cref="IObservableDomain"/>
    /// in a <see cref="ObservableLeague"/>.
    /// <para>
    /// The <see cref="IObservableDomainShellBase.DisposeAsync(IActivityMonitor)"/> must be called once this domain is no more required.
    /// </para>
    /// <para>
    /// Note that this <see cref="IAsyncDisposable.DisposeAsync"/> implementation will use the activity monitor that has been
    /// used to <see cref="IObservableDomainLoader.LoadAsync"/> this shell.
    /// </para>
    /// </summary>
    public interface IObservableDomainShell : IObservableDomainShellBase
    {
        /// <summary>
        /// Exports the whole domain state as a JSON object.
        /// </summary>
        /// <param name="millisecondsTimeout">
        /// The maximum number of milliseconds to wait for a read access before giving up.
        /// Wait indefinitely by default.
        /// </param>
        /// <returns>The state as a string or null if timeout occurred.</returns>
        string? ExportToString( int millisecondsTimeout = -1 );

        /// <inheritdoc cref="ObservableDomain.ModifyAsync(IActivityMonitor, Action?, bool, int, bool, bool, bool)"/>
        Task<TransactionResult> ModifyAsync( IActivityMonitor monitor,
                                             Action<IActivityMonitor, IObservableDomain> actions,
                                             bool throwException,
                                             int millisecondsTimeout,
                                             bool considerRolledbackAsFailure,
                                             bool parallelDomainPostActions,
                                             bool waitForDomainPostActionsCompletion )
;

        /// <inheritdoc cref="ObservableDomain.ModifyThrowAsync(IActivityMonitor, Action?, int, bool, bool, bool)"/>
        Task<TransactionResult> ModifyThrowAsync( IActivityMonitor monitor,
                                                  Action<IActivityMonitor, IObservableDomain> actions,
                                                  int millisecondsTimeout = -1,
                                                  bool considerRolledbackAsFailure = true,
                                                  bool parallelDomainPostActions = true,
                                                  bool waitForDomainPostActionsCompletion = false );

        /// <inheritdoc cref="ObservableDomain.ModifyThrowAsync{TResult}(IActivityMonitor, Func{TResult}, int, bool, bool, bool)"/>
        Task<TResult> ModifyThrowAsync<TResult>( IActivityMonitor monitor,
                                                 Func<IActivityMonitor, IObservableDomain, TResult> actions,
                                                 int millisecondsTimeout = -1,
                                                 bool considerRolledbackAsFailure = true,
                                                 bool parallelDomainPostActions = true,
                                                 bool waitForDomainPostActionsCompletion = false );

        /// <inheritdoc cref="ObservableDomain.ModifyNoThrowAsync(IActivityMonitor, Action?, int, bool, bool, bool)"/>
        Task<TransactionResult> ModifyNoThrowAsync( IActivityMonitor monitor,
                                                    Action<IActivityMonitor, IObservableDomain> actions,
                                                    int millisecondsTimeout = -1,
                                                    bool considerRolledbackAsFailure = true,
                                                    bool parallelDomainPostActions = true,
                                                    bool waitForDomainPostActionsCompletion = false );

        /// <inheritdoc cref="ObservableDomain.TryRead(IActivityMonitor, Action, int)"/>
        bool TryRead( IActivityMonitor monitor, Action<IActivityMonitor, IObservableDomain> reader, int millisecondsTimeout = -1 );

        /// <inheritdoc cref="ObservableDomain.TryRead{T}(IActivityMonitor, Func{T}, out T, int)"/>
        bool TryRead<T>( IActivityMonitor monitor, Func<IActivityMonitor, IObservableDomain, T> reader, [MaybeNullWhen( false )]out T result, int millisecondsTimeout = -1 );

    }
}
