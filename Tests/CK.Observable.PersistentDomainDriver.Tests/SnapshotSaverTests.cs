using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CK.Core;
using NUnit.Framework;
using Shouldly;

namespace CK.Observable.PersistentDomainDriverTests;

[TestFixture]
public class SnapshotSaverTests
{
    const string _domainName = "Test";

    static readonly DateTime _commitTime = new DateTime( 2026, 10, 27, 8, 0, 0, DateTimeKind.Utc );

    /// <summary>
    /// A domain with a client and a saver over a new folder, as the driver builds them.
    /// The capture function copies the committed state under the domain read lock. It counts its calls and keeps its exceptions.
    /// </summary>
    sealed class Harness : IDisposable
    {
        int _requestCount;
        readonly List<Exception> _requestErrors = new List<Exception>();

        /// <summary>
        /// Creates the harness. If <paramref name="initialObjects"/> is positive, a first commit creates
        /// these counters before the saver is set: that commit writes no file.
        /// </summary>
        public Harness( PersistentDomainOptions options, int initialObjects = 0 )
        {
            Folder = TestStore.NewDirectory();
            Store = new GenerationStore( Folder, _domainName );
            Monitor = new ActivityMonitor();
            Errors = new ActivityMonitorSimpleCollector { MinimalFilter = LogLevelFilter.Error, Capacity = 50 };
            Monitor.Output.RegisterClient( Errors );
            Client = new PersistentDomainClient( _domainName, options, null );
            Domain = Client.CreateNew( Monitor );
            if( initialObjects > 0 )
            {
                Domain.ModifyThrowAsync( Monitor, () =>
                {
                    for( int i = 0; i < initialObjects; ++i ) new OCounter { Count = i };
                } ).GetAwaiter().GetResult();
            }
            Saver = new SnapshotSaver( Store, options, Capture, () => Client.LastCommittedTransactionNumber );
            Client.Saver = Saver;
        }

        public string Folder { get; }

        public GenerationStore Store { get; }

        public ActivityMonitor Monitor { get; }

        public ActivityMonitorSimpleCollector Errors { get; }

        public PersistentDomainClient Client { get; }

        public ObservableDomain Domain { get; }

        public SnapshotSaver Saver { get; }

        public int RequestCount => Volatile.Read( ref _requestCount );

        public IReadOnlyList<Exception> RequestErrors
        {
            get
            {
                lock( _requestErrors ) return _requestErrors.ToArray();
            }
        }

        PendingSnapshot Capture( IActivityMonitor m )
        {
            Interlocked.Increment( ref _requestCount );
            try
            {
                return Client.CaptureCommittedSnapshot( m, Domain );
            }
            catch( Exception ex )
            {
                lock( _requestErrors ) _requestErrors.Add( ex );
                throw;
            }
        }

        /// <summary>Adds one to the counter. The first call creates the counter.</summary>
        public Task IncrementAsync( IActivityMonitor? monitor = null, bool parallelDomainPostActions = true )
        {
            return Domain.ModifyThrowAsync( monitor ?? Monitor, () =>
            {
                var counter = Domain.AllObjects.Items.OfType<OCounter>().FirstOrDefault() ?? new OCounter();
                counter.Count++;
            }, parallelDomainPostActions: parallelDomainPostActions, waitForDomainPostActionsCompletion: true );
        }

        public int OdFileCount => Store.ListGenerations().Count;

        public void Dispose()
        {
            Saver.Dispose();
            Domain.Dispose();
        }
    }

    /// <summary>Reads a file, checks its format and loads its payload in a new client.</summary>
    static (SnapshotFileHeader Header, int Count) ReadAndLoad( IActivityMonitor monitor, string path )
    {
        byte[]? payload;
        SnapshotFileHeader header;
        using( var input = File.OpenRead( path ) )
        {
            SnapshotFileFormat.Read( input, out header, out payload ).ShouldBe( SnapshotReadStatus.Valid, $"File '{path}' is not valid." );
        }
        payload.ShouldNotBeNull();
        var client = new PersistentDomainClient( _domainName, new PersistentDomainOptions(), null );
        using var d = client.Load( monitor, payload );
        d.TransactionSerialNumber.ShouldBe( header.TransactionSerialNumber );
        var count = d.AllObjects.Items.OfType<OCounter>().FirstOrDefault()?.Count ?? 0;
        return (header, count);
    }

    /// <summary>A saver with no domain: its capture throws and the last committed number is -1, so its timer saves nothing.</summary>
    static SnapshotSaver NewSaver( GenerationStore store, PersistentDomainOptions options )
    {
        return new SnapshotSaver( store, options, _ => throw new InvalidOperationException( "No domain." ), () => -1 );
    }

