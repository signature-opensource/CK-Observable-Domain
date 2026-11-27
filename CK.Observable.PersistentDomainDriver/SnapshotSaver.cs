using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CK.Core;

namespace CK.Observable;

/// <summary>
/// A snapshot that waits for its save. The payload is a copy that belongs to the save:
/// no commit changes it.
/// </summary>
/// <param name="TransactionSerialNumber">The transaction serial number of the domain at the commit.</param>
/// <param name="CommitTimeUtc">The UTC time of the commit.</param>
/// <param name="Payload">The snapshot bytes.</param>
internal sealed record PendingSnapshot( int TransactionSerialNumber, DateTime CommitTimeUtc, byte[] Payload );

/// <summary>
/// Writes the snapshots of one domain to its <see cref="GenerationStore"/> and decides when a commit is due for a save.
/// <para>
/// Only one save runs at a time. A save writes the newest offered snapshot, so a save that waits
/// for the save lock can write a snapshot newer than the one of its commit.
/// </para>
/// <para>
/// With a positive <see cref="PersistentDomainOptions.SnapshotSaveDelay"/>, the first commit of a delay window claims the save
/// under the domain lock: the window starts at its commit time. A commit that comes later in the window is skipped and starts
/// a one-shot timer for the end of the window.
/// </para>
/// <para>
/// The timer saves without a transaction. When it fires, it compares the last committed transaction number with the
/// saved one. If the last committed state is saved, it does nothing. If a commit claimed the current window after the
/// timer started, the save of that commit carries the state: the timer checks again at the end of the window.
/// Otherwise it claims the next window and saves. If the snapshot of a commit waits for its write, the timer writes it
/// with no copy. Else it copies the committed state under the domain read lock (the capture function) and saves the copy.
/// So after the last commit of a burst, exactly one save writes the last state.
/// A save never writes a snapshot that is not newer than the saved one.
/// </para>
/// <para>
/// With a zero or positive delay, the timer also checks after each save. If the domain is ahead of the saved state, it
/// tries again: after a failed write, after 1, 2, 4 ... seconds (60 seconds at most); else after the delay, at least 1 second.
/// A due commit also starts this check: the domain skips the post actions of a transaction when a TransactionDone handler
/// fails, and then the save of the commit does not run. A negative delay has no timer and no retry.
/// </para>
/// <para>
/// When the persistence is poisoned (the function given to the constructor returns true), the saver writes no file,
/// applies no retention and starts no timer, until the end of its life.
/// </para>
/// <para>
/// <see cref="CloseAsync"/> ends the life of the saver: after it, a save writes no file. The driver closes the saver
/// before it releases the lock of the folder.
/// </para>
/// </summary>
internal sealed class SnapshotSaver : IDisposable
{
    // A commit takes its commit time under the domain lock, before it takes the timer lock to claim.
    // Between these two steps, the timer can claim a window at a later time. The commit then sees a next due time
    // a little more than one delay ahead of its commit time. Without this tolerance, the commit takes this for
    // a clock that went back and claims a second save in the same window. The tolerance also absorbs small clock corrections.
    static readonly TimeSpan _clockBackTolerance = TimeSpan.FromSeconds( 1 );
    // The shortest wait of a check: a zero delay must not give a busy loop.
    static readonly TimeSpan _minCheckDelay = TimeSpan.FromSeconds( 1 );
    // The longest wait between two tries after failed writes.
    static readonly TimeSpan _maxRetryDelay = TimeSpan.FromSeconds( 60 );

    readonly GenerationStore _store;
    readonly PersistentDomainOptions _options;
    readonly Func<IActivityMonitor, PendingSnapshot> _capture;
    readonly Func<int> _lastCommittedTransactionNumber;
    readonly Func<bool> _isPoisoned;
    readonly TimeSpan _delay;
    readonly SemaphoreSlim _saveLock;
    readonly object _pendingLock;
    readonly object _timerLock;
    PendingSnapshot? _pending;
    Timer? _timer;
    // The UTC time for which the timer waits. Null when the timer does not wait. Under _timerLock.
    DateTime? _timerDueUtc;
    bool _stopped;
    // True after CloseAsync. Under _saveLock.
    bool _closed;
    // The saves that the timer started and that did not end yet. Under _timerLock.
    Task? _savesInFlight;
    int _savedTransactionNumber;
    // The UTC time from which a commit or the timer can claim a save. Under _timerLock.
    DateTime _nextDue;
    // The number of failed saves in a row. Under _timerLock.
    int _failureCount;
    // The marks of the load. The first save applies them before the rename of its file. Under _saveLock.
    IReadOnlyList<GenerationFileMark>? _loadMarks;
    // The marks that failed. The retention never deletes their files and tries them again. Under _saveLock.
    List<GenerationFileMark> _failedMarks;

