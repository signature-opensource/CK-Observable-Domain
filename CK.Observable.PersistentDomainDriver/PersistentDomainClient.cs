using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using CK.BinarySerialization;
using CK.Core;

namespace CK.Observable;

/// <summary>
/// The domain client of a persistent domain. It extends <see cref="MemoryTransactionProviderClient"/>
/// (a snapshot in memory after each commit, a rollback on failure) and gives the snapshots to a <see cref="SnapshotSaver"/>.
/// <para>
/// When a commit is due for a save, the client copies the snapshot bytes under the domain lock.
/// The save then writes this copy in a post action. The next commit cannot change the copy, so a save never writes a torn file.
/// </para>
/// <para>
/// If a snapshot creation throws, CK.Observable.Domain 20.1 keeps the previous memory snapshot, but this commit is not in it.
/// If the restore after a failed transaction throws, the domain is half-loaded. In both cases the client cannot save
/// the committed state: the client is poisoned
/// (<see cref="IsPoisoned"/>). It logs a fatal error once, and the persistence writes no file until the next start.
/// The next start loads the last good generation.
/// </para>
/// </summary>
internal sealed class PersistentDomainClient : MemoryTransactionProviderClient
{
    readonly string _domainName;
    readonly IServiceProvider? _serviceProvider;
    // Serializes the captures: two captures can hold the domain read lock at the same time.
    readonly object _captureLock;
    int _lastCommittedTransactionNumber;
    // 1 when the client is poisoned.
    int _poisoned;

    /// <summary>
    /// Initializes a new client. It applies <see cref="PersistentDomainOptions.CompressionKind"/>,
    /// <see cref="PersistentDomainOptions.SkipTransactionCount"/> and <see cref="PersistentDomainOptions.DebugMode"/>.
    /// </summary>
    /// <param name="domainName">The domain name.</param>
    /// <param name="options">The options of the domain.</param>
    /// <param name="serviceProvider">The services that the domain gives to its sidekicks.</param>
    public PersistentDomainClient( string domainName, PersistentDomainOptions options, IServiceProvider? serviceProvider )
    {
        Throw.CheckNotNullOrEmptyArgument( domainName );
        Throw.CheckNotNullArgument( options );
        _domainName = domainName;
        _serviceProvider = serviceProvider;
        _captureLock = new object();
        _lastCommittedTransactionNumber = -1;
        CompressionKind = options.CompressionKind;
        SkipTransactionCount = options.SkipTransactionCount;
        DebugMode = options.DebugMode;
    }

    /// <summary>
    /// Gets or sets the saver. The driver sets it after a successful load.
    /// When it is null, a commit only creates the snapshot in memory.
    /// </summary>
    public SnapshotSaver? Saver { get; set; }

    /// <summary>
    /// Gets the transaction number of the domain after the last commit. -1 before the first commit.
    /// The saver compares it with the saved number to know if the domain is ahead of the saved state.
    /// </summary>
    public int LastCommittedTransactionNumber => Volatile.Read( ref _lastCommittedTransactionNumber );

    /// <summary>
    /// Gets whether a snapshot creation or a restore failed. The state in memory then cannot be trusted,
    /// and the persistence refuses every save until the next start. It never becomes false again.
    /// </summary>
    public bool IsPoisoned => Volatile.Read( ref _poisoned ) != 0;

    /// <summary>
    /// Creates a new empty domain that uses this client. The time manager of the domain is stopped.
    /// </summary>
    /// <param name="monitor">The monitor.</param>
    /// <returns>The new domain.</returns>
    public ObservableDomain CreateNew( IActivityMonitor monitor )
    {
        return new ObservableDomain( monitor, _domainName, startTimer: false, client: this, serviceProvider: _serviceProvider );
    }

    /// <summary>
    /// Loads a domain from a snapshot payload. The time manager of the domain is stopped, whatever the snapshot holds:
    /// no timed event runs before the initial transaction of the driver, and this transaction starts the timer
    /// when <see cref="PersistentDomainDriver{TSelf}.AutoStartTimer"/> is true.
    /// </summary>
    /// <param name="monitor">The monitor.</param>
    /// <param name="payload">The snapshot bytes, as a save wrote them.</param>
    /// <returns>The loaded domain.</returns>
    /// <exception cref="Exception">The payload does not deserialize.</exception>
    public ObservableDomain Load( IActivityMonitor monitor, byte[] payload )
    {
        Throw.CheckNotNullArgument( payload );
        ObservableDomain? d = null;
        LoadOrCreateAndInitializeSnapshot( monitor, ref d, new MemoryStream( payload, writable: false ), startTimer: false );
        return d!;
    }