    /// <summary>
    /// The commits of a domain for the timer tests: <see cref="Commit"/> records the last committed number, and
    /// the capture returns a snapshot of this number. It counts the captures.
    /// </summary>
    sealed class FakeCommits
    {
        int _last = -1;
        int _captureCount;

        public int Last => Volatile.Read( ref _last );

        public int CaptureCount => Volatile.Read( ref _captureCount );

        public int Commit() => Interlocked.Increment( ref _last );

        public PendingSnapshot Capture( IActivityMonitor monitor )
        {
            Interlocked.Increment( ref _captureCount );
            return new PendingSnapshot( Last, DateTime.UtcNow, new byte[] { 1 } );
        }

        public SnapshotSaver NewSaver( GenerationStore store, PersistentDomainOptions options ) => new SnapshotSaver( store, options, Capture, () => Last );

        /// <summary>A due commit: it records the number, offers the snapshot and saves it, as the post action does.</summary>
        public async Task DueCommitAsync( SnapshotSaver saver )
        {
            int n = Commit();
            saver.Offer( new PendingSnapshot( n, DateTime.UtcNow, new byte[] { 1 } ) );
            (await saver.SaveAsync( new ActivityMonitor() )).ShouldBeTrue();
        }
    }

    static async Task<bool> PollAsync( Func<bool> condition, TimeSpan timeout )
    {
        var deadline = DateTime.UtcNow + timeout;
        while( !condition() )
        {
            if( DateTime.UtcNow > deadline ) return false;
            await Task.Delay( 10 );
        }
        return true;
    }

    [Test]
    public async Task zero_delay_saves_each_commit()
    {
        using var h = new Harness( new PersistentDomainOptions { SnapshotSaveDelay = TimeSpan.Zero } );
        h.OdFileCount.ShouldBe( 0 );
        h.Saver.SavedTransactionNumber.ShouldBe( -1 );

        for( int i = 0; i < 3; ++i ) await h.IncrementAsync();

        var files = h.Store.ListGenerations();
        files.Count.ShouldBe( 3 );
        var (header, count) = ReadAndLoad( h.Monitor, files[0].Path );
        count.ShouldBe( 3 );
        header.TransactionSerialNumber.ShouldBe( h.Domain.TransactionSerialNumber );
        h.Saver.SavedTransactionNumber.ShouldBe( h.Domain.TransactionSerialNumber );
        h.Errors.Entries.ShouldBeEmpty();
    }

    [Test]
    public async Task negative_delay_saves_only_on_the_save_command()
    {
        using var h = new Harness( new PersistentDomainOptions { SnapshotSaveDelay = Timeout.InfiniteTimeSpan } );

        await h.IncrementAsync();
        await h.IncrementAsync();
        h.OdFileCount.ShouldBe( 0 );

        await h.Domain.ModifyThrowAsync( h.Monitor, () => h.Domain.SendSnapshotCommand(), waitForDomainPostActionsCompletion: true );

        var files = h.Store.ListGenerations();
        files.Count.ShouldBe( 1 );
        ReadAndLoad( h.Monitor, files[0].Path ).Count.ShouldBe( 2 );
        h.RequestCount.ShouldBe( 0 );
        h.Errors.Entries.ShouldBeEmpty();
    }

    [Test]
    public async Task positive_delay_writes_a_trailing_save_after_idle()
    {
        // A long delay: on a slow agent, commit B still comes before the end of the window of commit A.
        var delay = TimeSpan.FromSeconds( 2 );
        using var h = new Harness( new PersistentDomainOptions { SnapshotSaveDelay = delay } );

        // Commit A is due: no save happened before.
        await h.IncrementAsync();
        h.OdFileCount.ShouldBe( 1 );
        await Task.Delay( 10 );
        // Commit B comes before the end of the delay: it is not due.
        await h.IncrementAsync();
        h.OdFileCount.ShouldBe( 1 );

        // No other commit comes. The timer requests the trailing save.
        (await PollAsync( () => h.OdFileCount == 2, delay + TimeSpan.FromSeconds( 10 ) )).ShouldBeTrue( "The trailing save did not come." );

        var files = h.Store.ListGenerations();
        ReadAndLoad( h.Monitor, files[0].Path ).Count.ShouldBe( 2 );
        h.RequestCount.ShouldBe( 1 );
        h.RequestErrors.ShouldBeEmpty();
        h.Errors.Entries.ShouldBeEmpty();
    }