    /// <summary>
    /// Initializes a new saver.
    /// </summary>
    /// <param name="store">The store. The saver serializes all its calls to the store.</param>
    /// <param name="options">The options of the domain.</param>
    /// <param name="capture">
    /// The function that copies the committed state of the domain under its read lock.
    /// The timer calls it for a save that no commit carries. It must not run inside a transaction.
    /// </param>
    /// <param name="lastCommittedTransactionNumber">The function that gets the transaction number of the last commit.</param>
    /// <param name="isPoisoned">The function that tells whether the state in memory cannot be trusted. Null for never.</param>
    public SnapshotSaver( GenerationStore store,
                          PersistentDomainOptions options,
                          Func<IActivityMonitor, PendingSnapshot> capture,
                          Func<int> lastCommittedTransactionNumber,
                          Func<bool>? isPoisoned = null )
    {
        Throw.CheckNotNullArgument( store );
        Throw.CheckNotNullArgument( options );
        Throw.CheckNotNullArgument( capture );
        Throw.CheckNotNullArgument( lastCommittedTransactionNumber );
        _store = store;
        _options = options;
        _capture = capture;
        _lastCommittedTransactionNumber = lastCommittedTransactionNumber;
        _isPoisoned = isPoisoned ?? (() => false);
        _delay = options.SnapshotSaveDelay;
        _saveLock = new SemaphoreSlim( 1, 1 );
        _pendingLock = new object();
        _timerLock = new object();
        _savedTransactionNumber = -1;
        _nextDue = DateTime.MinValue;
        _failedMarks = new List<GenerationFileMark>();
        if( _delay >= TimeSpan.Zero )
        {
            _timer = new Timer( OnTimer, null, Timeout.Infinite, Timeout.Infinite );
        }
    }

    /// <summary>
    /// Gets the transaction serial number of the last saved snapshot. -1 before the first save.
    /// </summary>
    public int SavedTransactionNumber => Volatile.Read( ref _savedTransactionNumber );

    /// <summary>
    /// Gets or sets whether a save applies the retention of the store after it writes a file.
    /// Defaults to false. The driver sets it after the first save of a successful start.
    /// </summary>
    public bool RetentionEnabled { get; set; }

    /// <summary>
    /// Sets the marks of the load. The first save that writes a file applies them after the flush of
    /// its temporary file and before the rename. So a start that writes no file marks no file, and a written
    /// file never exists without the marks. Call this method before the first commit that can save.
    /// <para>
    /// A mark that fails stays pending: the retention never deletes its file and tries the mark again at each pass.
    /// </para>
    /// </summary>
    /// <param name="marks">The marks to apply.</param>
    public void SetLoadMarks( IReadOnlyList<GenerationFileMark> marks )
    {
        Throw.CheckNotNullArgument( marks );
        _loadMarks = marks;
    }

    /// <summary>
    /// Applies the retention of the store under the save lock, and tries the failed marks again.
    /// This method never throws: it logs a warning. After <see cref="CloseAsync"/>, it does nothing.
    /// </summary>
    /// <param name="monitor">The monitor.</param>
    /// <returns>The task.</returns>
    public async Task ApplyRetentionAsync( IActivityMonitor monitor )
    {
        try
        {
            await _saveLock.WaitAsync().ConfigureAwait( false );
            try
            {
                if( !_closed && !_isPoisoned() ) ApplyRetention( monitor );
            }
            finally
            {
                _saveLock.Release();
            }
        }
        catch( Exception ex )
        {
            monitor.Warn( $"Domain '{_store.DomainName}': error while the driver applies the retention. The next save applies it again.", ex );
        }
    }

    // Under _saveLock. Runs just before the rename of the first written file.
    void ApplyLoadMarks( IActivityMonitor monitor )
    {
        var marks = _loadMarks;
        _loadMarks = null;
        if( marks == null || marks.Count == 0 ) return;
        try
        {
            _failedMarks.AddRange( _store.ApplyMarks( monitor, marks, DateTime.UtcNow ) );
        }
        catch( Exception ex )
        {
            // The marks can be applied again: the next retention tries all of them.
            monitor.Error( $"Domain '{_store.DomainName}': error while the driver marks the files that it did not load. The retention does not delete them.", ex );
            _failedMarks.AddRange( marks );
        }
    }

