using CK.BinarySerialization;
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

namespace CK.Observable;


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
    // The current snapshot is the first _snapshotLength bytes of _memory.
    // A new snapshot is written in _spare: the two streams are swapped under the _snapshotLock
    // only when the new snapshot is complete. The current snapshot is never written.
    MemoryStream _memory;
    MemoryStream _spare;
    readonly object _snapshotLock;
    int _snapshotLength;
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
        _spare = new MemoryStream( 16 * 1024 );
        _snapshotLock = new object();
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
    /// Gets or sets whether debug mode is enabled for serialization.
    /// When true, sentinel bytes are written that can be verified during deserialization.
    /// Defaults to false.
    /// </summary>
    public bool DebugMode { get; set; }

    /// <summary>
    /// Gets the next client if any.
    /// This is useful if the default behavior of the virtual methods must be changed.
    /// </summary>
    protected IObservableDomainClient? Next { get; }

    /// <summary>
    /// Number of transactions to skip after every save.
    /// <para>
    /// Defaults to zero: transaction mode is on, unhandled errors trigger a rollback of the current state 
    /// with <see cref="CurrentTransactionStatus.Rollingback"/>.
    /// </para>
    /// <para>
    /// When positive, the transaction mode is on, but in a very dangerous mode: whenever saves are skipped,
    /// the domain rollbacks to an old version of itself. <see cref="DomainView.CurrentTransactionStatus"/>
    /// can be <see cref="CurrentTransactionStatus.DangerousRollingback"/>.
    /// </para>
    /// <para>
    /// When set to -1, transaction mode is off. The domain must be saved explicitly.
    /// </para>
    /// </summary>
    public int SkipTransactionCount
    {
        get => _skipTransactionCount;
        set
        {
            Throw.CheckOutOfRangeArgument( value >= -1 );
            _skipTransactionCount = value;
        }
    }

    /// <summary>
    /// Default behavior is FIRST to relay the call to the next client if any, and
    /// THEN to create a snapshot (simply calls <see cref="CreateSnapshot"/> protected method)
    /// if <see cref="TransactionDoneEventArgs.RollbackedInfo"/> is null.
    /// </summary>
    /// <param name="c">The transaction context.</param>
    public virtual void OnTransactionCommit( TransactionDoneEventArgs c )
    {
        Next?.OnTransactionCommit( c );
        if( c.RollbackedInfo == null )
        {
            CreateSnapshot( c.Monitor, c.Domain, false, c.HasSaveCommand );
        }
    }

    /// <summary>
    /// See <see cref="IObservableDomainClient.OnUnhandledException(IActivityMonitor, ObservableDomain, Exception, ref bool)"/>.
    /// This implementation does nothing here: it relays to the next client.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="ex">The exception that has been raised.</param>
    /// <param name="swallowError">Unchanged (unless the next client changed it).</param>
    public virtual void OnUnhandledException( IActivityMonitor monitor, ObservableDomain d, Exception ex, ref bool swallowError )
    {
        Next?.OnUnhandledException( monitor, d, ex, ref swallowError );
    }

    /// <summary>
    /// Default behavior is FIRST to relay the failure to the next client if any, and
    /// THEN to call <see cref="RollbackSnapshot"/> (and throws an <see cref="Exception"/>
    /// if no snapshot was available or if an error occurred).
    /// By default, <see cref="ObservableDomain.TimeManager"/> is started if it was previously started.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="d">The associated domain.</param>
    /// <param name="errors">A necessarily non null list of errors with at least one error.</param>
    public virtual void OnTransactionFailure( IActivityMonitor monitor, ObservableDomain d, IReadOnlyList<CKExceptionData> errors )
    {
        Next?.OnTransactionFailure( monitor, d, errors );
        if( !RollbackSnapshot( monitor, d, null ) )
        {
            Throw.Exception( "No snapshot available or error while restoring the last snapshot." );
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
        var target = _spare;
        target.Position = 0;
        s.CopyTo( target );
        int length = (int)target.Position;
        // We check 16 bytes. Even an empty domain require more bytes than that (secret, name, object count...).
        Throw.CheckState( length > 16 );
        var rawBytes = target.GetBuffer();
        var kind = _currentSnapshotKind;
        var headerLength = _currentSnapshotHeaderLength;
        if( rawBytes[0] == 0 )
        {
            kind = (CompressionKind)rawBytes[1];
            if( kind != CompressionKind.None && kind != CompressionKind.GZiped )
            {
                Throw.InvalidDataException( "Invalid CompressionKind marker." );
            }
            headerLength = 2;
        }
        else if( rawBytes[0] == 1 )
        {
            kind = ReadHeader( rawBytes.AsSpan().Slice( 1, SnapshotHeaderLength - 1 ) );
            headerLength = SnapshotHeaderLength;
        }
        int previousLength;
        CompressionKind? previousKind;
        int previousHeaderLength;
        lock( _snapshotLock )
        {
            previousLength = _snapshotLength;
            previousKind = _currentSnapshotKind;
            previousHeaderLength = _currentSnapshotHeaderLength;
            _spare = _memory;
            _memory = target;
            _snapshotLength = length;
            _currentSnapshotKind = kind;
            _currentSnapshotHeaderLength = headerLength;
        }
        try
        {
            DoLoadOrCreateFromSnapshot( monitor, ref d, false, startTimer );
        }
        catch
        {
            // The bytes cannot be loaded: the previous snapshot (still in the spare memory) is restored.
            lock( _snapshotLock )
            {
                _memory = _spare;
                _spare = target;
                _snapshotLength = previousLength;
                _currentSnapshotKind = previousKind;
                _currentSnapshotHeaderLength = previousHeaderLength;
            }
            throw;
        }
        lock( _snapshotLock )
        {
            _snapshotSerialNumber = d.TransactionSerialNumber;
            _snapshotTimeUtc = d.TransactionCommitTimeUtc;
        }
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
            s.Read( bytes );
            return (ReadHeader( bytes ), SnapshotHeaderLength); 
        }
        if( v == 0 )
        {
            var k = (CompressionKind)s.ReadByte();
            if( k == CompressionKind.None || k == CompressionKind.GZiped ) return (k, 2);
        }
        Throw.InvalidDataException( "Invalid Snapshot header." );
        // Unreachable.
        return default;
    }

    static CompressionKind ReadHeader( ReadOnlySpan<byte> bytes )
    {
        if( bytes.SequenceEqual( _headerNone.AsSpan() ) ) return CompressionKind.None;
        else if( bytes.SequenceEqual( _headerGZip.AsSpan() ) ) return CompressionKind.GZiped;
        Throw.InvalidDataException( "Invalid Snapshot header." );
        // Unreachable.
        return default;
    }

    /// <summary>
    /// Writes the current snapshot to the provided stream.
    /// The stream respects the <see cref="CurrentSnapshotKind"/>.
    /// </summary>
    /// <param name="s">The target stream.</param>
    /// <param name="skipSnapshotHeader">True to skip the version and compression kind header.</param>
    protected void WriteSnapshot( Stream s, bool skipSnapshotHeader = false )
    {
        var c = CopySnapshot();
        s.Write( skipSnapshotHeader ? c.Bytes.Span.Slice( c.HeaderLength ) : c.Bytes.Span );
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
        var c = CopySnapshot();
        return s.WriteAsync( skipSnapshotHeader ? c.Bytes.Slice( c.HeaderLength ) : c.Bytes ).AsTask();
    }

    /// <summary>
    /// An immutable copy of a snapshot.
    /// </summary>
    /// <param name="Bytes">The snapshot bytes, header included. Empty when there is no snapshot.</param>
    /// <param name="HeaderLength">The length of the version and compression kind header at the start of the <paramref name="Bytes"/>.</param>
    /// <param name="SerialNumber">The transaction serial number of the snapshot. -1 when there is no snapshot.</param>
    /// <param name="TimeUtc">The time of the snapshot (the transaction commit time).</param>
    protected readonly record struct SnapshotCopy( ReadOnlyMemory<byte> Bytes, int HeaderLength, int SerialNumber, DateTime TimeUtc );

    /// <summary>
    /// Copies the current snapshot with its serial number in one atomic step: a concurrent commit or
    /// rollback cannot change the snapshot during the copy. Use this method to persist a snapshot
    /// from outside the domain lock (for instance from a post action) and to know which transaction
    /// the persisted bytes contain.
    /// </summary>
    /// <returns>The snapshot copy.</returns>
    protected SnapshotCopy CopySnapshot()
    {
        lock( _snapshotLock )
        {
            if( _snapshotSerialNumber == -1 )
            {
                return new SnapshotCopy( ReadOnlyMemory<byte>.Empty, 0, -1, _snapshotTimeUtc );
            }
            return new SnapshotCopy( _memory.GetBuffer().AsSpan( 0, _snapshotLength ).ToArray(),
                                     _currentSnapshotHeaderLength,
                                     _snapshotSerialNumber,
                                     _snapshotTimeUtc );
        }
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
    protected bool RollbackSnapshot( IActivityMonitor monitor, ObservableDomain d, bool? startTimer )
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
                DoLoadOrCreateFromSnapshot( monitor, ref d, restoring: true, startTimer );
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
    /// Loads the domain from the current snapshot memory: either calls Load() on the existing <paramref name="domain"/>
    /// or calls <see cref="DeserializeDomain(IActivityMonitor, RewindableStream, bool?)"/> if it is null.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="domain">The domain to reload or deserialize if null.</param>
    /// <param name="restoring">
    /// True when called from <see cref="RollbackSnapshot"/>, false when called by <see cref="LoadOrCreateAndInitializeSnapshot"/>.
    /// </param>
    /// <param name="startTimer">
    /// Ensures that the <see cref="ObservableDomain.TimeManager"/> is running or stopped.
    /// When null, it keeps its previous state (it is initially stopped at domain creation) and then its current state is persisted.
    /// </param>
    protected virtual void DoLoadOrCreateFromSnapshot( IActivityMonitor monitor, [AllowNull]ref ObservableDomain domain, bool restoring, bool? startTimer )
    {
        void Ensure( IActivityMonitor monitor, [AllowNull]ref ObservableDomain domain, RewindableStream stream, bool? startTimer )
        {
            if( domain != null )
            {
                if( !domain.Load( monitor, stream, startTimer: startTimer ) )
                {
                    Throw.Exception( $"Error while loading serialized domain. Please see logs." );
                }
            }
            else
            {
                domain = DeserializeDomain( monitor, stream, startTimer );
            }
        }

        // The domain lock serializes the commits and the rollbacks: the current snapshot cannot be
        // swapped (and then overwritten) while it is read here.
        MemoryStream memory;
        int length;
        int headerLength;
        CompressionKind? kind;
        lock( _snapshotLock )
        {
            memory = _memory;
            length = _snapshotLength;
            headerLength = _currentSnapshotHeaderLength;
            kind = _currentSnapshotKind;
        }
        // A read-only view on the snapshot: the reader cannot read stale bytes after the snapshot
        // and cannot change the state of the current snapshot memory.
        var snapshot = new MemoryStream( memory.GetBuffer(), headerLength, length - headerLength, writable: false );
        if( kind == CompressionKind.GZiped )
        {
            using var g = new GZipStream( snapshot, CompressionMode.Decompress );
            Ensure( monitor, ref domain, RewindableStream.FromGZipStream( g ), startTimer );
        }
        else
        {
            // The kind is null when LoadOrCreateAndInitializeSnapshot found no header: the stream is not compressed.
            Debug.Assert( kind is null or CompressionKind.None );
            Ensure( monitor, ref domain, RewindableStream.FromStream( snapshot ), startTimer );
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
    protected abstract ObservableDomain DeserializeDomain( IActivityMonitor monitor, RewindableStream stream, bool? startTimer );

    /// <summary>
    /// Creates a snapshot, respecting the <see cref="CompressionKind"/>.
    /// </summary>
    /// <param name="monitor">The monitor to use.</param>
    /// <param name="d">The associated domain.</param>
    /// <param name="initialOne">
    /// True if this snapshot is the initial one, created by the first call
    /// to <see cref="OnTransactionStart(IActivityMonitor, ObservableDomain, DateTime)"/>.
    /// Subsequent calls are coming from <see cref="OnTransactionCommit(TransactionDoneEventArgs)"/>.
    /// <para>
    /// This "initial" snapshot is the first one for this Client, this has nothing to do with the <see cref="ObservableDomain.TransactionSerialNumber"/>
    /// that can already be greater than 0 if the domain has been loaded.
    /// </para>
    /// </param>
    /// <param name="ignoreSkipTransactionCount">True to create a snapshot regardless of <see cref="SkipTransactionCount"/>.</param>
    protected virtual void CreateSnapshot( IActivityMonitor monitor, IObservableDomain d, bool initialOne, bool ignoreSkipTransactionCount )
    {
        // If we ignoreSkipTransactionCount or skipTransactionCount is 0 (full transacted mode)
        // or if have no snapshot yet, we do the snapshot: we always have a snapshot to restore,
        // even if it is the very first one.
        if( !ignoreSkipTransactionCount && _skipTransactionCount != 0 && _snapshotSerialNumber >= 0 )
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
            // The new snapshot is written in the spare memory: if Save throws, the current snapshot
            // (and its serial number) stays the same.
            // The spare memory is written without the _snapshotLock: the calls to CreateSnapshot must not be concurrent.
            // The commits are serialized by the domain write lock. A call outside a transaction (an initial snapshot,
            // like the one of StreamStoreClient.InitializeAsync) must happen before the domain is shared.
            var kind = CompressionKind;
            var target = _spare;
            target.Position = 0;
            target.WriteByte( CurrentSerializationVersion );
            target.Write( kind == CompressionKind.None ? _headerNone : _headerGZip );
            Debug.Assert( target.Position == SnapshotHeaderLength );
            if( kind == CompressionKind.GZiped )
            {
                using( var gz = new GZipStream( target, CompressionLevel.Optimal, leaveOpen: true ) )
                {
                    d.Save( monitor, gz, debugMode: DebugMode );
                }
            }
            else
            {
                Debug.Assert( kind == CompressionKind.None );
                d.Save( monitor, target, debugMode: DebugMode );
            }
            lock( _snapshotLock )
            {
                _spare = _memory;
                _memory = target;
                _snapshotLength = (int)target.Position;
                _currentSnapshotHeaderLength = SnapshotHeaderLength;
                _currentSnapshotKind = kind;
                _snapshotSerialNumber = d.TransactionSerialNumber;
                _snapshotTimeUtc = d.TransactionCommitTimeUtc;
            }
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
                                            ? $"Snapshot {_snapshotLength} bytes, n°{_snapshotSerialNumber} - {_snapshotTimeUtc}."
                                            : "No snapshot.";

}