    [Test]
    public async Task commits_during_saves_never_give_a_torn_file()
    {
        using var h = new Harness( new PersistentDomainOptions { SnapshotSaveDelay = TimeSpan.Zero, CompressionKind = CompressionKind.None } );

        // A large domain: each snapshot takes time to create and to copy, so commits and saves overlap.
        await h.Domain.ModifyThrowAsync( h.Monitor, () =>
        {
            for( int i = 0; i < 5000; ++i ) new OCounter { Count = i };
        } );

        const int taskCount = 4;
        const int commitsPerTask = 50;
        var tasks = Enumerable.Range( 0, taskCount ).Select( _ => Task.Run( async () =>
        {
            // A monitor is not thread-safe: each task has its own.
            var m = new ActivityMonitor();
            for( int i = 0; i < commitsPerTask; ++i )
            {
                await h.Domain.ModifyThrowAsync( m, () =>
                {
                    new OCounter { Count = i };
                }, parallelDomainPostActions: true );
            }
        } ) ).ToArray();
        await Task.WhenAll( tasks );

        var files = h.Store.ListGenerations();
        files.Count.ShouldBeGreaterThan( 0 );
        foreach( var f in files )
        {
            ReadAndLoad( h.Monitor, f.Path );
        }
        // The last commit offers the last snapshot. Its post action saves it before ModifyThrowAsync returns.
        var newest = ReadAndLoad( h.Monitor, files[0].Path );
        newest.Header.TransactionSerialNumber.ShouldBe( h.Domain.TransactionSerialNumber );
        h.Errors.Entries.ShouldBeEmpty();
    }

    [Test]
    public async Task stop_while_the_timer_waits_does_not_save_later()
    {
        // A long delay: on a slow agent, the second commit still comes before the end of the window of the first.
        var delay = TimeSpan.FromSeconds( 2 );
        using var h = new Harness( new PersistentDomainOptions { SnapshotSaveDelay = delay } );

        await h.IncrementAsync();
        await h.IncrementAsync();
        await h.Saver.StopAsync();
        var countAtStop = h.OdFileCount;
        countAtStop.ShouldBe( 1 );

        // The end of the window passes. The disposed timer sends no request.
        await Task.Delay( delay + TimeSpan.FromMilliseconds( 500 ) );

        h.OdFileCount.ShouldBe( countAtStop );
        h.RequestCount.ShouldBe( 0 );
        h.RequestErrors.ShouldBeEmpty();
        h.Errors.Entries.ShouldBeEmpty();
    }

    [Test]
    public async Task a_save_during_a_save_in_progress_waits_for_it()
    {
        var folder = TestStore.NewDirectory();
        var store = new GenerationStore( folder, _domainName );
        using var saver = NewSaver( store, new PersistentDomainOptions() );

        // A large payload: its checksum, write and flush take time.
        var large = new byte[64 * 1024 * 1024];
        large[0] = 1;
        large[^1] = 2;
        saver.Offer( new PendingSnapshot( 1, _commitTime, large ) );
        var first = Task.Run( () => saver.SaveAsync( new ActivityMonitor() ) );

        // The temporary file exists while the first save writes.
        bool InProgress() => Directory.EnumerateFiles( folder, "*.tmp" ).Any();
        var deadline = DateTime.UtcNow.AddSeconds( 5 );
        while( !InProgress() && !first.IsCompleted && DateTime.UtcNow < deadline ) { }
        InProgress().ShouldBeTrue( "The first save did not start, or it ended before the test saw it." );

        saver.Offer( new PendingSnapshot( 2, _commitTime.AddSeconds( 1 ), new byte[] { 3, 4, 5 } ) );
        var second = saver.SaveAsync( new ActivityMonitor() );
        // The second save waits for the save lock.
        second.IsCompleted.ShouldBeFalse();

        (await first).ShouldBeTrue();
        (await second).ShouldBeTrue();

        var files = store.ListGenerations();
        files.Count.ShouldBe( 2 );
        var headers = new List<SnapshotFileHeader>();
        foreach( var f in files )
        {
            using var input = File.OpenRead( f.Path );
            SnapshotFileFormat.Read( input, out var header, out _ ).ShouldBe( SnapshotReadStatus.Valid );
            headers.Add( header );
        }
        headers[0].Generation.ShouldNotBe( headers[1].Generation );
        headers.Select( x => x.TransactionSerialNumber ).OrderBy( x => x ).ShouldBe( new[] { 1, 2 } );
        saver.SavedTransactionNumber.ShouldBe( 2 );
    }