    // Under _saveLock. Tries the failed marks again, then deletes the files that the store does not need.
    // The retention never deletes the file of a pending or failed mark.
    void ApplyRetention( IActivityMonitor monitor )
    {
        var utcNow = DateTime.UtcNow;
        if( _failedMarks.Count > 0 )
        {
            _failedMarks = _store.ApplyMarks( monitor, _failedMarks, utcNow, isRetry: true );
        }
        HashSet<long>? protectedGenerations = null;
        if( _failedMarks.Count > 0 || _loadMarks != null )
        {
            protectedGenerations = new HashSet<long>( _failedMarks.Select( m => m.File.Generation ) );
            if( _loadMarks != null ) protectedGenerations.UnionWith( _loadMarks.Select( m => m.File.Generation ) );
        }
        _store.ApplyRetention( monitor, _options.GenerationCount, _options.DamagedFileKeepDuration, utcNow, protectedGenerations );
    }

    /// <summary>
    /// Claims a save for a commit at <paramref name="commitTimeUtc"/>. The client calls it under the domain lock.
    /// A zero delay gives true. A negative delay gives false.
    /// A positive delay gives true only for the first commit of a delay window: the claim then moves the next due time
    /// to <c>commitTimeUtc + SnapshotSaveDelay</c>.
    /// The claim does not wait for the end of the save, so a commit during a save in progress does not claim a second save.
    /// If the next due time is more than the delay plus 1 second ahead, the clock went back: the commit starts a new window.
    /// A forced save (the save command) needs no claim and does not move the next due time.
    /// </summary>
    /// <param name="commitTimeUtc">The UTC time of the commit.</param>
    /// <returns>True if the commit has the save.</returns>
    public bool TryClaimSave( DateTime commitTimeUtc )
    {
        if( _delay == TimeSpan.Zero ) return true;
        if( _delay < TimeSpan.Zero ) return false;
        lock( _timerLock )
        {
            return ClaimWindow( commitTimeUtc );
        }
    }

    // Under _timerLock. A claim starts a new delay window at timeUtc.
    // If the next due time is more than one delay (plus the tolerance) ahead, the clock went back: the claim is due,
    // so that the claims do not stop until the clock catches up.
    bool ClaimWindow( DateTime timeUtc )
    {
        if( timeUtc < _nextDue && _nextDue - timeUtc <= _delay + _clockBackTolerance ) return false;
        _nextDue = timeUtc + _delay;
        return true;
    }

    /// <summary>
    /// Offers a snapshot for the next save. The saver keeps the snapshot with the highest serial number.
    /// A snapshot that is not newer than the saved one is ignored: a capture runs outside the domain lock,
    /// so its offer can come after the write of a newer snapshot.
    /// </summary>
    /// <param name="snapshot">The snapshot.</param>
    public void Offer( PendingSnapshot snapshot )
    {
        Throw.CheckNotNullArgument( snapshot );
        lock( _pendingLock )
        {
            if( snapshot.TransactionSerialNumber <= SavedTransactionNumber ) return;
            if( _pending == null || snapshot.TransactionSerialNumber > _pending.TransactionSerialNumber )
            {
                _pending = snapshot;
            }
        }
    }

    /// <summary>
    /// Writes the newest offered snapshot. If no snapshot waits, or if the offered snapshot is already saved,
    /// the method does nothing and returns true. This method never throws: it logs the error and returns false.
    /// An error of the retention after a good write logs a warning: the method then returns true.
    /// After <see cref="CloseAsync"/>, the method writes no file and returns false.
    /// <para>
    /// After the save, if the domain is ahead of the saved state, the method starts the timer for a new try
    /// (see the remarks of the class).
    /// </para>
    /// </summary>
    /// <param name="monitor">The monitor.</param>
    /// <returns>True on success, false if the save failed.</returns>
    public async Task<bool> SaveAsync( IActivityMonitor monitor )
    {
        bool success = await DoSaveAsync( monitor ).ConfigureAwait( false );
        ScheduleCheckAfterSave( success );
        return success;
    }