    /// <summary>
    /// Copies the snapshot of the last committed transaction, for a save that no commit carries.
    /// The method runs under the domain read lock: no transaction runs and no rollback runs during the copy.
    /// If the memory snapshot is older than the domain (<see cref="MemoryTransactionProviderClient.SkipTransactionCount"/>
    /// skipped it), the method creates it first.
    /// <para>
    /// Do not call this method inside a transaction of the domain: the read lock does not allow recursion.
    /// </para>
    /// </summary>
    /// <param name="monitor">The monitor.</param>
    /// <param name="domain">The domain of this client.</param>
    /// <returns>The copy of the committed state.</returns>
    /// <exception cref="InvalidOperationException">The client is poisoned.</exception>
    public PendingSnapshot CaptureCommittedSnapshot( IActivityMonitor monitor, ObservableDomain domain )
    {
        Throw.CheckNotNullArgument( monitor );
        Throw.CheckNotNullArgument( domain );
        lock( _captureLock )
        {
            return domain.Read( monitor, () =>
            {
                // A rollback runs under the write lock: it cannot poison the client during this copy.
                if( IsPoisoned ) Throw.InvalidOperationException( $"Domain '{_domainName}': the state in memory cannot be trusted. The driver saves nothing until the next start." );
                if( CurrentSerialNumber != domain.TransactionSerialNumber )
                {
                    CreateSnapshot( monitor, domain, false, ignoreSkipTransactionCount: true );
                }
                using var copy = new MemoryStream();
                WriteSnapshot( copy );
                return new PendingSnapshot( CurrentSerialNumber, CurrentTimeUtc, copy.ToArray() );
            } );
        }
    }

    /// <summary>
    /// Records the transaction number and creates the snapshot in memory. If the commit is due for a save, copies the
    /// snapshot bytes, offers them to the <see cref="Saver"/>, adds a post action that saves them and asks the saver for
    /// a check (the domain can skip this post action). If the commit is not due, asks the saver for a trailing save.
    /// A poisoned client only creates the snapshot in memory.
    /// This method runs under the domain lock.
    /// </summary>
    /// <param name="c">The transaction context.</param>
    public override void OnTransactionCommit( TransactionDoneEventArgs c )
    {
        // Before the saver calls: a check of the saver that reads it then sees this commit.
        Volatile.Write( ref _lastCommittedTransactionNumber, c.Domain.TransactionSerialNumber );
        if( c.RollbackedInfo == null )
        {
            var saver = Saver;
            if( saver == null || IsPoisoned )
            {
                CreateSnapshot( c.Monitor, c.Domain, false, c.HasSaveCommand );
            }
            else
            {
                bool forced = c.HasSaveCommand;
                // A forced save needs no claim: a claim is only for the first commit of a delay window.
                bool due = forced || saver.TryClaimSave( c.CommitTimeUtc );
                CreateSnapshot( c.Monitor, c.Domain, false, ignoreSkipTransactionCount: due );
                if( due )
                {
                    // The copy belongs to the save. The next commit rewrites the memory snapshot, not this copy.
                    using var copy = new MemoryStream();
                    WriteSnapshot( copy );
                    saver.Offer( new PendingSnapshot( CurrentSerialNumber, c.CommitTimeUtc, copy.ToArray() ) );
                    c.PostActions.Add( ctx => saver.SaveAsync( ctx.Monitor ) );
                    saver.ScheduleSaveCheck();
                }
                else
                {
                    saver.ScheduleTrailingSave();
                }
            }
        }
        Next?.OnTransactionCommit( c );
    }

    /// <summary>
    /// Restores the memory snapshot (see <see cref="MemoryTransactionProviderClient.OnTransactionFailure"/>).
    /// If the restore throws, the domain is half-loaded: the client is poisoned, then the exception goes on.
    /// </summary>
    /// <param name="monitor">The monitor.</param>
    /// <param name="d">The domain.</param>
    /// <param name="errors">The errors of the transaction.</param>
    public override void OnTransactionFailure( IActivityMonitor monitor, ObservableDomain d, IReadOnlyList<CKExceptionData> errors )
    {
        try
        {
            base.OnTransactionFailure( monitor, d, errors );
        }
        catch( Exception ex )
        {
            Poison( monitor, "the restore of the memory snapshot after a failed transaction failed", ex );
            throw;
        }
    }

    /// <summary>
    /// Creates the memory snapshot (see <see cref="MemoryTransactionProviderClient.CreateSnapshot"/>).
    /// If the creation throws, the memory snapshot does not hold this commit (20.1 keeps the previous one):
    /// the client is poisoned, then the exception goes on.
    /// </summary>
    /// <param name="monitor">The monitor.</param>
    /// <param name="d">The domain.</param>
    /// <param name="initialOne">True for the first snapshot of this client.</param>
    /// <param name="ignoreSkipTransactionCount">True to create a snapshot whatever <see cref="MemoryTransactionProviderClient.SkipTransactionCount"/>.</param>
    protected override void CreateSnapshot( IActivityMonitor monitor, IObservableDomain d, bool initialOne, bool ignoreSkipTransactionCount )
    {
        try
        {
            base.CreateSnapshot( monitor, d, initialOne, ignoreSkipTransactionCount );
        }
        catch( Exception ex )
        {
            Poison( monitor, "the creation of the memory snapshot failed", ex );
            throw;
        }
    }

    // Logs a fatal error once. The saver reads IsPoisoned before each step: it starts no timer and writes no file.
    void Poison( IActivityMonitor monitor, string reason, Exception ex )
    {
        if( Interlocked.Exchange( ref _poisoned, 1 ) != 0 ) return;
        monitor.Fatal( $"Domain '{_domainName}': {reason}. The state in memory cannot be trusted. The driver writes no snapshot file until the next start, "
                       + "and the next start loads the last good generation.", ex );
    }

    /// <summary>Calls the deserialization constructor of <see cref="ObservableDomain"/>.</summary>
    protected override ObservableDomain DeserializeDomain( IActivityMonitor monitor, RewindableStream stream, bool? startTimer )
    {
        return new ObservableDomain( monitor, _domainName, this, stream, _serviceProvider, startTimer );
    }
}