    // C1: a capture outside the domain lock can offer an older snapshot after a newer one is written.
    // The saver must not write it: the highest generation must hold the newest state.
    [Test]
    public async Task an_offer_older_than_the_saved_snapshot_writes_nothing()
    {
        var folder = TestStore.NewDirectory();
        var store = new GenerationStore( folder, _domainName );
        using var saver = NewSaver( store, new PersistentDomainOptions() );

        saver.Offer( new PendingSnapshot( 2, _commitTime, new byte[] { 2 } ) );
        (await saver.SaveAsync( new ActivityMonitor() )).ShouldBeTrue();
        saver.Offer( new PendingSnapshot( 1, _commitTime, new byte[] { 1 } ) );
        (await saver.SaveAsync( new ActivityMonitor() )).ShouldBeTrue();

        store.ListGenerations().ShouldHaveSingleItem();
        saver.SavedTransactionNumber.ShouldBe( 2 );
    }

    // M2: when a snapshot of a commit already waits for its write, the timer writes it and copies nothing.
    // Under steady commits with a zero delay, this avoids a full copy of the domain each second.
    [Test]
    public async Task the_timer_does_not_capture_when_a_snapshot_waits_for_its_write()
    {
        var store = new GenerationStore( TestStore.NewDirectory(), _domainName );
        var commits = new FakeCommits();
        using var saver = commits.NewSaver( store, new PersistentDomainOptions { SnapshotSaveDelay = TimeSpan.Zero } );

        // A due commit offers its snapshot and starts the check. Its save post action does not run.
        int n = commits.Commit();
        saver.Offer( new PendingSnapshot( n, DateTime.UtcNow, new byte[] { 1 } ) );
        saver.ScheduleSaveCheck();

        (await PollAsync( () => saver.SavedTransactionNumber == n, TimeSpan.FromSeconds( 5 ) )).ShouldBeTrue( "The check did not save." );
        commits.CaptureCount.ShouldBe( 0 );
    }

    [Test]
    public async Task a_closed_saver_writes_nothing()
    {
        using var h = new Harness( new PersistentDomainOptions() );
        await h.IncrementAsync();
        h.OdFileCount.ShouldBe( 1 );
        await h.Saver.CloseAsync();
        // A post action of a commit that read the saver before the close can call SaveAsync after the close.
        h.Saver.Offer( new PendingSnapshot( 1000, DateTime.UtcNow, new byte[] { 1, 2, 3 } ) );
        (await h.Saver.SaveAsync( h.Monitor )).ShouldBeFalse();
        h.OdFileCount.ShouldBe( 1 );
        Directory.GetFiles( h.Folder ).Length.ShouldBe( 1 );
    }

    [Test]
    public async Task failed_save_returns_false_and_the_next_save_works()
    {
        using var h = new Harness( new PersistentDomainOptions { SnapshotSaveDelay = TimeSpan.Zero } );
        // Without its folder, the store cannot write the temporary file.
        Directory.Delete( h.Folder );

        // The save of this commit fails. The commit itself succeeds.
        await h.IncrementAsync();
        (await h.Saver.SaveAsync( h.Monitor )).ShouldBeFalse();
        h.Errors.Entries.ShouldNotBeEmpty();
        h.Saver.SavedTransactionNumber.ShouldBe( -1 );

        Directory.CreateDirectory( h.Folder );
        Directory.EnumerateFiles( h.Folder ).ShouldBeEmpty();
        await h.IncrementAsync();

        var files = h.Store.ListGenerations();
        files.Count.ShouldBe( 1 );
        ReadAndLoad( h.Monitor, files[0].Path ).Count.ShouldBe( 2 );
        h.Saver.SavedTransactionNumber.ShouldBe( h.Domain.TransactionSerialNumber );
    }

    /// <summary>Makes the first <paramref name="count"/> renames of the store fail. The error is not a transient one.</summary>
    static void FailRenames( GenerationStore store, int count )
    {
        int calls = 0;
        store.MoveFile = ( source, target ) =>
        {
            if( Interlocked.Increment( ref calls ) <= count ) throw new IOException( "The rename fails (test)." );
            File.Move( source, target, overwrite: false );
        };
    }