    async Task<bool> DoSaveAsync( IActivityMonitor monitor )
    {
        try
        {
            await _saveLock.WaitAsync().ConfigureAwait( false );
            try
            {
                if( _closed )
                {
                    monitor.Warn( $"Domain '{_store.DomainName}': the saver is closed. The save writes no file." );
                    return false;
                }
                if( _isPoisoned() )
                {
                    monitor.Warn( $"Domain '{_store.DomainName}': the state in memory cannot be trusted. The save writes no file." );
                    return false;
                }
                PendingSnapshot? snapshot;
                lock( _pendingLock )
                {
                    snapshot = _pending;
                }
                // Newest wins: a snapshot that is not newer than the saved one is never written over it.
                // The loader takes the highest generation, so an older state in a new file hides the newer one.
                if( snapshot == null || snapshot.TransactionSerialNumber <= _savedTransactionNumber )
                {
                    if( snapshot != null )
                    {
                        lock( _pendingLock )
                        {
                            if( _pending == snapshot ) _pending = null;
                        }
                    }
                    return true;
                }
                Action? beforeRename = _loadMarks != null ? () => ApplyLoadMarks( monitor ) : null;
                var file = _store.WriteGeneration( snapshot.TransactionSerialNumber, snapshot.CommitTimeUtc, snapshot.Payload, beforeRename );
                Volatile.Write( ref _savedTransactionNumber, snapshot.TransactionSerialNumber );
                lock( _pendingLock )
                {
                    // Release the bytes. A newer offer stays.
                    if( _pending == snapshot ) _pending = null;
                }
                monitor.Trace( $"Domain '{_store.DomainName}': snapshot n°{snapshot.TransactionSerialNumber} saved to '{file.Path}'." );
                if( RetentionEnabled )
                {
                    // The file is written: a retention error does not fail the save and keeps the claim of the window.
                    try
                    {
                        ApplyRetention( monitor );
                    }
                    catch( Exception ex )
                    {
                        monitor.Warn( $"Domain '{_store.DomainName}': error while the driver applies the retention. The next save applies it again.", ex );
                    }
                }
                return true;
            }
            finally
            {
                _saveLock.Release();
            }
        }
        catch( Exception ex )
        {
            monitor.Error( $"Domain '{_store.DomainName}': error while saving the snapshot to '{_store.Folder}'.", ex );
            // The claim of the window gave no file: the next commit claims a new save at once.
            lock( _timerLock )
            {
                _nextDue = DateTime.MinValue;
            }
            return false;
        }
    }

    /// <summary>
    /// Starts the timer of a trailing save for a skipped commit. This applies only to a positive delay.
    /// The client calls it under the domain lock, for a commit that did not claim a save.
    /// If the timer already waits for an earlier time, or if the saver is stopped, the method does nothing.
    /// The timer ends when the current delay window ends.
    /// </summary>
    public void ScheduleTrailingSave()
    {
        lock( _timerLock )
        {
            if( _stopped || _timer == null || _delay <= TimeSpan.Zero || _isPoisoned() ) return;
            ArmTimer( WaitForTheWindowEnd() );
        }
    }

    /// <summary>
    /// Starts the timer of a check for a due commit. The client calls it under the domain lock, after it adds the save post action.
    /// If this post action does not run, the check at the end of the wait saves the state. If the post action saves the state,
    /// the check does nothing. The wait is the delay, at least 1 second. A negative delay has no check.
    /// </summary>
    public void ScheduleSaveCheck()
    {
        lock( _timerLock )
        {
            if( _stopped || _timer == null || _isPoisoned() ) return;
            ArmTimer( _delay > _minCheckDelay ? _delay : _minCheckDelay );
        }
    }

    // After each save: if the domain is ahead of the saved state, a new try comes later.
    void ScheduleCheckAfterSave( bool success )
    {
        lock( _timerLock )
        {
            _failureCount = success ? 0 : Math.Min( _failureCount + 1, 7 );
            if( _stopped || _timer == null || _isPoisoned() ) return;
            if( _lastCommittedTransactionNumber() <= SavedTransactionNumber ) return;
            TimeSpan wait;
            if( success )
            {
                wait = _delay > _minCheckDelay ? _delay : _minCheckDelay;
            }
            else
            {
                // 1, 2, 4 ... seconds, 60 seconds at most.
                wait = TimeSpan.FromSeconds( 1 << (_failureCount - 1) );
                if( wait > _maxRetryDelay ) wait = _maxRetryDelay;
            }
            ArmTimer( wait );
        }
    }

    // Under _timerLock. The wait until the end of the current window, between zero and the delay.
    // A clock that went back cannot make it longer than the delay.
    TimeSpan WaitForTheWindowEnd()
    {
        var wait = _nextDue - DateTime.UtcNow;
        if( wait < TimeSpan.Zero ) return TimeSpan.Zero;
        return wait > _delay ? _delay : wait;
    }

