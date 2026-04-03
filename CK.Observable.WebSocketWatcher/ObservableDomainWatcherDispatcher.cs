using System;
using System.Buffers;
using System.Text.Json;
using System.Threading.Tasks;
using SimpleR;

namespace CK.Observable.WebSocketWatcher;

/// <summary>
/// SimpleR dispatcher that manages <see cref="ObservableDomainWatcher"/> lifecycle
/// for each WebSocket connection on the <c>/ws/observable</c> endpoint.
/// <para>
/// On connect, creates a watcher and sends back a JSON message containing the connection identifier.
/// On disconnect, destroys the watcher and releases all domain subscriptions.
/// </para>
/// </summary>
public sealed class ObservableDomainWatcherDispatcher : IWebSocketMessageDispatcher<string, ReadOnlyMemory<byte>>
{
    private readonly ObservableDomainWatcherManager _manager;

    public ObservableDomainWatcherDispatcher( ObservableDomainWatcherManager manager )
    {
        _manager = manager;
    }

    /// <summary>
    /// Creates a new <see cref="ObservableDomainWatcher"/> and sends back a JSON message containing the connection identifier.
    /// </summary>
    /// <param name="connection">The newly established connection.</param>
    public async Task OnConnectedAsync( IWebsocketConnectionContext<ReadOnlyMemory<byte>> connection )
    {
        _manager.CreateWatcher( connection );
        var buffer = new ArrayBufferWriter<byte>( 256 );
        await using var writer = new Utf8JsonWriter( buffer );
        writer.WriteStartObject();
        writer.WriteString( "connectionId", connection.ConnectionId );
        writer.WriteEndObject();
        await writer.FlushAsync();
        await connection.WriteAsync( buffer.WrittenMemory );
    }

    /// <summary>
    /// Destroys the <see cref="ObservableDomainWatcher"/> and releases all domain subscriptions.
    /// </summary>
    /// <param name="connection">The connection that is being disconnected.</param>
    /// <param name="exception">The exception that caused the disconnection, if any.</param>
    public async Task OnDisconnectedAsync( IWebsocketConnectionContext<ReadOnlyMemory<byte>> connection, Exception? exception )
    {
        await _manager.DestroyWatcherAsync( connection.ConnectionId );
    }

    /// <summary>
    /// Does nothing as we don't support sending messages yet.
    /// </summary>
    /// <param name="connection">The connection to send a message on.</param>
    /// <param name="message">The message to send.</param>
    /// <returns></returns>
    public Task DispatchMessageAsync( IWebsocketConnectionContext<ReadOnlyMemory<byte>> connection, string message )
    {
        return Task.CompletedTask;
    }
}
