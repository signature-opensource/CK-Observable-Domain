using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable.League
{
    /// <summary>
    /// This is the primary, first client. It handles the transaction snapshot and
    /// associates a <see cref="TransactClient"/> as its direct next client.
    /// </summary>
    class StreamStoreClient : MemoryTransactionProviderClient
    {
        readonly string _storeName;
        int _savedTransactionNumber;
        DateTime _nextSave;
        int _autoSaveTime;

        public StreamStoreClient( string domainName, IStreamStore store, IObservableDomainClient? next = null )
            : base( new TransactionEventCollectorClient( next ) )
        {
            DomainName = domainName;
            _storeName = domainName.Length == 0 ? "Coordinator" : "D-" + domainName;
            StreamStore = store;
            _savedTransactionNumber = -1;
        }

        /// <summary>
        /// Gets the next client that is in charge of the exported events.
        /// </summary>
        public TransactionEventCollectorClient TransactClient => (TransactionEventCollectorClient)Next!;

        /// <summary>
        /// Gets the domain name.
        /// </summary>
        public string DomainName { get; }

        /// <summary>
        /// Gets the stream store.
        /// </summary>
        public IStreamStore StreamStore { get; }

        /// <summary>
        /// See <see cref="ManagedDomainOptions.AutoSaveTime"/>.
        /// </summary>
        public int AutoSaveTime
        {
            get => _autoSaveTime;
            set
            {
                if( _autoSaveTime != value )
                {
                    if( value < 0 )
                    {
                        _autoSaveTime = -1;
                        _nextSave = Util.UtcMaxValue;
                    }
                    else
                    {
                        _nextSave = CurrentTimeUtc.AddMilliseconds( _autoSaveTime = value );
                    }
                }
            }
        }

        /// <summary>
        /// Overridden to FIRST create a snapshot and THEN call the next client.
        /// </summary>
        /// <param name="c"></param>
        public override void OnTransactionCommit( in SuccessfulTransactionContext c )
        {
            CreateSnapshot( c.Monitor, c.Domain );
            if( c.CommitTimeUtc >= _nextSave ) c.AddPostAction( SaveAsync );
            Next?.OnTransactionCommit( c );
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
                    CreateSnapshot( monitor, d );
                    if( !await SaveAsync( monitor ) )
                    {
                        throw new Exception( $"Unable to initialize the store for '{_storeName}'." );
                    }
                }
            }
        }

        /// <summary>
        /// Saves the current snapshot if the <see cref="MemoryTransactionProviderClient.CurrentSerialNumber"/> has changed since the last save.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <returns>True on success, false if an error occurred.</returns>
        public async Task<bool> SaveAsync( IActivityMonitor monitor )
        {
            if( _savedTransactionNumber != CurrentSerialNumber )
            {
                try
                {
                    await StreamStore.CreateAsync( _storeName, WriteSnapshotAsync );
                    if( _autoSaveTime >= 0 ) _nextSave = CurrentTimeUtc.AddMilliseconds( _autoSaveTime );
                    _savedTransactionNumber = CurrentSerialNumber;
                    monitor.Trace( $"Domain '{_storeName}' saved." );
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