    // Under _timerLock, with a timer. Starts the timer for the wait. If the timer already waits for an earlier time,
    // it keeps that time: the earlier check does the work of the later one.
    void ArmTimer( TimeSpan wait )
    {
        Debug.Assert( _timer != null );
        if( wait < TimeSpan.Zero ) wait = TimeSpan.Zero;
        var dueUtc = DateTime.UtcNow + wait;
        if( _timerDueUtc.HasValue && _timerDueUtc.Value <= dueUtc ) return;
        _timerDueUtc = dueUtc;
        _timer.Change( wait, Timeout.InfiniteTimeSpan );
    }

    void OnTimer( object? state )
    {
        lock( _timerLock )
        {
            _timerDueUtc = null;
            if( _stopped || _timer == null || _isPoisoned() ) return;
            // The last committed state is saved: nothing to do.
            if( _lastCommittedTransactionNumber() <= SavedTransactionNumber ) return;
            if( _delay > TimeSpan.Zero && !ClaimWindow( DateTime.UtcNow ) )
            {
                // A commit claimed the current window after the timer started. Its save carries the state of the
                // commits before it. A commit after it needs a save at the end of this window: the timer checks again then.
                ArmTimer( WaitForTheWindowEnd() );
                return;
            }
            // Task.Run only queues the save: the capture does not run under the timer lock.
            // The save is in the in-flight task before the lock ends, so StopAsync always sees it.
            var save = Task.Run( TimerSaveAsync );
            _savesInFlight = _savesInFlight is { IsCompleted: false } previous
                                ? Task.WhenAll( previous, save )
                                : save;
        }
    }

    // A save that no commit carries: the timer copies the committed state and saves it.
    async Task TimerSaveAsync()
    {
        var monitor = new ActivityMonitor( $"Snapshot save of domain '{_store.DomainName}'." );
        try
        {
            // A snapshot of a commit that waits for its write is written first, with no copy: a copy of the whole
            // domain each second under steady commits costs too much. If the domain is still ahead after this save,
            // the check after the save starts the timer again, and the next pass copies the state.
            bool waits;
            lock( _pendingLock )
            {
                waits = _pending != null && _pending.TransactionSerialNumber > SavedTransactionNumber;
            }
            if( !waits )
            {
                PendingSnapshot snapshot;
                try
                {
                    snapshot = _capture( monitor );
                }
                catch( Exception ex )
                {
                    monitor.Error( $"Domain '{_store.DomainName}': error while the driver copies the committed state for a save.", ex );
                    ScheduleCheckAfterSave( false );
                    return;
                }
                Offer( snapshot );
            }
            await SaveAsync( monitor ).ConfigureAwait( false );
        }
        finally
        {
            monitor.MonitorEnd();
        }
    }

    /// <summary>
    /// Sets the stop flag, disposes the timer, then waits for the saves that the timer started.
    /// When the task ends, no timer save runs and no timer save can start. <see cref="SaveAsync"/> still works, for the final save.
    /// <para>
    /// A timer save takes the domain read lock. Do not call this method inside a transaction of the domain:
    /// the wait never ends.
    /// </para>
    /// </summary>
    /// <returns>The task that ends when no timer save runs.</returns>
    public Task StopAsync()
    {
        Task? inFlight;
        lock( _timerLock )
        {
            _stopped = true;
            _timer?.Dispose();
            _timer = null;
            _timerDueUtc = null;
            inFlight = _savesInFlight;
            _savesInFlight = null;
        }
        return inFlight ?? Task.CompletedTask;
    }

    /// <summary>
    /// Calls <see cref="StopAsync"/> and does not wait for the saves in flight. Use <see cref="StopAsync"/> to wait.
    /// The save lock stays usable: a post action that runs after this call
    /// gets a normal result, not an <see cref="ObjectDisposedException"/>.
    /// </summary>
    public void Dispose() => _ = StopAsync();

    /// <summary>
    /// Calls <see cref="StopAsync"/>, then waits for the save in progress and closes the saver.
    /// When the task ends, no save runs and a later <see cref="SaveAsync"/> writes no file.
    /// A save post action of a commit before the close can run after it: this save then writes nothing.
    /// <para>
    /// Do not call this method inside a transaction of the domain: the wait never ends.
    /// </para>
    /// </summary>
    /// <returns>The task that ends when the saver is closed.</returns>
    public async Task CloseAsync()
    {
        await StopAsync().ConfigureAwait( false );
        await _saveLock.WaitAsync().ConfigureAwait( false );
        _closed = true;
        _saveLock.Release();
    }
}
