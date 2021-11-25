using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
        readonly IStreamStore _streamStore;
        readonly string _storeName;
        readonly JsonEventCollector _eventCollector;
        int _savedTransactionNumber;
        int _savesBeforeNextHousekeeping;
        DateTime _nextSave;
        int _snapshotSaveDelay;

        public StreamStoreClient( string domainName, IStreamStore store, IObservableDomainClient? next = null )
            : base( next )
        {
            _eventCollector = new JsonEventCollector();
            DomainName = domainName;
            _storeName = domainName.Length == 0 ? "Coordinator" : "D-" + domainName;
            _streamStore = store;
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
        /// See <see cref="ManagedDomainOptions.HousekeepingRate"/>.
        /// </summary>
        public int HousekeepingRate { get; set; } = 0;

        /// <summary>
        /// Overridden to FIRST create a snapshot and THEN call the next client.
        /// </summary>
        /// <param name="c"></param>
        public override void OnTransactionCommit( in SuccessfulTransactionEventArgs c )
        {
            CreateSnapshot( c.Monitor, c.Domain, false, c.HasSaveCommand );
            // We save the snapshot if we must (and there is no compensation for this of course).
            if( c.CommitTimeUtc >= _nextSave ) c.PostActions.Add( ctx => SaveAsync( ctx.Monitor ) );
            Next?.OnTransactionCommit( c );
        }

        public override void OnTransactionStart( IActivityMonitor monitor, ObservableDomain d, DateTime timeUtc )
        {
            // Avoids the call to CreateSnapshot: InitializeAsync below handles the initialization.
            Next?.OnTransactionStart( monitor, d, timeUtc );
        }

        /// <summary>
        /// See base <see cref="MemoryTransactionProviderClient.LoadOrCreateAndInitializeSnapshot"/> comments.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="stream">The stream from which the domain must be deserialized.</param>
        /// <param name="startTimer">Whether to start the domain's <see cref="TimeManager"/>.</param>
        /// <returns>Never: throws a <see cref="NotSupportedException"/>.</returns>
        protected override sealed ObservableDomain DeserializeDomain( IActivityMonitor monitor, Stream stream, bool? startTimer )
        {
            var d = DoDeserializeDomain( monitor, stream, startTimer );
            _eventCollector.CollectEvent( d, clearEvents: false );
            return d;
        }

        protected abstract ObservableDomain DoDeserializeDomain( IActivityMonitor monitor, Stream stream, bool? startTimer );

        /// <summary>
        /// Initializes the domain from the store or initializes the store with a new domain.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="factory">The domain factory to use if no stream exists in the store.</param>
        /// <param name="startTimer">Whether to start the <see cref="TimeManager"/>.</param>
        /// <returns>The awaitable.</returns>
        public async Task<ObservableDomain> InitializeAsync( IActivityMonitor monitor, bool? startTimer, bool createOnLoadError, Func<IActivityMonitor,bool,ObservableDomain> factory )
        {
            ObservableDomain result = null;
            using( var s = await _streamStore.OpenReadAsync( _storeName ) )
            {
                if( s != null )
                {
                    try
                    {
                        LoadOrCreateAndInitializeSnapshot( monitor, ref result, s, startTimer );
                        _savedTransactionNumber = CurrentSerialNumber;
                    }
                    catch( Exception ex )
                    {
                        if( createOnLoadError )
                        {
                            monitor.Error( $"Error while loading domain. Automatically recreating a new one and initializing it.", ex );
                            result = await Create( monitor, startTimer, factory );
                        }
                        else
                        {
                            throw;
                        }
                    }
                }
                else
                {
                    result = await Create( monitor, startTimer, factory );
                }
            }
            return result;

            async Task<ObservableDomain> Create( IActivityMonitor monitor, bool? startTimer, Func<IActivityMonitor, bool, ObservableDomain> factory )
            {
                ObservableDomain result = factory( monitor, startTimer ?? false );
                _eventCollector.CollectEvent( result, clearEvents: true );
                // Calling CreateSnapshot so that
                // the initial snapshot can be saved to the Store: this initializes
                // the Store for this domain. From now on, it will be reloaded.
                CreateSnapshot( monitor, result, true, true );
                if( !await SaveAsync( monitor ) )
                {
                    throw new Exception( $"Unable to initialize the store for '{_storeName}'." );
                }
                return result;
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
                        await _streamStore.UpdateAsync( _storeName, WriteSnapshotAsync, true ).ConfigureAwait( false );
                        if( _snapshotSaveDelay >= 0 ) _nextSave = CurrentTimeUtc.AddMilliseconds( _snapshotSaveDelay );
                        _savedTransactionNumber = CurrentSerialNumber;
                    }
                    if( sendToArchive )
                    {
                        await _streamStore.DeleteAsync( _storeName, true ).ConfigureAwait( false );
                        monitor.Info( $"Domain '{_storeName}' saved and sent to archives." );
                    }
                    else monitor.Trace( $"Domain '{_storeName}' saved." );

                    if( HousekeepingRate > 0 && _streamStore is IBackupStreamStore backupStreamStore )
                    {
                        --_savesBeforeNextHousekeeping;
                        if( _savesBeforeNextHousekeeping <= 0
                            && (SnapshotKeepDuration > TimeSpan.Zero || SnapshotMaximalTotalKiB > 0) )
                        {
                            using( monitor.OpenTrace( "Executing housekeeping for snapshot backups." ) )
                            {
                                _savesBeforeNextHousekeeping = HousekeepingRate;
                                backupStreamStore.CleanBackups( monitor, _storeName, SnapshotKeepDuration, SnapshotMaximalTotalKiB * 1024L );
                            }
                        }
                    }

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