    // R6: the save of the last commit before an idle period fails. The timer tries again after 1 second.
    [Test]
    public async Task a_lost_save_during_idle_time_is_retried()
    {
        using var h = new Harness( new PersistentDomainOptions { SnapshotSaveDelay = TimeSpan.Zero } );
        FailRenames( h.Store, 1 );

        await h.IncrementAsync();
        h.Saver.SavedTransactionNumber.ShouldBeLessThan( h.Domain.TransactionSerialNumber );
        h.OdFileCount.ShouldBe( 0 );

        (await PollAsync( () => h.Saver.SavedTransactionNumber == h.Domain.TransactionSerialNumber, TimeSpan.FromSeconds( 5 ) ))
            .ShouldBeTrue( "The lost save was not retried." );
        var files = h.Store.ListGenerations();
        files.Count.ShouldBe( 1 );
        ReadAndLoad( h.Monitor, files[0].Path ).Count.ShouldBe( 1 );
        h.RequestErrors.ShouldBeEmpty();
    }

    // R6: after two failed writes in a row, the retries wait 1 second, then 2 seconds.
    [Test]
    public async Task the_retries_after_failed_writes_wait_longer_each_time()
    {
        using var h = new Harness( new PersistentDomainOptions { SnapshotSaveDelay = TimeSpan.Zero } );
        FailRenames( h.Store, 2 );

        var start = DateTime.UtcNow;
        await h.IncrementAsync();
        (await PollAsync( () => h.Saver.SavedTransactionNumber == h.Domain.TransactionSerialNumber, TimeSpan.FromSeconds( 10 ) ))
            .ShouldBeTrue( "The lost save was not retried." );
        (DateTime.UtcNow - start).ShouldBeGreaterThan( TimeSpan.FromSeconds( 2.5 ) );
        // The snapshot of the commit waits for its write: the retries write it with no copy of the domain.
        h.RequestCount.ShouldBe( 0 );
    }

    // R6: a TransactionDone handler fails once. The domain skips the post actions, so the save of the due commit
    // does not run. The check that the commit started saves the state.
    [Test]
    public async Task a_skipped_save_post_action_is_retried()
    {
        using var h = new Harness( new PersistentDomainOptions { SnapshotSaveDelay = TimeSpan.Zero } );
        bool fail = true;
        h.Domain.TransactionDone += ( sender, e ) =>
        {
            if( fail )
            {
                fail = false;
                throw new InvalidOperationException( "The handler fails once (test)." );
            }
        };

        var result = await h.Domain.ModifyAsync( h.Monitor, () => new OCounter { Count = 1 },
                                                 throwException: false,
                                                 millisecondsTimeout: -1,
                                                 considerRolledbackAsFailure: true,
                                                 parallelDomainPostActions: true,
                                                 waitForDomainPostActionsCompletion: true );
        result.Success.ShouldBeFalse();
        h.OdFileCount.ShouldBe( 0 );

        (await PollAsync( () => h.Saver.SavedTransactionNumber == h.Domain.TransactionSerialNumber, TimeSpan.FromSeconds( 5 ) ))
            .ShouldBeTrue( "The skipped save was not retried." );
        h.OdFileCount.ShouldBe( 1 );
    }

    // A negative delay means no automatic save: a failed save of the save command is not retried.
    [Test]
    public async Task a_negative_delay_has_no_retry()
    {
        using var h = new Harness( new PersistentDomainOptions { SnapshotSaveDelay = Timeout.InfiniteTimeSpan } );
        FailRenames( h.Store, 1 );

        await h.Domain.ModifyThrowAsync( h.Monitor, () =>
        {
            new OCounter { Count = 1 };
            h.Domain.SendSnapshotCommand();
        }, waitForDomainPostActionsCompletion: true );
        await Task.Delay( 1500 );

        h.OdFileCount.ShouldBe( 0 );
        h.RequestCount.ShouldBe( 0 );
    }

    [Test]
    public async Task retention_runs_only_when_enabled()
    {
        using var h = new Harness( new PersistentDomainOptions { SnapshotSaveDelay = TimeSpan.Zero, GenerationCount = 2 } );
        h.Saver.RetentionEnabled.ShouldBeFalse();

        for( int i = 0; i < 4; ++i ) await h.IncrementAsync();
        h.OdFileCount.ShouldBe( 4 );

        h.Saver.RetentionEnabled = true;
        await h.IncrementAsync();

        var files = h.Store.ListGenerations();
        files.Count.ShouldBe( 2 );
        ReadAndLoad( h.Monitor, files[0].Path ).Count.ShouldBe( 5 );
        h.Errors.Entries.ShouldBeEmpty();
    }

