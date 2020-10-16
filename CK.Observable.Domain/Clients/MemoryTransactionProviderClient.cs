using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace CK.Observable
{

    /// <summary>
    /// This is a simple, yet useful, participant that implements transaction in memory.
    /// It is open to extensions and can be used as a base class: the <see cref="FileTransactionProviderClient"/> extends this.
    /// The protected <see cref="LoadAndInitializeSnapshot"/> and <see cref="WriteSnapshot"/> methods offers access to
    /// the internal memory.
    /// </summary>
    public class MemoryTransactionProviderClient : IObservableDomainClient
    {
        readonly MemoryStream _memory;
        int _snapshotSerialNumber;
        DateTime _snapshotTimeUtc;
        CompressionKind? _currentSnapshotKind;

        /// <summary>
        /// Current serialization version of the snapshot: this is the first byte of the
        /// stream, before any compression.
        /// </summary>
        public const byte CurrentSerializationVersion = 0;

        const int SnapshotHeaderLength = 2;

        /// <summary>
        /// Initializes a new <see cref="MemoryTransactionProviderClient"/>.
        /// </summary>
        /// <param name="next">The next manager (can be null).</param>
        public MemoryTransactionProviderClient( IObservableDomainClient? next = null )
        {
            Next = next;
            _memory = new MemoryStream( 16 * 1024 );
            _snapshotSerialNumber = -1;
            _snapshotTimeUtc = Util.UtcMinValue;
        }

        /// <summary>
        /// Gets the current, snapshot, serial number.
        /// Defaults to -1 when there is no snapshot.
        /// </summary>
        public int CurrentSerialNumber => _snapshotSerialNumber;

        /// <summary>
        /// Gets the time of the current snapshot (the transaction commit time).
        /// Defaults to <see cref="Util.UtcMinValue"/> when there is no snapshot.
        /// </summary>
        public DateTime CurrentTimeUtc => _snapshotTimeUtc;

        /// <summary>
        /// Gets whether a snapshot is available and if it is, its <see cref="CompressionKind"/>.
        /// </summary>
        public CompressionKind? CurrentSnapshotKind => _currentSnapshotKind;

        /// <summary>
        /// Gets or sets the compression kind to use. It will be used for the next snapshot. 
        /// Defaults to <see cref="CompressionKind.None"/>.
        /// </summary>
        public CompressionKind CompressionKind { get; set; }

        /// <summary>
        /// Gets the next client if any.
        /// This is usesful if the default behavior of the virtual methods must be changed.
        /// </summary>
        protected IObservableDomainClient? Next { get; }

        /// <summary>
        /// Called when the domain instance is created.
        /// By default, does nothing.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="d">The newly created domain.</param>
        public virtual void OnDomainCreated( IActivityMonitor monitor, ObservableDomain d )
        {
            Next?.OnDomainCreated( monitor, d );
        }

        /// <summary>
        /// Default behavior is FIRST to relay the call to the next client if any, and
        /// THEN to create a snapshot (simply calls <see cref="CreateSnapshot"/> protected method).
        /// </summary>
        /// <param name="c">The transaction context.</param>
        public virtual void OnTransactionCommit( in SuccessfulTransactionEventArgs c )
        {
            Next?.OnTransactionCommit( c );
            CreateSnapshot( c.Monitor, c.Domain );
        }

        /// <summary>
        /// Default behavior is FIRST to relay the failure to the next client if any, and
        /// THEN to call <see cref="RestoreSnapshot"/> (and throws an <see cref="Exception"/>
        /// if no snapshot was available or if an error occured).
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="d">The associated domain.</param>
        /// <param name="errors">A necessarily non null list of errors with at least one error.</param>
        public virtual void OnTransactionFailure( IActivityMonitor monitor, ObservableDomain d, IReadOnlyList<CKExceptionData> errors )
        {
            Next?.OnTransactionFailure( monitor, d, errors );
            if( !RestoreSnapshot( monitor, d ) )
            {
                throw new Exception( "No snapshot available or error while restoring the last snapshot." );
            }
        }

        /// <summary>
        /// Simply calls the next <see cref="IObservableDomainClient"/> in the chain of responsibility
        /// and if there is no snapshot an initial snapshot is created.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="d">The associated domain.</param>
        /// <param name="timeUtc">The Utc time associated to the transaction.</param>
        public virtual void OnTransactionStart( IActivityMonitor monitor, ObservableDomain d, DateTime timeUtc )
        {
            Next?.OnTransactionStart( monitor, d, timeUtc );
            if( _snapshotSerialNumber == -1 ) CreateSnapshot( monitor, d );
        }

        /// <summary>
        /// Initializes the current snapshot with the provided stream content and calls <see cref="DoLoadFromSnapshot(IActivityMonitor, ObservableDomain)"/>
        /// to load the domain.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="d">The domain.</param>
        /// <param name="s">The readable stream (will be copied into the memory).</param>
        protected void LoadAndInitializeSnapshot( IActivityMonitor monitor, ObservableDomain d, Stream s )
        {
            _memory.Position = 0;
            s.CopyTo( _memory );
            if( _memory.Position < 3 ) throw new InvalidDataException( "Invalid Snapshot restoration." );
            var rawBytes = _memory.GetBuffer();
            if( rawBytes[0] != 0 ) throw new InvalidDataException( "Invalid Snapshot version. Only 0 is currently supported." );
            _currentSnapshotKind = (CompressionKind)rawBytes[1];
            if( _currentSnapshotKind != CompressionKind.None && _currentSnapshotKind != CompressionKind.GZiped ) throw new InvalidDataException( "Invalid CompressionKind marker." );
            DoLoadFromSnapshot( monitor, d, false );
            _snapshotSerialNumber = d.TransactionSerialNumber;
            _snapshotTimeUtc = d.TransactionCommitTimeUtc;
        }

        /// <summary>
        /// Writes the current snapshot to the provided stream.
        /// The stream respects the <see cref="CurrentSnapshotKind"/>.
        /// </summary>
        /// <param name="s">The target stream.</param>
        /// <param name="skipSnapshotHeader">True to skip the version and compression kind header.</param>
        protected void WriteSnapshot( Stream s, bool skipSnapshotHeader = false )
        {
            var len = (int)_memory.Position;
            s.Write( _memory.GetBuffer(), skipSnapshotHeader ? SnapshotHeaderLength : 0, skipSnapshotHeader ? len - SnapshotHeaderLength : len );
        }

        /// <summary>
        /// Writes the current snapshot to the provided stream.
        /// The stream respects the <see cref="CurrentSnapshotKind"/> and the snapshot header is written.
        /// </summary>
        /// <param name="s">The target stream.</param>
        protected Task WriteSnapshotAsync( Stream s ) => WriteSnapshotAsync( s, false );

        /// <summary>
        /// Writes the current snapshot to the provided stream.
        /// The stream respects the <see cref="CurrentSnapshotKind"/>.
        /// </summary>
        /// <param name="s">The target stream.</param>
        /// <param name="skipSnapshotHeader">True to skip the version and compression kind header.</param>
        protected Task WriteSnapshotAsync( Stream s, bool skipSnapshotHeader )
        {
            var len = (int)_memory.Position;
            return s.WriteAsync( _memory.GetBuffer(), skipSnapshotHeader ? SnapshotHeaderLength : 0, skipSnapshotHeader ? len - SnapshotHeaderLength : len );
        }

        /// <summary>
        /// Restores the last snapshot. Returns false if there is no snapshot or if an error occurred.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="d">The associated domain.</param>
        /// <returns>False if no snapshot is available or if the restoration failed. True otherwise.</returns>
        protected bool RestoreSnapshot( IActivityMonitor monitor, ObservableDomain d )
        {
            if( _snapshotSerialNumber == -1 )
            {
                Debug.Assert( ToString() == "No snapshot." );
                monitor.Warn( "No snapshot." );
                return false;
            }
            using( monitor.OpenInfo( $"Restoring snapshot: {ToString()}" ) )
            {
                try
                {
                    DoLoadFromSnapshot( monitor, d, true );
                    monitor.CloseGroup( "Success." );
                    return true;
                }
                catch( Exception ex )
                {
                    monitor.Error( ex );
                    return false;
                }
            }
        }

        /// <summary>
        /// Loads the domain from the current snapshot memory.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="d">The domain to load.</param>
        /// <param name="restoring">
        /// True when called from <see cref="RestoreSnapshot"/>, false when called by <see cref="LoadAndInitializeSnapshot"/>.
        /// </param>
        protected virtual void DoLoadFromSnapshot( IActivityMonitor monitor, ObservableDomain d, bool restoring )
        {
            var loadAction = GetLoadHook( monitor, restoring );
            // Hook that wraps the GetLoadHook() value (if not null) and returns true (to activate the timed events)
            // when LoadAndInitializeSnapshot is calling, or false when RestoreSnapshot is at stake.
            Func<ObservableDomain, bool> loadHook = domain => { loadAction?.Invoke( domain ); return !restoring; };

            long p = _memory.Position;
            _memory.Position = SnapshotHeaderLength;
            if( _currentSnapshotKind == CompressionKind.GZiped )
            {
                using( var gz = new GZipStream( _memory, CompressionMode.Decompress, leaveOpen: true ) )
                {
                    d.Load( monitor, gz, leaveOpen: true, loadHook: loadHook );
                }
            }
            else
            {
                Debug.Assert( CompressionKind == CompressionKind.None );
                d.Load( monitor, _memory, leaveOpen: true, loadHook: loadHook );
            }
            if( _memory.Position != p ) throw new Exception( $"Internal error: stream position should be {p} but was {_memory.Position} after reload." );
        }

        /// <summary>
        /// Extension point called when loading the domain.
        /// See <see cref="ObservableDomain.Load(IActivityMonitor, Stream, bool, System.Text.Encoding?, int, Func{ObservableDomain, bool}?)"/> loadHook
        /// parameter.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="restoring">
        /// True when called from <see cref="RestoreSnapshot"/>, false when called by <see cref="LoadAndInitializeSnapshot"/>.
        /// </param>
        /// <returns>Defaults to null.</returns>
        protected virtual Action<ObservableDomain>? GetLoadHook( IActivityMonitor monitor, bool restoring ) => null;

        /// <summary>
        /// Creates a snapshot, respecting the <see cref="CompressionKind"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="d">The associated domain.</param>
        protected void CreateSnapshot( IActivityMonitor monitor, IObservableDomain d )
        {
            using( monitor.OpenTrace( $"Creating snapshot." ) )
            {
                _memory.Position = 0;
                _memory.WriteByte( CurrentSerializationVersion );
                _memory.WriteByte( (byte)CompressionKind );
                if( CompressionKind == CompressionKind.GZiped )
                {
                    using( var gz = new GZipStream( _memory, CompressionLevel.Optimal, leaveOpen: true ) )
                    {
                        d.Save( monitor, gz, leaveOpen: true );
                    }
                }
                else
                {
                    Debug.Assert( CompressionKind == CompressionKind.None );
                    d.Save( monitor, _memory, leaveOpen: true );
                }
                _currentSnapshotKind = CompressionKind;
                _snapshotSerialNumber = d.TransactionSerialNumber;
                _snapshotTimeUtc = d.TransactionCommitTimeUtc;
                monitor.CloseGroup( ToString() );
            }
        }

        /// <summary>
        /// Does nothing at this level.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="d">The disposed domain.</param>
        public virtual void OnDomainDisposed( IActivityMonitor monitor, ObservableDomain d )
        {
        }

        /// <summary>
        /// Returns the number of bytes, the transaction number and the time or "No snapshot.".
        /// </summary>
        /// <returns>A readable string.</returns>
        public override string ToString() => _snapshotSerialNumber != -1
                                                ? $"Snapshot {_memory.Position} bytes, n°{_snapshotSerialNumber} - {_snapshotTimeUtc}."
                                                : "No snapshot.";

    }
}
