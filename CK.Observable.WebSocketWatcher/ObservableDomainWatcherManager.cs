using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using CK.AspNet.WebSocketChannel;
using CK.Core;
using CK.Cris;

namespace CK.Observable.WebSocketWatcher;

/// <summary>
/// Singleton that tracks the <see cref="ObservableDomainWatcher"/> of the connections that actually
/// watch a domain, keyed by connection identifier.
/// <para>
/// Handles the <see cref="IObservableDomainWatcherStartOrRestartCommand"/> to start or restart watching
/// a domain for a given connection.
/// </para>
/// <para>
/// The socket, its lifecycle and the shutdown drain belong to <see cref="WebSocketChannelManager"/>:
/// this object only reacts to the connections it sees closing, and pushes under <see cref="Topic"/>.
/// </para>
/// </summary>
public sealed class ObservableDomainWatcherManager : IRealObject
{
    /// <summary>
    /// The channel topic of the observable domain events. The TypeScript client matches on this exact
    /// string (see <c>WebSocketObservableDomainConnection</c>).
    /// </summary>
    public const string Topic = "OD";

    private readonly ConcurrentDictionary<string, ObservableDomainWatcher> _watchers = new();

    private ObservableDomainDriverHost _host = null!;
    private WebSocketChannelManager _channel = null!;

    private void StObjConstruct( ObservableDomainDriverHost host, WebSocketChannelManager channel )
    {
        _host = host;
        _channel = channel;
    }

    /// <summary>
    /// Subscribes to the channel here rather than in <c>OnHostStart</c>: several hosts can start on the
    /// same StObjMap (tests do), and a subscription per start would pile handlers up. StObjInitialize
    /// runs once per map.
    /// </summary>
    private void StObjInitialize( IActivityMonitor monitor, IStObjObjectMap map )
    {
        _channel.ConnectionClosed.Async += OnConnectionClosedAsync;
    }

    private async Task OnConnectionClosedAsync( IActivityMonitor monitor, ConnectionClosedEvent e, CancellationToken cancel )
    {
        if( _watchers.TryRemove( e.ConnectionId, out var watcher ) )
        {
            await watcher.DisposeAsync().ConfigureAwait( false );
        }
    }

    /// <summary>
    /// Handles the <see cref="IObservableDomainWatcherStartOrRestartCommand"/> to start or restart watching.
    /// It will use the ConnectionId to find the connection on the channel and the domain to find the
    /// associated <see cref="IObservableDomainDriver"/>.
    /// <para>
    /// The watcher is created on demand: a client that never watches a domain costs nothing here, which
    /// matters now that every feature shares the same connections.
    /// </para>
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="command">The command to handle.</param>
    /// <returns>A JSON export of the started or restarted watch.</returns>
    [CommandHandler]
    public async Task<string> HandleStartOrRestartWatchAsync( IActivityMonitor monitor, IObservableDomainWatcherStartOrRestartCommand command )
    {
        if( _channel.TryGetConnection( command.ConnectionId, out _ ) is false )
            Throw.InvalidDataException( $"{command.ConnectionId} does not exist, or is not identified as you." );

        var watcher = _watchers.GetOrAdd( command.ConnectionId, id => new ObservableDomainWatcher( _host, _channel, id ) );

        // The connection can vanish between the check above and this line: the closed event would then
        // have found nothing to remove, and the watcher would stay forever. Clean up rather than leak.
        if( _channel.TryGetConnection( command.ConnectionId, out _ ) is false )
        {
            if( _watchers.TryRemove( command.ConnectionId, out var orphan ) )
            {
                await orphan.DisposeAsync().ConfigureAwait( false );
            }
            Throw.InvalidDataException( $"{command.ConnectionId} does not exist, or is not identified as you." );
        }

        return await watcher.StartOrRestartWatchAsync( monitor, command.DomainName, command.TransactionNumber ).ConfigureAwait( false );
    }
}