    [Test]
    public async Task a_retention_error_after_a_good_write_keeps_the_success_and_the_claim()
    {
        var folder = TestStore.NewDirectory();
        var store = new GenerationStore( folder, _domainName );
        // GenerationCount 0 makes the retention of the store throw after the write. Validate rejects this value,
        // but it is a simple way to get a retention error (for example, a folder enumeration that fails on a UNC path).
        var options = new PersistentDomainOptions { SnapshotSaveDelay = TimeSpan.FromMinutes( 1 ), GenerationCount = 0 };
        using var saver = NewSaver( store, options );
        saver.RetentionEnabled = true;
        var monitor = new ActivityMonitor();
        using var warnings = monitor.CollectEntries( out var entries, LogLevelFilter.Warn );

        saver.TryClaimSave( _commitTime ).ShouldBeTrue();
        saver.Offer( new PendingSnapshot( 1, _commitTime, new byte[] { 1 } ) );
        (await saver.SaveAsync( monitor )).ShouldBeTrue();

        saver.SavedTransactionNumber.ShouldBe( 1 );
        store.ListGenerations().ShouldHaveSingleItem();
        var warning = entries.ShouldHaveSingleItem();
        warning.MaskedLevel.ShouldBe( LogLevel.Warn );
        warning.Text.ShouldContain( "retention" );
        // The write succeeded: the claim of the window stays.
        saver.TryClaimSave( _commitTime.AddSeconds( 1 ) ).ShouldBeFalse();
    }

    [Test]
    public async Task the_load_marks_wait_for_the_first_written_file()
    {
        var folder = TestStore.NewDirectory();
        var store = new GenerationStore( folder, _domainName );
        var old = store.WriteGeneration( 1, _commitTime, new byte[] { 1 } );
        store = new GenerationStore( folder, _domainName );
        using var saver = NewSaver( store, new PersistentDomainOptions() );
        saver.SetLoadMarks( new[] { new GenerationFileMark( old, GenerationMark.Superseded ) } );

        // No snapshot waits: the save writes no file and marks no file.
        (await saver.SaveAsync( new ActivityMonitor() )).ShouldBeTrue();
        File.Exists( old.Path ).ShouldBeTrue();

        saver.Offer( new PendingSnapshot( 2, _commitTime, new byte[] { 2 } ) );
        (await saver.SaveAsync( new ActivityMonitor() )).ShouldBeTrue();
        File.Exists( old.Path ).ShouldBeFalse();
        File.Exists( old.Path + ".superseded" ).ShouldBeTrue();
        store.ListGenerations().ShouldHaveSingleItem().Generation.ShouldBe( 2 );
    }

    [Test]
    public async Task a_claim_holds_the_delay_window_before_the_save_ends()
    {
        var folder = TestStore.NewDirectory();
        var store = new GenerationStore( folder, _domainName );
        using var saver = NewSaver( store, new PersistentDomainOptions { SnapshotSaveDelay = TimeSpan.FromSeconds( 1 ) } );

        // The first commit claims the save. No save ended yet, but the window is taken.
        saver.TryClaimSave( _commitTime ).ShouldBeTrue();
        saver.TryClaimSave( _commitTime.AddMilliseconds( 10 ) ).ShouldBeFalse();
        saver.TryClaimSave( _commitTime.AddMilliseconds( 999 ) ).ShouldBeFalse();

        // The save of the claim fails: the next commit claims again at once.
        Directory.Delete( folder );
        saver.Offer( new PendingSnapshot( 1, _commitTime, new byte[] { 1 } ) );
        (await saver.SaveAsync( new ActivityMonitor() )).ShouldBeFalse();
        saver.TryClaimSave( _commitTime.AddMilliseconds( 20 ) ).ShouldBeTrue();
        saver.TryClaimSave( _commitTime.AddMilliseconds( 30 ) ).ShouldBeFalse();

        // The window ends one delay after the claim.
        saver.TryClaimSave( _commitTime.AddMilliseconds( 1020 ) ).ShouldBeTrue();
    }

    [Test]
    public void zero_and_negative_delays_do_not_use_claims()
    {
        var store = new GenerationStore( TestStore.NewDirectory(), _domainName );
        using var zero = NewSaver( store, new PersistentDomainOptions { SnapshotSaveDelay = TimeSpan.Zero } );
        zero.TryClaimSave( _commitTime ).ShouldBeTrue();
        zero.TryClaimSave( _commitTime ).ShouldBeTrue();
        using var negative = NewSaver( store, new PersistentDomainOptions { SnapshotSaveDelay = Timeout.InfiniteTimeSpan } );
        negative.TryClaimSave( _commitTime ).ShouldBeFalse();
    }

