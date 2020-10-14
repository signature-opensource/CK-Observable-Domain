using CK.Core;
using System;
using CK.Observable.League;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Observable
{
    public abstract class ObservableWatcher : IDisposable
    {
        readonly ObservableLeague _league;
        readonly SemaphoreSlim _lock;
        readonly List<DomainWatcher> _watched;

        readonly struct DomainWatcher : IDisposable
        {
            readonly ObservableWatcher W;
            public readonly IObservableDomainLoader D;

            public DomainWatcher( ObservableWatcher w, IObservableDomainLoader d )
            {
                W = w;
                D = d;
                d.DomainChanged += DomainChanged;
            }

            public void Dispose()
            {
                D.DomainChanged -= DomainChanged;
            }

            void DomainChanged( IActivityMonitor monitor, JsonEventCollector.TransactionEvent e )
            {
                if( e.TransactionNumber == 1 ) W.HandleEvent( monitor, new WatchEvent( D.DomainName, "{\"N\":1,\"E\":[]}" ) );
                else
                {
                    W.HandleEvent( monitor, new WatchEvent( D.DomainName, "{\"N\":"+e.TransactionNumber+",\"E\":[["+ e.ExportedEvents +"]]}" ) );
                }
            }
        }

        public ObservableWatcher( ObservableLeague league )
        {
            _league = league;
            _lock = new SemaphoreSlim( 1, 1 );
            _watched = new List<DomainWatcher>();
        }

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
            ///     <item>A full export of the domain: an object with "N", "P", "C", "O", "R" members.</item>
            ///     <item>
            ///     A set of events to apply: an object with "N", "E" members.
            ///     Note that when "N":1, "E" is an empty array and a full export should be triggered.
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
        /// Ensures that the appropriate <see cref="WatchEvent"/> is handled by <see cref="HandleEvent(IActivityMonitor, WatchEvent)"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="domainName">The domain that should be watched.</param>
        /// <param name="transactionNumber">
        /// The starting transaction number.
        /// Should be between 1 and the current transaction number (excluded), 0 to trigger a full export.
        /// </param>
        /// <returns>The awaitable.</returns>
        public async Task StartOrRestartWatchAsync( IActivityMonitor monitor, string domainName, int transactionNumber )
        {
            var msg = await GetStartOrRestartEventAsync( monitor, domainName, transactionNumber );
            HandleEvent( monitor, msg );
        }

        async Task<WatchEvent> GetStartOrRestartEventAsync( IActivityMonitor monitor, string domainName, int transactionNumber )
        {
            IReadOnlyList<JsonEventCollector.TransactionEvent>? events = null;
            await _lock.WaitAsync();
            try
            {
                IObservableDomainLoader loader;
                var idx = _watched.IndexOf( loader => loader.D.DomainName == domainName );
                if( idx >= 0 )
                {
                    loader = _watched[idx].D;
                }
                else
                {
                    loader = _league.Find( domainName );
                }
                if( loader.IsDestroyed )
                {
                    if( idx >= 0 )
                    {
                        _watched[idx].Dispose();
                        _watched.RemoveAt( idx );
                        monitor.Trace( $"Client '{WatcherId}': lost '{domainName}'." );
                    }
                    return new WatchEvent( domainName, string.Empty );
                }
                events = loader.GetTransactionEvents( transactionNumber );
                if( events == null )
                {
                    await using( var shell = await loader.LoadAsync( monitor ) )
                    {
                        if( shell != null )
                        {
                            if( idx < 0 )
                            {
                                _watched.Add( new DomainWatcher( this, loader ) );
                            }
                            return new WatchEvent( domainName, shell.ExportToString() );
                        }
                    }
                    return new WatchEvent( domainName, string.Empty );
                }
            }
            finally
            {
                _lock.Release();
            }
            Debug.Assert( events != null );
            if( events.Count == 0 )
            {
                return new WatchEvent( domainName, "{\"Error\":\"Invalid transaction number.\"}" );
            }
            bool atLeastOne = false;
            StringBuilder b = new StringBuilder();
            b.Append( "{\"N\":" ).Append( events[events.Count - 1].TransactionNumber )
                .Append( "{\"E\":[" );
            foreach( var t in events )
            {
                if( atLeastOne ) b.Append( ',' );
                else atLeastOne = true;
                b.Append( '[' ).Append( t.ExportedEvents ).Append( "]" );
            }
            b.Append( "]}" );
            return new WatchEvent( domainName, b.ToString() );
        }

        /// <summary>
        /// Stops watching a domain.
        /// </summary>
        /// <param name="monitor"></param>
        /// <param name="domainName"></param>
        /// <returns></returns>
        public async ValueTask UnwatchAsync( IActivityMonitor monitor, string domainName )
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
                }
                else
                {
                    monitor.Warn( $"Client '{WatcherId}': '{domainName}' not found. Unwatch skipped." );
                }
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
        internal protected abstract void HandleEvent( IActivityMonitor monitor, WatchEvent e );

        /// <summary>
        /// Gets the watcher identifier.
        /// This is primarily used for logs.
        /// </summary>
        protected abstract string WatcherId { get; }

    }
}
