using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{

    /// <summary>
    /// This is a simple, yet useful, participant that implements transaction in memory.
    /// It is open to extensions and can be used as a base class: the <see cref="FileTransactionProviderClient"/> extends this.
    /// The protected <see cref="LoadOrCreateAndInitializeSnapshot"/> and <see cref="WriteSnapshot"/> methods offers access to
    /// the internal memory stream.
    /// <para>
    /// This class is abstract to be able to deserialize typed domains (thanks to <see cref="DeserializeDomain"/> abstract method).
    /// This is used only when <see cref="LoadOrCreateAndInitializeSnapshot"/> is called that happens outside
    /// of the <see cref="IObservableDomainClient"/> responsibilities and has been designed mostly to 
    /// support domain management by Observable leagues (or other domain managers).
    /// </para>
    /// </summary>
    public abstract class MemoryTransactionProviderClient : IObservableDomainClient
    {
        readonly MemoryStream _memory;
        int _skipTransactionCount;
        int _snapshotSerialNumber;
        DateTime _snapshotTimeUtc;
        CompressionKind? _currentSnapshotKind;
        int _currentSnapshotHeaderLength;

        static readonly byte[] _headerNone = Encoding.ASCII.GetBytes( "OD-None" );
        static readonly byte[] _headerGZip = Encoding.ASCII.GetBytes( "OD-GZip" );

        /// <summary>
        /// Current serialization version of the snapshot: this is the first byte of the
        /// stream, before any compression.
        /// </summary>
        public const byte CurrentSerializationVersion = 1;

        const int SnapshotHeaderLength = 8;

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
        /// Gets or sets the behavior regarding disposed objects during <see cref="IObservableDomain.Save"/>.
        /// Defaults to <see cref="SaveDestroyedObjectBehavior.None"/>.
        /// </summary>
        public SaveDestroyedObjectBehavior SaveDisposedObjectBehavior { get; set; }

        /// <summary>
        /// Gets the next client if any.
        /// This is useful if the default behavior of the virtual methods must be changed.
        /// </summary>
        protected IObservableDomainClient? Next { get; }

        /// <summary>
        /// Number of transactions to skip after every save.
        /// <para>
        /// Defaults to zero: transaction mode is on, unhandled errors trigger a rollback of the current state.
        /// </para>
        /// <para>
        /// When positive, the transaction mode is on, but in a very dangerous mode: whenever saves are skipped,
        /// the domain rollbacks to an old version of itself.
        /// </para>
        /// <para>
        /// When set to -1, transaction mode is off. Unhandled errors are logged (as <see cref="LogLevel.Error"/>) and
        /// silently swallowed by <see cref="OnUnhandledError(IActivityMonitor, ObservableDomain, Exception, ref bool)"/>.
        /// </para>
        /// </summary>
        public int SkipTransactionCount
        {
            get => _skipTransactionCount;
            set
            {
                if( _skipTransactionCount < -1 ) throw new ArgumentOutOfRangeException( nameof(SkipTransactionCount) );
                _skipTransactionCount = value;
            }
        }

        /// <summary>
        /// Called when the domain instance is created.
        /// By default, does nothing.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="d">The newly created domain.</param>
        /// <param name="startTimer">
        /// Whether the <see cref="ObservableDomain.TimeManager"/> must be running or stopped.
        /// A client can alter the value (typically setting it to false if needed).
        /// </param>
        public virtual void OnDomainCreated( IActivityMonitor monitor, ObservableDomain d, ref bool startTimer )
        {
            Next?.OnDomainCreated( monitor, d, ref startTimer );
        }

        /// <summary>
        /// Default behavior is FIRST to relay the call to the next client if any, and
        /// THEN to create a snapshot (simply calls <see cref="CreateSnapshot"/> protected method).
        /// </summary>
        /// <param name="c">The transaction context.</param>
        public virtual void OnTransactionCommit( in SuccessfulTransactionEventArgs c )
        {
            Next?.OnTransactionCommit( c );
            CreateSnapshot( c.Monitor, c.Domain, false, c.HasSaveCommand );
        }


        /// <summary>
        /// See <see cref="IObservableDomainClient.OnUnhandledError(IActivityMonitor, ObservableDomain, Exception, ref bool)"/>.
        /// Empty implementation nothing here: <paramref name="swallowError"/> is not changed.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="ex">The exception that has been raised.</param>
        /// <param name="swallowError">Unchanged.</param>
        public virtual void OnUnhandledError( IActivityMonitor monitor, ObservableDomain d, Exception ex, ref bool swallowError )
        {
            if( _skipTransactionCount < 0 )
            {
                monitor.Error( $"Error while modifying domain '{d.DomainName}' (SkipTransactionCount = -1).", ex );
                swallowError = true;
            }
        }

        /// <summary>
        /// Default behavior is FIRST to relay the failure to the next client if any, and
        /// THEN to call <see cref="RestoreSnapshot"/> (and throws an <see cref="Exception"/>
        /// if no snapshot was available or if an error occurred).
        /// By default, <see cref="ObservableDomain.TimeManager"/> is started if it was previously started.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="d">The associated domain.</param>
        /// <param name="errors">A necessarily non null list of errors with at least one error.</param>
        public virtual void OnTransactionFailure( IActivityMonitor monitor, ObservableDomain d, IReadOnlyList<CKExceptionData> errors )
        {
            Next?.OnTransactionFailure( monitor, d, errors );
            if( !RestoreSnapshot( monitor, d, null ) )
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
            if( _snapshotSerialNumber == -1 ) CreateSnapshot( monitor, d, true, true );
        }

        /// <summary>
        /// Initializes the current snapshot with the provided stream content and
        /// calls <see cref="DoLoadOrCreateFromSnapshot"/> to reload the existing domain or
        /// instantiates a new instance.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="d">The domain to reload or the new instantiated domain.</param>
        /// <param name="s">The readable stream (will be copied into the memory).</param>
        /// <param name="startTimer">
        /// Ensures that the <see cref="ObservableDomain.TimeManager"/> is running or stopped.
        /// When null, it keeps its previous state (it is initially stopped at domain creation) and then its current state is persisted.
        /// </param>
        protected void LoadOrCreateAndInitializeSnapshot( IActivityMonitor monitor, [AllowNull]ref ObservableDomain d, Stream s, bool? startTimer )
        {
            _memory.Position = 0;
            s.CopyTo( _memory );
            // We check 16 bytes. Even an empty domain require more bytes than that (secret, name, object count...).
            if( _memory.Position < 16 ) throw new InvalidDataException( $"Invalid Snapshot restoration (stream length is {_memory.Position})." );
            var rawBytes = _memory.GetBuffer();
            if( rawBytes[0] == 0 )
            {
                _currentSnapshotKind = (CompressionKind)rawBytes[1];
                if( _currentSnapshotKind != CompressionKind.None && _currentSnapshotKind != CompressionKind.GZiped )
                {
                    throw new InvalidDataException( "Invalid CompressionKind marker." );
                }
                _currentSnapshotHeaderLength = 2; 
            }
            else if( rawBytes[0] == 1 )
            {
                _currentSnapshotKind = ReadHeader( rawBytes.AsSpan().Slice( 1, SnapshotHeaderLength - 1 ) );
                _currentSnapshotHeaderLength = SnapshotHeaderLength;
            }
            DoLoadOrCreateFromSnapshot( monitor, ref d, false, startTimer );
            _snapshotSerialNumber = d.TransactionSerialNumber;
            _snapshotTimeUtc = d.TransactionCommitTimeUtc;
        }

        /// <summary>
        /// Reads the start of the stream and tries to find the snapshot version and header.
        /// On success, the kind of compression to use the stream is returned, otherwise
        /// an <see cref="InvalidDataException"/> is thrown.
        /// <para>
        /// The <see cref="Stream.Position"/> is always forwarded by this method. On success,
        /// the returned tuple specifies the number of bytes that have been read.
        /// </para>
        /// </summary>
        /// <param name="s">The stream to read.</param>
        /// <returns>The compression kind to use and the number of bytes consumed.</returns>
        public static (CompressionKind Kind, int HeaderLength) ReadSnapshotHeader( Stream s )
        {
            var v = s.ReadByte();
            if( v == 1 )
            {
                Span<byte> bytes = stackalloc byte[SnapshotHeaderLength - 1];
                return (ReadHeader( bytes ), SnapshotHeaderLength); 
            }
            if( v == 0 )
            {
                var k = (CompressionKind)s.ReadByte();
                if( k == CompressionKind.None || k == CompressionKind.GZiped ) return (k, 2);
            }
            throw new InvalidDataException( "Invalid Snapshot header." );
        }

        static CompressionKind ReadHeader( ReadOnlySpan<byte> bytes )
        {
            if( bytes.SequenceEqual( _headerNone.AsSpan() ) ) return CompressionKind.None;
            else if( bytes.SequenceEqual( _headerGZip.AsSpan() ) ) return CompressionKind.GZiped;
            throw new InvalidDataException( "Invalid Snapshot header." );
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
        /// The stream respects the <see cref="CurrentSnapshotKind"/> since it is a direct copy of the memory stream.
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
        /// <param name="startTimer">
        /// Ensures that the <see cref="ObservableDomain.TimeManager"/> is running or stopped.
        /// When null, it keeps its previous state (it is initially stopped at domain creation) and then its current state is persisted.
        /// </param>
        /// <returns>False if no snapshot is available or if the restoration failed. True otherwise.</returns>
        protected bool RestoreSnapshot( IActivityMonitor monitor, ObservableDomain d, bool? startTimer )
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
                    DoLoadOrCreateFromSnapshot( monitor, ref d, true, startTimer );
                    monitor.CloseGroup( "Success." );
                    return true;
                }
                catch( Exception )
                {
                    monitor.CloseGroup( "Failed." );
                    return false;
                }
            }
        }

        /// <summary>
        /// Loads the domain from the current snapshot memory: either calls Load on it or invokes the deserialization
        /// constructor when <paramref name="domain"/> is null.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="domain">The domain to reload or deserialize.</param>
        /// <param name="restoring">
        /// True when called from <see cref="RestoreSnapshot"/>, false when called by <see cref="LoadOrCreateAndInitializeSnapshot"/>.
        /// </param>
        /// <param name="startTimer">
        /// Ensures that the <see cref="ObservableDomain.TimeManager"/> is running or stopped.
        /// When null, it keeps its previous state (it is initially stopped at domain creation) and then its current state is persisted.
        /// </param>
        protected virtual void DoLoadOrCreateFromSnapshot( IActivityMonitor monitor, ref ObservableDomain? domain, bool restoring, bool? startTimer )
        {
            static void ReloadOrThrow( IActivityMonitor monitor,
                                       ObservableDomain domain,
                                       Stream stream,
                                       bool? startTimer )
            {
                if( !domain.Load( monitor, stream, leaveOpen: true, startTimer: startTimer ) )
                {
                    throw new CKException( $"Error while loading serialized domain. Please see logs." );
                }
            }

            void Ensure( IActivityMonitor monitor, ref ObservableDomain? domain, Stream stream, bool? startTimer )
            {
                if( domain != null )
                {
                    ReloadOrThrow( monitor, domain, stream, startTimer );
                }
                else
                {
                    domain = DeserializeDomain( monitor, stream, startTimer );
                }
            }
            _memory.Position = _currentSnapshotHeaderLength;
            if( _currentSnapshotKind == CompressionKind.GZiped )
            {
                using( var gz = new GZipStream( _memory, CompressionMode.Decompress, leaveOpen: true ) )
                {
                    Ensure( monitor, ref domain, gz, startTimer );
                }
            }
            else
            {
                Debug.Assert( CompressionKind == CompressionKind.None );
                Ensure( monitor, ref domain, _memory, startTimer );
            }
        }


        /// <summary>
        /// Extension point that can only be called from <see cref="LoadOrCreateAndInitializeSnapshot"/> with a null domain:
        /// instead of reloading the non-existing domain, this method must call the deserialization constructor.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="stream">The stream from which the domain must be deserialized.</param>
        /// <param name="startTimer">
        /// Ensures that the <see cref="ObservableDomain.TimeManager"/> is running or stopped.
        /// When null, it keeps its previous state (it is initially stopped at domain creation) and then its current state is persisted.
        /// </param>
        /// <returns>The new domain.</returns>
        protected abstract ObservableDomain DeserializeDomain( IActivityMonitor monitor, Stream stream, bool? startTimer );

        /// <summary>
        /// Creates a snapshot, respecting the <see cref="CompressionKind"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="d">The associated domain.</param>
        /// <param name="initialOne">
        /// True if this snapshot is the initial one, created by the first call
        /// to <see cref="OnTransactionStart(IActivityMonitor, ObservableDomain, DateTime)"/>.
        /// Subsequent calls are coming from <see cref="OnTransactionCommit(in SuccessfulTransactionEventArgs)"/>.
        /// <para>
        /// This "initial" snapshot is the first one for this Client, this has nothing to do with the <see cref="ObservableDomain.TransactionSerialNumber"/>
        /// that can be greater than 0 if the domain has been loaded.
        /// </para>
        /// </param>
        /// <param name="ignoreSkipTransactionCount">True to create a snapshot regardless of <see cref="SkipTransactionCount"/>.</param>
        protected virtual void CreateSnapshot( IActivityMonitor monitor, IObservableDomain d, bool initialOne, bool ignoreSkipTransactionCount )
        {
            if( !ignoreSkipTransactionCount && _skipTransactionCount != 0 && _snapshotSerialNumber > 0 )
            {
                if( _skipTransactionCount > 0 )
                {
                    int delta = d.TransactionSerialNumber - _snapshotSerialNumber;
                    if( delta <= SkipTransactionCount )
                    {
                        monitor.Trace( $"Skipped snapshot of '{d.DomainName}' ({delta}/{SkipTransactionCount})." );
                        return;
                    }
                }
                else
                {
                    // SkipTransactionCount is -1: no auto save of transactions.
                    return;
                }
            }
            using( monitor.OpenTrace( $"Creating snapshot of '{d.DomainName}'." ) )
            {
                _memory.Position = 0;
                _memory.WriteByte( CurrentSerializationVersion );
                _memory.Write( CompressionKind == CompressionKind.None ? _headerNone : _headerGZip );
                Debug.Assert( _memory.Position == SnapshotHeaderLength );
                if( CompressionKind == CompressionKind.GZiped )
                {
                    using( var gz = new GZipStream( _memory, CompressionLevel.Optimal, leaveOpen: true ) )
                    {
                        d.Save( monitor, gz, leaveOpen: true, saveDestroyed: SaveDisposedObjectBehavior );
                    }
                }
                else
                {
                    Debug.Assert( CompressionKind == CompressionKind.None );
                    d.Save( monitor, _memory, leaveOpen: true, saveDestroyed: SaveDisposedObjectBehavior );
                }
                _currentSnapshotHeaderLength = SnapshotHeaderLength;
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