    [Test]
    public void a_clock_that_steps_backward_does_not_stall_the_claims()
    {
        var store = new GenerationStore( TestStore.NewDirectory(), _domainName );
        using var saver = NewSaver( store, new PersistentDomainOptions { SnapshotSaveDelay = TimeSpan.FromMilliseconds( 200 ) } );
        saver.TryClaimSave( _commitTime ).ShouldBeTrue();
        // The clock goes back 1 hour: the next due time is more than one delay ahead, so the commit is due.
        var back = _commitTime.AddHours( -1 );
        saver.TryClaimSave( back ).ShouldBeTrue();
        // The new window starts at the new clock time.
        saver.TryClaimSave( back.AddMilliseconds( 100 ) ).ShouldBeFalse();
        saver.TryClaimSave( back.AddMilliseconds( 200 ) ).ShouldBeTrue();
    }

    [Test]
    public async Task parallel_commits_in_one_delay_window_give_one_claimed_save_and_a_trailing_save()
    {
        // The commits take about 0.2 s on a developer machine. A window of 3 s keeps them in one window on a slow agent.
        var delay = TimeSpan.FromSeconds( 3 );
        // A large domain: each save takes time, so commits run while a save is in progress.
        using var h = new Harness( new PersistentDomainOptions { SnapshotSaveDelay = delay }, initialObjects: 5000 );

        const int taskCount = 4;
        const int commitsPerTask = 10;
        var start = DateTime.UtcNow;
        var tasks = Enumerable.Range( 0, taskCount ).Select( _ => Task.Run( async () =>
        {
            var m = new ActivityMonitor();
            for( int i = 0; i < commitsPerTask; ++i )
            {
                await h.IncrementAsync( m );
            }
        } ) ).ToArray();
        await Task.WhenAll( tasks );
        var elapsed = DateTime.UtcNow - start;
        TestContext.Out.WriteLine( $"The {taskCount * commitsPerTask} commits took {elapsed.TotalMilliseconds:F0} ms. The delay is {delay.TotalMilliseconds:F0} ms." );
        elapsed.ShouldBeLessThan( delay, "All the commits must run inside one delay window." );

        // Only the first commit claimed a save.
        h.OdFileCount.ShouldBe( 1 );

        // The timer requests the trailing save at the end of the window. It writes the last state.
        (await PollAsync( () => h.OdFileCount == 2, delay + TimeSpan.FromSeconds( 10 ) )).ShouldBeTrue( "The trailing save did not come." );
        var files = h.Store.ListGenerations();
        ReadAndLoad( h.Monitor, files[0].Path ).Count.ShouldBe( taskCount * commitsPerTask );
        h.RequestCount.ShouldBe( 1 );
        h.Errors.Entries.ShouldBeEmpty();
    }

    [Test]
    public async Task a_steady_stream_gives_one_save_per_window_and_the_last_state_is_saved()
    {
        var delay = TimeSpan.FromMilliseconds( 300 );
        using var h = new Harness( new PersistentDomainOptions { SnapshotSaveDelay = delay } );

        // A commit about every 15 ms for about 3 delays.
        int commitCount = 0;
        var start = DateTime.UtcNow;
        while( DateTime.UtcNow - start < TimeSpan.FromMilliseconds( 900 ) )
        {
            await h.IncrementAsync();
            ++commitCount;
            await Task.Delay( 15 );
        }
        var streamDuration = DateTime.UtcNow - start;
        // The windows that the stream touches. A window starts at its claim.
        int windows = (int)(streamDuration.Ticks / delay.Ticks) + 1;

        // After the stream, exactly one trailing save writes the last state.
        (await PollAsync( () => h.Store.ListGenerations() is { Count: > 0 } f && ReadAndLoad( h.Monitor, f[0].Path ).Count == commitCount,
                          TimeSpan.FromSeconds( 3 ) )).ShouldBeTrue( "The last state was not saved." );
        var savesAfterTrailing = h.OdFileCount;
        await Task.Delay( 2 * delay );
        h.OdFileCount.ShouldBe( savesAfterTrailing, "No save comes after the trailing save." );

        TestContext.Out.WriteLine( $"Commits: {commitCount}, duration: {streamDuration.TotalMilliseconds:F0} ms, windows: {windows}, saves: {savesAfterTrailing}, requests: {h.RequestCount}." );
        savesAfterTrailing.ShouldBeLessThanOrEqualTo( windows + 1 );
        h.RequestErrors.ShouldBeEmpty();
        h.Errors.Entries.ShouldBeEmpty();
    }

