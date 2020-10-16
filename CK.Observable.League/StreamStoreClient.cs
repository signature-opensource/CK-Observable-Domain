using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable.League
{
    /// <summary>
    /// This is the primary client that handles the transaction snapshot (an optional next client
    /// can be linked if needed).
    /// <para>
    /// It is specialized by <see cref="CoordinatorClient"/> for the coordinator domain
    /// and <see cref="ObservableLeague.DomainClient"/> for all the other domains.
    /// </para>
    /// </summary>
    abstract class StreamStoreClient : MemoryTransactionProviderClient
    {
        readonly string _storeName;
        readonly JsonEventCollector _eventCollector;
        readonly Action<IActivityMonitor, ObservableDomain>? _loadHook;
        int _savedTransactionNumber;
        DateTime _nextSave;
        int _snapshotSaveDelay;

        public StreamStoreClient( string domainName, IStreamStore store, Action<IActivityMonitor, ObservableDomain>? loadHook, IObservableDomainClient? next = null )
            : base( next )
        {
            _eventCollector = new JsonEventCollector();
            _loadHook = loadHook;
            DomainName = domainName;
            _storeName = domainName.Length == 0 ? "Coordinator" : "D-" + domainName;
            StreamStore = store;
            _savedTransactionNumber = -1;
        }

        /// <summary>
        /// Gets the <see cref="JsonEventCollector"/> that is in charge of the exported events.
        /// </summary>
        public JsonEventCollector JsonEventCollector => _eventCollector;

        /// <summary>
        /// Gets the domain name.
        /// </summary>
        public string DomainName { get; }

        /// <summary>
        /// Gets the stream store.
        /// </summary>
        public IStreamStore StreamStore { get; }

        /// <summary>
        /// See <see cref="ManagedDomainOptions.SnapshotSaveDelay"/>.
        /// </summary>
        public int SnapshotSaveDelay
        {
            get => _snapshotSaveDelay;
            set
            {
                if( _snapshotSaveDelay != value )
                {
                    if( value < 0 )
                    {
                        _snapshotSaveDelay = -1;
                        _nextSave = Util.UtcMaxValue;
                    }
                    else
                    {
                        _nextSave = CurrentTimeUtc.AddMilliseconds( _snapshotSaveDelay = value );
                    }
                }
            }
        }

        /// <summary>
        /// See <see cref="ManagedDomainOptions.SnapshotKeepDuration"/>.
        /// </summary>
        public TimeSpan SnapshotKeepDuration { get; set; } = TimeSpan.FromDays( 2 );

        /// <summary>
        /// See <see cref="ManagedDomainOptions.SnapshotMaximalTotalKiB"/>.
        /// </summary>
        public int SnapshotMaximalTotalKiB { get; set; } = 10 * 1024;

        /// <summary>
        /// Overridden to FIRST create a snapshot and THEN call the next client.
        /// </summary>
        /// <param name="c"></param>
        public override void OnTransactionCommit( in SuccessfulTransactionEventArgs c )
        {
            CreateSnapshot( c.Monitor, c.Domain, false );
            // We save the snapshot if we must (and there is no compensation for this of course).
            if( c.CommitTimeUtc >= _nextSave ) c.PostActions.Add( ctx => SaveAsync( ctx.Monitor ) );
            Next?.OnTransactionCommit( c );
        }

        protected override Action<ObservableDomain>? GetLoadHook( IActivityMonitor monitor, bool restoring )
        {
            return _loadHook != null
                        ? d => _loadHook( monitor, d )
                        : (Action<ObservableDomain>?)null;
        }

        protected override void CreateSnapshot( IActivityMonitor monitor, IObservableDomain d, bool initialOne )
        {
            if( initialOne && _loadHook != null && d.TransactionSerialNumber == 0 )
            {
                monitor.Debug( "The snapshot will be created by the InitializeAsync." );
            }
            else
            {
                base.CreateSnapshot( monitor, d, initialOne );
            }
        }

        /// <summary>
        /// Initializes the domain from the store or initializes the store from the domain.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="d">The domain to initialize.</param>
        /// <returns>The awaitable.</returns>
        public async Task InitializeAsync( IActivityMonitor monitor, ObservableDomain d )
        {
            using( var s = await StreamStore.OpenReadAsync( _storeName ) )
            {
                if( s != null )
                {
                    LoadAndInitializeSnapshot( monitor, d, s );
                    _savedTransactionNumber = CurrentSerialNumber;
                }
                else
                {
                    if( _loadHook != null )
                    {
                        // Applies the load hook to the newly created domain.
                        // Since this is an "initializer": new domains are de facto
                        // initialized just like the loaded ones.
                        await d.ModifyThrowAsync( monitor, () => _loadHook( monitor, d ) );
                        // It is useless to call CreateSnapshot: ModifyThrowAsync did commit the
                        // very first transaction.
                        Debug.Assert( d.TransactionSerialNumber == 1 );
                    }
                    else
                    {
                        // When there is no initializers, we call CreateSnapshot so that
                        // the initial snapshot can be saved to the Store: this initializes
                        // the Store for this domain.
                        // From now on, it will be reloaded.
                        CreateSnapshot( monitor, d, true );
                    }
                    if( !await SaveAsync( monitor ) )
                    {
                        throw new Exception( $"Unable to initialize the store for '{_storeName}'." );
                    }
                }
            }
        }

        /// <summary>
        /// Saves the current snapshot if the <see cref="MemoryTransactionProviderClient.CurrentSerialNumber"/> has changed
        /// since the last save.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <returns>True on success, false if an error occurred.</returns>
        public Task<bool> SaveAsync( IActivityMonitor monitor ) => DoSaveAsync( monitor, false );

        /// <summary>
        /// Archives the persistent file in the store: the domain's file is no more available.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        public Task ArchiveAsync( IActivityMonitor monitor ) => DoSaveAsync( monitor, true );

        async Task<bool> DoSaveAsync( IActivityMonitor monitor, bool sendToArchive )
        {
            if( _savedTransactionNumber != CurrentSerialNumber || sendToArchive )
            {
                try
                {
                    if( _savedTransactionNumber != CurrentSerialNumber )
                    {
                        await StreamStore.UpdateAsync( _storeName, WriteSnapshotAsync, true );
                        if( _snapshotSaveDelay >= 0 ) _nextSave = CurrentTimeUtc.AddMilliseconds( _snapshotSaveDelay );
                        _savedTransactionNumber = CurrentSerialNumber;
                    }
                    if( sendToArchive )
                    {
                        await StreamStore.DeleteAsync( _storeName, true );
                        monitor.Info( $"Domain '{_storeName}' saved and sent to archives." );
                    }
                    else monitor.Trace( $"Domain '{_storeName}' saved." );
                }
                catch( Exception ex )
                {
                    monitor.Error( $"While saving domain '{_storeName}'.", ex );
                    return false;
                }
            }
            return true;
        }

    }


}
