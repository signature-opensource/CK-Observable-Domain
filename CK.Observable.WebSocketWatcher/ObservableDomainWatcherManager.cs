using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using CK.Core;
using CK.Cris;
using Microsoft.Extensions.Hosting;
using SimpleR;

namespace CK.Observable.WebSocketWatcher;

/// <summary>
/// Singleton that tracks all active <see cref="ObservableDomainWatcher"/> instances, keyed by connection identifier.
/// <para>
/// Handles the <see cref="IObservableDomainWatcherStartOrRestartCommand"/> to start or restart watching
/// a domain for a given connection.
/// </para>
/// </summary>
public sealed class ObservableDomainWatcherManager : IRealObject
{
    private readonly ConcurrentDictionary<string, ObservableDomainWatcher> _watchers = new();

    private ObservableDomainDriverHost _host = null!;

    // Set by AbortAll on ApplicationStopping: once stopping, new connections are refused so an
    // auto-reconnecting client cannot re-arm the full ShutdownTimeout drain.
    private volatile bool _stopping;

    private void StObjConstruct( ObservableDomainDriverHost host )
    {
        _host = host;
    }

    /// <summary>
    /// Registers <see cref="AbortAll"/> on <see cref="IHostApplicationLifetime.ApplicationStopping"/> so open
    /// connections are aborted before Kestrel starts draining (<see cref="OnHostStopAsync"/> is only a late backstop).
    /// </summary>
    private void OnHostStart( IActivityMonitor monitor, IHostApplicationLifetime lifetime )
    {
        // This real object is a process singleton: reset _stopping so the manager is reusable if a new
        // host starts on the same StObjMap (e.g. across tests) after a previous host stopped.
        _stopping = false;
        lifetime.ApplicationStopping.Register( AbortAll );
    }

    /// <summary>
    /// Handles the <see cref="IObservableDomainWatcherStartOrRestartCommand"/> to start or restart watching.
    /// It will use the ConnectionId to find the associated <see cref="ObservableDomainWatcher"/> and
    /// the domain to find the associated <see cref="IObservableDomainDriver"/>.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="command">The command to handle.</param>
    /// <returns>A JSON export of the started or restarted watch.</returns>
    [CommandHandler]
    public Task<string> HandleStartOrRestartWatchAsync( IActivityMonitor monitor, IObservableDomainWatcherStartOrRestartCommand command )
    {
        if( _watchers.TryGetValue( command.ConnectionId, out var watcher ) is false)
            Throw.InvalidDataException( $"{command.ConnectionId} does not exist, or is not identified as you." );

        return watcher.StartOrRestartWatchAsync( monitor, command.DomainName, command.TransactionNumber );
    }

    internal async Task<bool> CreateWatcherAsync( IWebsocketConnectionContext<ReadOnlyMemory<byte>> connection )
    {
        // Refuse new connections once stopping so a reconnect cannot re-arm the ShutdownTimeout drain.
        if( _stopping )
        {
            connection.Abort();
            return false;
        }

        var watcher = new ObservableDomainWatcher( _host, connection );
        if( _watchers.TryAdd( connection.ConnectionId, watcher ) is false )
        {
            await watcher.DisposeAsync().ConfigureAwait( false );
            return false;
        }

        // Re-check: AbortAll sets _stopping before iterating, so it may have missed this entry added just after.
        if( _stopping && _watchers.TryRemove( connection.ConnectionId, out _ ) )
        {
            connection.Abort();
            await watcher.DisposeAsync().ConfigureAwait( false );
            return false;
        }

        return true;
    }

    /// <summary>
    /// Sets <see cref="_stopping"/> then aborts every tracked connection (registered on ApplicationStopping by
    /// <see cref="OnHostStart"/>). Each watcher is then removed by <see cref="DestroyWatcherAsync"/> as its read loop ends.
    /// </summary>
    internal void AbortAll()
    {
        _stopping = true;
        foreach( var (_, watcher) in _watchers )
        {
            watcher.Abort();
        }
    }

    internal async Task<bool> DestroyWatcherAsync( string connectionId )
    {
        if( _watchers.TryRemove( connectionId, out var watcher ) )
        {
            await watcher.DisposeAsync().ConfigureAwait( false );
            return true;
        }

        return false;
    }

    private async Task OnHostStopAsync( IActivityMonitor monitor )
    {
        // Late backstop: AbortAll should already have closed everything. Abort (not just dispose) any straggler,
        // since DisposeAsync alone never aborts the socket.
        _stopping = true;
        foreach( var (connectionId, watcher) in _watchers )
        {
            if( _watchers.TryRemove( connectionId, out _ ) )
            {
                watcher.Abort();
                await watcher.DisposeAsync().ConfigureAwait( false );
            }
        }
    }
}
