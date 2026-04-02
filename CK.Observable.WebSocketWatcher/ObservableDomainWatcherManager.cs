using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using CK.Core;
using CK.Cris;
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

    private void StObjConstruct( ObservableDomainDriverHost host )
    {
        _host = host;
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

    internal bool CreateWatcher( IWebsocketConnectionContext<ReadOnlyMemory<byte>> connection )
    {
        var watcher = new ObservableDomainWatcher( _host, connection );
        if( _watchers.TryAdd( connection.ConnectionId, watcher ) is false )
        {
            watcher.Dispose();
            return false;
        }

        return true;
    }

    internal bool DestroyWatcher( string connectionId )
    {
        if( _watchers.TryRemove( connectionId, out var watcher ) )
        {
            watcher.Dispose();
            return true;
        }

        return false;
    }
}