    [Test]
    public async Task a_commit_skipped_after_a_claim_while_the_timer_waits_is_saved()
    {
        var delay = TimeSpan.FromMilliseconds( 200 );
        var store = new GenerationStore( TestStore.NewDirectory(), _domainName );
        var commits = new FakeCommits();
        using var saver = commits.NewSaver( store, new PersistentDomainOptions { SnapshotSaveDelay = delay } );

        // Commit A claims window 1 and saves. Commit B is skipped: the timer waits for the end of window 1.
        var t0 = DateTime.UtcNow;
        saver.TryClaimSave( t0 ).ShouldBeTrue();
        await commits.DueCommitAsync( saver );
        saver.TryClaimSave( t0.AddMilliseconds( 10 ) ).ShouldBeFalse();
        commits.Commit();
        saver.ScheduleTrailingSave();
        // Commit C claims window 2 before the timer runs (its commit time is after the end of window 1). Its save carries B.
        saver.TryClaimSave( t0 + delay ).ShouldBeTrue();
        await commits.DueCommitAsync( saver );
        // Commit D comes after C in window 2. The timer already waits, so D does not start it again.
        saver.TryClaimSave( t0 + delay + TimeSpan.FromMilliseconds( 10 ) ).ShouldBeFalse();
        int d = commits.Commit();
        saver.ScheduleTrailingSave();

        // The timer runs inside window 2. It must not drop D: it waits for the end of window 2, then saves.
        (await PollAsync( () => saver.SavedTransactionNumber == d, TimeSpan.FromSeconds( 2 ) )).ShouldBeTrue( "D was not saved." );
        (DateTime.UtcNow - t0).ShouldBeGreaterThanOrEqualTo( 2 * delay - TimeSpan.FromMilliseconds( 20 ) );
        await Task.Delay( 2 * delay );
        commits.CaptureCount.ShouldBe( 1 );
    }

    [Test]
    public async Task a_timer_after_a_claim_with_no_later_commit_requests_nothing()
    {
        var delay = TimeSpan.FromMilliseconds( 200 );
        var store = new GenerationStore( TestStore.NewDirectory(), _domainName );
        var commits = new FakeCommits();
        using var saver = commits.NewSaver( store, new PersistentDomainOptions { SnapshotSaveDelay = delay } );

        var t0 = DateTime.UtcNow;
        saver.TryClaimSave( t0 ).ShouldBeTrue();
        await commits.DueCommitAsync( saver );
        saver.TryClaimSave( t0.AddMilliseconds( 10 ) ).ShouldBeFalse();
        commits.Commit();
        saver.ScheduleTrailingSave();
        // Commit C claims window 2. Its save carries the state of B. No commit comes after C.
        saver.TryClaimSave( t0 + delay ).ShouldBeTrue();
        await commits.DueCommitAsync( saver );

        await Task.Delay( 3 * delay );
        commits.CaptureCount.ShouldBe( 0 );
    }

    [Test]
    public async Task stop_waits_for_the_timer_save_in_flight()
    {
        var store = new GenerationStore( TestStore.NewDirectory(), _domainName );
        var entered = new TaskCompletionSource( TaskCreationOptions.RunContinuationsAsynchronously );
        var release = new TaskCompletionSource( TaskCreationOptions.RunContinuationsAsynchronously );
        int requestCount = 0;
        bool requestEnded = false;
        using var saver = new SnapshotSaver( store, new PersistentDomainOptions { SnapshotSaveDelay = TimeSpan.FromMilliseconds( 100 ) }, _ =>
        {
            Interlocked.Increment( ref requestCount );
            entered.TrySetResult();
            release.Task.Wait();
            Volatile.Write( ref requestEnded, true );
            return new PendingSnapshot( 1, DateTime.UtcNow, new byte[] { 1 } );
        }, () => 1 );

        // No save happened: the timer ends at once and the timer save starts.
        saver.ScheduleTrailingSave();
        (await Task.WhenAny( entered.Task, Task.Delay( 2000 ) )).ShouldBe( entered.Task, "The timer save did not start." );

        var stop = saver.StopAsync();
        await Task.Delay( 100 );
        stop.IsCompleted.ShouldBeFalse( "StopAsync must wait for the timer save in flight." );

        release.SetResult();
        (await Task.WhenAny( stop, Task.Delay( 2000 ) )).ShouldBe( stop, "StopAsync did not end." );
        Volatile.Read( ref requestEnded ).ShouldBeTrue();
        saver.SavedTransactionNumber.ShouldBe( 1 );

        // After the stop, no timer save starts.
        saver.ScheduleTrailingSave();
        await Task.Delay( 300 );
        Volatile.Read( ref requestCount ).ShouldBe( 1 );
    }
}
