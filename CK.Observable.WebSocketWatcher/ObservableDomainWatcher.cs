using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CK.AspNet.WebSocketChannel;
using CK.Core;

namespace CK.Observable.WebSocketWatcher;

/// <summary>
/// Manages domain subscriptions for a single WebSocket connection.
/// <para>
/// A client can watch multiple <see cref="ObservableDomain"/> simultaneously through the same connection.
/// Each watched domain is backed by a <see cref="DomainSubscription"/> that pushes transaction events
/// to the client in real time via the <see cref="JsonEventCollector.LastEventChanged"/> event.
/// </para>
/// <para>
/// This watcher does not own the socket: it pushes on the application-wide channel under the
/// <see cref="ObservableDomainWatcherManager.Topic"/> topic. Other features push on the same socket
/// under theirs, and the channel serializes the writes.
/// </para>
/// </summary>
public sealed class ObservableDomainWatcher : IAsyncDisposable
{
    private readonly ObservableDomainDriverHost _host;
    private readonly WebSocketChannelManager _channel;
    private readonly string _connectionId;
    private readonly SemaphoreSlim _lock;
    private readonly Dictionary<string, DomainSubscription> _watched;
    // Guards against in-flight event handlers pushing for a released watcher, and prevents
    // double-dispose of the semaphore if disposal paths ever overlap.
    private volatile bool _disposed;

    /// <summary>
    /// Initializes a new <see cref="ObservableDomainWatcher"/> for the given connection.
    /// </summary>
    /// <param name="host">The driver host used to resolve domains by name.</param>
    /// <param name="channel">The channel to push events on.</param>
    /// <param name="connectionId">The connection this watcher belongs to.</param>
    public ObservableDomainWatcher( ObservableDomainDriverHost host,
                                    WebSocketChannelManager channel,
                                    string connectionId )
    {
        _host = host;
        _channel = channel;
        _connectionId = connectionId;
        _lock = new SemaphoreSlim( 1, 1 );
        _watched = new Dictionary<string, DomainSubscription>();
    }

    /// <summary>
    /// Starts or restarts watching a domain. If the client's <paramref name="transactionNumber"/> is still
    /// available in the event collector, returns only the missed events; otherwise returns a full domain export.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="domainName">The name of the domain to watch.</param>
    /// <param name="transactionNumber">The last transaction number known by the client (0 for a full export).</param>
    /// <returns>A JSON string with the events or the full export, or <see cref="string.Empty"/> on error.</returns>
    public async Task<string> StartOrRestartWatchAsync( IActivityMonitor monitor, string domainName, int transactionNumber )
    {
        await _lock.WaitAsync().ConfigureAwait( false );
        try
        {
            if( !_host.Drivers.TryGetValue( domainName, out var driver ) )
            {
                monitor.Warn( $"Unable to find domain '{domainName}'." );
                return string.Empty;
            }

            if( !_watched.ContainsKey( domainName ) )
            {
                _watched.Add( domainName, new DomainSubscription( this, driver ) );
            }

            var (currentTransactionNumber, events) = driver.EventCollector.GetTransactionEvents( transactionNumber );

            string eventsJson;
            if( events == null )
            {
                var export = driver.Domain.ExportToString();
                if( export == null )
                {
                    monitor.Warn( $"Unable to export domain '{domainName}'." );
                    return string.Empty;
                }
                eventsJson = export;
            }
            else if( events.Count == 0 )
            {
                if( currentTransactionNumber > transactionNumber )
                {
                    eventsJson = "{\"Error\":\"Invalid transaction number.\"}";
                }
                else
                {
                    eventsJson = $"{{\"N\":{currentTransactionNumber},\"E\":[]}}";
                }
            }
            else
            {
                var b = new StringBuilder();
                b.Append( "{\"N\":" )
                    .Append( events[^1].TransactionNumber )
                    .Append( ",\"E\":[" );
                var atLeastOne = false;
                foreach( var t in events )
                {
                    if( atLeastOne ) b.Append( ',' );
                    else atLeastOne = true;
                    b.Append( '[' ).Append( t.ExportedEvents ).Append( ']' );
                }
                b.Append( "]}" );
                eventsJson = b.ToString();
            }

            return eventsJson;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Stops watching a domain and disposes the corresponding subscription.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="domainName">The name of the domain to stop watching.</param>
    public async Task UnwatchAsync( IActivityMonitor monitor, string domainName )
    {
        await _lock.WaitAsync().ConfigureAwait( false );
        try
        {
            if( _watched.Remove( domainName, out var subscription ) )
            {
                subscription.Dispose();
                monitor.Trace( $"Client '{_connectionId}' unwatched '{domainName}'." );
            }
            else
            {
                monitor.Warn( $"Client '{_connectionId}': '{domainName}' not found. Unwatch skipped." );
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private ValueTask WriteAsync( ReadOnlyMemory<byte> message )
    {
        // In-flight event after dispose: silently bail out. Pushing on a connection that is gone is
        // already a no-op on the channel side; this only avoids building the envelope for nothing.
        if( _disposed ) return ValueTask.CompletedTask;
        return _channel.SendAsync( _connectionId, ObservableDomainWatcherManager.Topic, message );
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if( _disposed ) return; // Already disposed.
        _disposed = true;
        await _lock.WaitAsync().ConfigureAwait( false );
        try
        {
            foreach( var sub in _watched.Values )
                sub.Dispose();
            _watched.Clear();
        }
        finally
        {
            _lock.Release();
        }
        _lock.Dispose();
    }

    private sealed class DomainSubscription : IDisposable
    {
        private readonly ObservableDomainWatcher _watcher;
        private readonly IObservableDomainDriver _driver;

        public DomainSubscription( ObservableDomainWatcher watcher, IObservableDomainDriver driver )
        {
            _watcher = watcher;
            _driver = driver;
            driver.EventCollector.LastEventChanged.Async += OnDomainChangedAsync;
        }

        public void Dispose()
        {
            _driver.EventCollector.LastEventChanged.Async -= OnDomainChangedAsync;
        }

        private async Task OnDomainChangedAsync( IActivityMonitor monitor, JsonEventCollector.TransactionEvent e, CancellationToken cancellation )
        {
            var buffer = new ArrayBufferWriter<byte>();
            await using var writer = new Utf8JsonWriter( buffer );
            writer.WriteStartArray();
            writer.WriteStringValue( _driver.DomainName );
            writer.WriteStartObject();
            if( e.TransactionNumber == 1 )
            {
                writer.WriteNumber( "N", 1 );
                writer.WritePropertyName( "E" );
                writer.WriteRawValue( "[]"u8 );
                writer.WriteNumber( "L", 0 );
            }
            else
            {
                writer.WriteNumber( "N", e.TransactionNumber );
                writer.WritePropertyName( "E" );
                writer.WriteStartArray();
                writer.WriteStartArray();
                writer.WriteRawValue( e.ExportedEvents, skipInputValidation: true );
                writer.WriteEndArray();
                writer.WriteEndArray();
                writer.WriteNumber( "L", e.LastExportedTransactionNumber );
            }
            writer.WriteEndObject();
            writer.WriteEndArray();
            await writer.FlushAsync( cancellation ).ConfigureAwait( false );

            await _watcher.WriteAsync( buffer.WrittenMemory ).ConfigureAwait( false );
        }
    }
}
