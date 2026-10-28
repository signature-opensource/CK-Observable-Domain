using System;
using System.Buffers;
using System.Collections.Generic;
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
    readonly ObservableDomainDriverHost _host;
    readonly WebSocketChannelConnection _connection;
    readonly SemaphoreSlim _lock;
    readonly Dictionary<string, DomainSubscription> _watched;
    // Guards against in-flight event handlers pushing for a released watcher, and prevents
    // double-dispose of the semaphore if disposal paths ever overlap.
    volatile bool _disposed;

    /// <summary>
    /// Initializes a new <see cref="ObservableDomainWatcher"/> for the given connection.
    /// </summary>
    /// <param name="host">The driver host used to resolve domains by name.</param>
    /// <param name="connection">The connection.</param>
    public ObservableDomainWatcher( ObservableDomainDriverHost host, WebSocketChannelConnection connection )
    {
        _host = host;
        _connection = connection;
        _lock = new SemaphoreSlim( 1, 1 );
        _watched = new Dictionary<string, DomainSubscription>();
    }

    /// <summary>
    /// Starts or restarts watching a domain and returns the full domain export.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="domainName">The name of the domain to watch.</param>
    /// <param name="transactionNumber">
    /// The last transaction number known by the client. It is ignored: the client always receives a full export,
    /// because its number cannot prove that its state is the one of the domain.
    /// </param>
    /// <returns>A JSON string with the full export, or <see cref="string.Empty"/> on error.</returns>
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

            // The client transaction number cannot prove that the client state is the one of the domain: after a restart
            // or a dangerous rollback, the domain can reach this number with another history.
            // The client always receives the full export.
            var export = driver.Domain.ExportToString();
            if( export == null )
            {
                monitor.Warn( $"Unable to export domain '{domainName}'." );
                return string.Empty;
            }
            return export;
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

    ValueTask WriteAsync( ReadOnlyMemory<byte> message )
    {
        // In-flight event after dispose: silently bail out. Pushing on a connection that is gone is
        // already a no-op on the channel side; this only avoids building the envelope for nothing.
        return _disposed
                ? ValueTask.CompletedTask
                : _connection.WriteAsync( ObservableDomainWatcherManager.Topic, message );
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
            var message = CreatePushMessage( _driver.DomainName,
                                             e.TransactionNumber,
                                             e.ExportedEvents,
                                             e.LastExportedTransactionNumber,
                                             e.DomainExport );
            await _watcher.WriteAsync( message ).ConfigureAwait( false );
        }
    }

    /// <summary>
    /// Creates the message pushed to the client for a <see cref="JsonEventCollector.TransactionEvent"/>:
    /// a JSON array with the domain name and the watch event.
    /// </summary>
    /// <param name="domainName">The domain name.</param>
    /// <param name="transactionNumber">The <see cref="JsonEventCollector.TransactionEvent.TransactionNumber"/>.</param>
    /// <param name="exportedEvents">The <see cref="JsonEventCollector.TransactionEvent.ExportedEvents"/>.</param>
    /// <param name="lastExportedTransactionNumber">The <see cref="JsonEventCollector.TransactionEvent.LastExportedTransactionNumber"/>.</param>
    /// <param name="domainExport">The <see cref="JsonEventCollector.TransactionEvent.DomainExport"/>.</param>
    /// <returns>The UTF-8 JSON message.</returns>
    internal static ReadOnlyMemory<byte> CreatePushMessage( string domainName,
                                                            int transactionNumber,
                                                            string exportedEvents,
                                                            int lastExportedTransactionNumber,
                                                            string? domainExport )
    {
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter( buffer );
        writer.WriteStartArray();
        writer.WriteStringValue( domainName );
        if( domainExport != null )
        {
            // The client cannot apply the events of this transaction: it receives the full export.
            writer.WriteRawValue( domainExport, skipInputValidation: true );
        }
        else
        {
            writer.WriteStartObject();
            if( transactionNumber == 1 )
            {
                writer.WriteNumber( "N", 1 );
                writer.WritePropertyName( "E" );
                writer.WriteRawValue( "[]"u8 );
                writer.WriteNumber( "L", 0 );
            }
            else
            {
                writer.WriteNumber( "N", transactionNumber );
                writer.WritePropertyName( "E" );
                writer.WriteStartArray();
                writer.WriteStartArray();
                // A transaction that changed nothing has no exported events: WriteRawValue rejects an empty string.
                if( exportedEvents.Length > 0 ) writer.WriteRawValue( exportedEvents, skipInputValidation: true );
                writer.WriteEndArray();
                writer.WriteEndArray();
                writer.WriteNumber( "L", lastExportedTransactionNumber );
            }
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.Flush();
        return buffer.WrittenMemory;
    }
}
