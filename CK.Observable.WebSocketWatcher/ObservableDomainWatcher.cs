using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CK.Core;
using SimpleR;

namespace CK.Observable.WebSocketWatcher;

/// <summary>
/// Manages domain subscriptions for a single WebSocket connection.
/// <para>
/// A client can watch multiple <see cref="ObservableDomain"/> simultaneously through the same connection.
/// Each watched domain is backed by a <see cref="DomainSubscription"/> that pushes transaction events
/// to the client in real time via the <see cref="JsonEventCollector.LastEventChanged"/> event.
/// </para>
/// </summary>
public sealed class ObservableDomainWatcher : IAsyncDisposable
{
    private readonly ObservableDomainDriverHost _host;
    private readonly IWebsocketConnectionContext<ReadOnlyMemory<byte>> _connection;
    private readonly SemaphoreSlim _lock;
    private readonly SemaphoreSlim _writeLock;
    private readonly Dictionary<string, DomainSubscription> _watched;
    // Guards against in-flight event handlers writing to a disposed connection,
    // and prevents double-dispose of semaphores if disposal paths ever overlap.
    private volatile bool _disposed;

    /// <summary>
    /// Initializes a new <see cref="ObservableDomainWatcher"/> for the given WebSocket connection.
    /// </summary>
    /// <param name="host">The driver host used to resolve domains by name.</param>
    /// <param name="connection">The WebSocket connection to push events to.</param>
    public ObservableDomainWatcher( ObservableDomainDriverHost host,
                                    IWebsocketConnectionContext<ReadOnlyMemory<byte>> connection )
    {
        _host = host;
        _connection = connection;
        _lock = new SemaphoreSlim( 1, 1 );
        _writeLock = new SemaphoreSlim( 1, 1 );
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
                monitor.Trace( $"Client '{_connection.ConnectionId}' unwatched '{domainName}'." );
            }
            else
            {
                monitor.Warn( $"Client '{_connection.ConnectionId}': '{domainName}' not found. Unwatch skipped." );
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private async ValueTask WriteAsync( ReadOnlyMemory<byte> message )
    {
        if( _disposed ) return; // In-flight event after dispose: silently bail out.
        await _writeLock.WaitAsync().ConfigureAwait( false );
        try
        {
            if( _disposed ) return; // Dispose happened while waiting for the lock.
            await _connection.WriteAsync( message ).ConfigureAwait( false );
        }
        finally
        {
            _writeLock.Release();
        }
    }

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
        _writeLock.Dispose();
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
                writer.WriteRawValue( e.ExportedEvents );
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
