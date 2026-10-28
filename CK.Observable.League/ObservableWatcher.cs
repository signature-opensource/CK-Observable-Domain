using CK.Core;
using System;
using CK.Observable.League;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Observable;

/// <summary>
/// Base abstract class that watches any number of domains in a <see cref="IObservableLeague"/>.
/// This class is thread safe.
/// </summary>
public abstract class ObservableWatcher : IDisposable
{
    readonly IObservableLeague _league;
    readonly SemaphoreSlim _lock;
    readonly List<DomainWatcher> _watched;

    class DomainWatcher : IDisposable
    {
        readonly ObservableWatcher _w;
        public readonly IObservableDomainLoader D;
        public DomainWatcher( ObservableWatcher w, IObservableDomainLoader d )
        {
            _w = w;
            D = d;
            d.DomainChanged.Async += DomainChangedAsync;
        }

        public void Dispose()
        {
            D.DomainChanged.Async -= DomainChangedAsync;
        }

        async Task DomainChangedAsync( IActivityMonitor monitor, JsonEventCollector.TransactionEvent e, CancellationToken cancellation )
        {
            // The clients cannot apply the events of this transaction: they receive the full export.
            if( e.DomainExport != null ) await _w.HandleEventAsync( monitor, new WatchEvent( D.DomainName, e.DomainExport ) );
            else if( e.TransactionNumber == 1 ) await _w.HandleEventAsync( monitor, new WatchEvent( D.DomainName, "{\"N\":1,\"E\":[],\"L\":0}" ) );
            else
            {
                await _w.HandleEventAsync( monitor, new WatchEvent(
                    D.DomainName,
                    "{\"N\":" + e.TransactionNumber +
                    ",\"E\":[[" + e.ExportedEvents + "]],\"L\":" + e.LastExportedTransactionNumber + "}"
                    ) );
            }
        }
    }

    /// <summary>
    /// Initializes a new watcher on a league.
    /// <see cref="StartOrRestartWatchAsync(IActivityMonitor, string, int)"/> must be called for each
    /// domain that must be watched.
    /// </summary>
    /// <param name="league">The league.</param>
    public ObservableWatcher( IObservableLeague league )
    {
        _league = league;
        _lock = new SemaphoreSlim( 1, 1 );
        _watched = new List<DomainWatcher>();
    }

    /// <summary>
    /// Disposing this watcher cancels all the subscriptions on the league and the currently watched domains.
    /// </summary>
    public void Dispose()
    {
        // We don't use AvailableWaitHandle, so we don't need to
        // dispose the SemaphoreSlim.
        // This greatly reduces the pain here.
        _lock.Wait();
        try
        {
            foreach( var w in _watched ) w.Dispose();
            _watched.Clear();
        }
        finally
        {
            _lock.Release();
        }

    }

    /// <summary>
    /// Defines a watch event.
    /// </summary>
    public readonly struct WatchEvent
    {
        /// <summary>
        /// The domain name.
        /// </summary>
        public readonly string DomainName;

        /// <summary>
        /// The Json string that is either:
        /// <list type="bullet">
        ///     <item>An object with a "Error" string member.</item>
        ///     <item>
        ///     A full export of the domain: an object with "N", "P", "C", "O", "R" members.
        ///     This is the answer to a start or restart, and the push of a transaction that does not follow the previous one.
        ///     </item>
        ///     <item>
        ///     A set of events to apply: an object with "N", "E", "L" members.
        ///     Note that when "N":1, "E" is an empty array: the client must ask for a full export. This happens only when the
        ///     domain cannot be exported.
        ///     </item>
        ///     <item>An empty string if the domain doesn't exist (or has been destroyed).</item>
        /// </list>
        /// </summary>
        public readonly string JsonExport;

        internal WatchEvent( string name, string json )
        {
            DomainName = name;
            JsonExport = json;
        }
    }

    /// <summary>
    /// Starts the watch of a domain.
    /// The <see cref="HandleEventAsync(IActivityMonitor, WatchEvent)"/> is called with the
    /// appropriate <see cref="WatchEvent"/>.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="domainName">The domain that should be watched.</param>
    /// <param name="transactionNumber">
    /// The transaction number of the client. It is ignored: the client always receives a full export,
    /// because its number cannot prove that its state is the one of the domain.
    /// </param>
    /// <returns>The awaitable.</returns>
    public async Task StartOrRestartWatchAsync( IActivityMonitor monitor, string domainName, int transactionNumber )
    {
        WatchEvent msg = await GetStartOrRestartEventAsync( monitor, domainName, transactionNumber );
        await HandleEventAsync( monitor, msg );
    }

    public async Task<WatchEvent> GetStartOrRestartEventAsync( IActivityMonitor monitor, string domainName, int transactionNumber )
    {
        await _lock.WaitAsync();
        try
        {
            IObservableDomainLoader? loader;
            var idx = _watched.IndexOf( loader => loader.D.DomainName == domainName );
            if( idx >= 0 )
            {
                loader = _watched[idx].D;
            }
            else
            {
                loader = _league.Find( domainName );
                if( loader == null )
                {
                    monitor.Warn( $"Unable to find domain '{domainName}'." );
                    return new WatchEvent( domainName, string.Empty );
                }
            }
            Debug.Assert( loader != null );
            if( loader.IsDestroyed )
            {
                if( idx >= 0 )
                {
                    _watched[idx].Dispose();
                    _watched.RemoveAt( idx );
                    monitor.Trace( $"Client '{WatcherId}': lost '{domainName}'." );
                }
                monitor.Warn( $"Domain '{domainName}' has been destroyed." );
                return new WatchEvent( domainName, string.Empty );
            }
            if( idx < 0 )
            {
                _watched.Add( new DomainWatcher( this, loader ) );
            }

            // The client transaction number cannot prove that the client state is the one of the domain: after a restart
            // from an older snapshot or a dangerous rollback, the domain can reach this number with another history.
            // The client always receives the full export.
            await using( var shell = await loader.LoadAsync( monitor ) )
            {
                if( shell != null )
                {
                    return new WatchEvent( domainName, shell.ExportToString() );
                }
            }
            monitor.Warn( $"Unable to load the Domain '{domainName}'." );
            return new WatchEvent( domainName, string.Empty );
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Stops watching a domain.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="domainName">The domain that should not be watched anymore.</param>
    /// <returns>
    /// Whether the <paramref name="domainName"/> has been found: false is the domain doesn't exist or was not watched.
    /// </returns>
    public async ValueTask<bool> UnwatchAsync( IActivityMonitor monitor, string domainName )
    {
        await _lock.WaitAsync();
        try
        {
            var idx = _watched.IndexOf( loader => loader.D.DomainName == domainName );
            if( idx >= 0 )
            {
                var w = _watched[idx];
                w.Dispose();
                _watched.RemoveAt( idx );
                monitor.Trace( $"Client '{WatcherId}' unwatch '{domainName}'." );
                return true;
            }
            monitor.Warn( $"Client '{WatcherId}': '{domainName}' not found. Unwatch skipped." );
            return false;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Must handle the message.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="e">The watch event.</param>
    internal protected abstract ValueTask HandleEventAsync( IActivityMonitor monitor, WatchEvent e );

    /// <summary>
    /// Gets the watcher identifier.
    /// This is primarily used for logs.
    /// </summary>
    protected abstract string WatcherId { get; }

}

