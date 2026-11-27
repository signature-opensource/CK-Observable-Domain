using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CK.Core;
using NUnit.Framework;
using Shouldly;

namespace CK.Observable.PersistentDomainDriverTests;

[TestFixture]
public class GenerationStoreTests
{
    static readonly DateTime _commitTime = new DateTime( 2026, 10, 27, 8, 30, 15, DateTimeKind.Utc );
    static readonly DateTime _t = new DateTime( 2026, 11, 1, 0, 0, 0, DateTimeKind.Utc );

    static string[] FileNames( string folder, string pattern = "*" )
        => Directory.GetFiles( folder, pattern ).Select( Path.GetFileName ).OrderBy( n => n, StringComparer.Ordinal ).ToArray()!;

    static void WriteMany( GenerationStore store, int count )
    {
        for( int i = 0; i < count; ++i )
        {
            store.WriteGeneration( i, _commitTime, new byte[] { (byte)i } );
        }
    }

    static IActivityMonitor NewMonitor() => new ActivityMonitor();

    [Test]
    public void empty_folder_starts_at_generation_1()
    {
        var store = new GenerationStore( TestStore.NewDirectory(), "D" );
        store.NextGeneration.ShouldBe( 1 );
        store.ListGenerations().ShouldBeEmpty();
    }

    [Test]
    public void write_creates_one_valid_file_and_increments_the_generation()
    {
        var folder = TestStore.NewDirectory();
        var store = new GenerationStore( folder, "D" );
        var written = store.WriteGeneration( 3, _commitTime, new byte[] { 9 } );

        written.Generation.ShouldBe( 1 );
        FileNames( folder ).ShouldBe( new[] { "D.0000000000000000001.od" } );
        Path.GetFileName( written.Path ).ShouldBe( "D.0000000000000000001.od" );
        using( var input = File.OpenRead( written.Path ) )
        {
            SnapshotFileFormat.Read( input, out var header, out var payload ).ShouldBe( SnapshotReadStatus.Valid );
            payload.ShouldBe( new byte[] { 9 } );
            header.Generation.ShouldBe( 1 );
            header.TransactionSerialNumber.ShouldBe( 3 );
            header.CommitTimeUtc.ShouldBe( _commitTime );
        }
        store.NextGeneration.ShouldBe( 2 );
        FileNames( folder, "*.tmp" ).ShouldBeEmpty();
    }

    [Test]
    public void next_generation_counts_marked_files()
    {
        var folder = TestStore.NewDirectory();
        File.WriteAllBytes( Path.Combine( folder, "D.0000000000000000004.od.damaged" ), Array.Empty<byte>() );
        File.WriteAllBytes( Path.Combine( folder, "D.0000000000000000002.od" ), Array.Empty<byte>() );
        var store = new GenerationStore( folder, "D" );
        store.NextGeneration.ShouldBe( 5 );
        store.ListGenerations().Select( g => g.Generation ).ShouldBe( new long[] { 2 } );
    }

    [Test]
    public void names_parse_whatever_the_case()
    {
        // An operator can rename a file with another case. NTFS ignores the case of a name.
        var folder = TestStore.NewDirectory();
        File.WriteAllBytes( Path.Combine( folder, "D.0000000000000000002.OD" ), Array.Empty<byte>() );
        File.WriteAllBytes( Path.Combine( folder, "d.0000000000000000003.Od.Superseded" ), Array.Empty<byte>() );
        File.WriteAllBytes( Path.Combine( folder, "D.0000000000000000004.od.DAMAGED" ), Array.Empty<byte>() );
        var store = new GenerationStore( folder, "D" );

        store.NextGeneration.ShouldBe( 5 );
        store.ListGenerations().Select( g => g.Generation ).ShouldBe( new long[] { 2 } );
        store.WriteGeneration( 0, _commitTime, new byte[] { 1 } ).Generation.ShouldBe( 5 );

        // The retention sees the marked files with their mark: the old damaged file goes, the newest .od files stay.
        store.ApplyRetention( NewMonitor(), 5, TimeSpan.FromDays( 7 ), DateTime.UtcNow.AddDays( 30 ) );
        FileNames( folder ).ShouldBe( new[] { "D.0000000000000000002.OD", "D.0000000000000000005.od" } );
    }

    [Test]
    public void an_occupied_name_moves_the_write_to_the_next_generation()
    {
        var folder = TestStore.NewDirectory();
        var store = new GenerationStore( folder, "D" );
        // Files copied in after the store read the folder.
        File.WriteAllBytes( Path.Combine( folder, "D.0000000000000000001.od" ), new byte[] { 42 } );
        File.WriteAllBytes( Path.Combine( folder, "D.0000000000000000002.od" ), new byte[] { 43 } );

        var written = store.WriteGeneration( 7, _commitTime, new byte[] { 9 } );

        written.Generation.ShouldBe( 3 );
        Path.GetFileName( written.Path ).ShouldBe( "D.0000000000000000003.od" );
        using( var input = File.OpenRead( written.Path ) )
        {
            SnapshotFileFormat.Read( input, out var header, out _ ).ShouldBe( SnapshotReadStatus.Valid );
            header.Generation.ShouldBe( 3 );
        }
        store.NextGeneration.ShouldBe( 4 );
        File.ReadAllBytes( Path.Combine( folder, "D.0000000000000000001.od" ) ).ShouldBe( new byte[] { 42 } );
        File.ReadAllBytes( Path.Combine( folder, "D.0000000000000000002.od" ) ).ShouldBe( new byte[] { 43 } );
        FileNames( folder, "*.tmp" ).ShouldBeEmpty();
    }

    // Windows only: NTFS ignores the case, so a name with another case occupies the name of the next generation.
    // A case-sensitive file system keeps both names, and the write uses the lower-case name.
    [Test]
    [Platform( "Win" )]
    public void a_name_in_use_with_another_case_moves_the_write_to_the_next_generation()
    {
        var folder = TestStore.NewDirectory();
        var store = new GenerationStore( folder, "D" );
        File.WriteAllBytes( Path.Combine( folder, "D.0000000000000000001.OD" ), new byte[] { 43 } );

        var written = store.WriteGeneration( 7, _commitTime, new byte[] { 9 } );

        written.Generation.ShouldBe( 2 );
        Path.GetFileName( written.Path ).ShouldBe( "D.0000000000000000002.od" );
        File.ReadAllBytes( Path.Combine( folder, "D.0000000000000000001.OD" ) ).ShouldBe( new byte[] { 43 } );
    }

    [Test]
    public void the_move_past_occupied_names_is_bounded()
    {
        var folder = TestStore.NewDirectory();
        var store = new GenerationStore( folder, "D" );
        for( int g = 1; g <= 101; ++g )
        {
            File.WriteAllBytes( Path.Combine( folder, $"D.{g:D19}.od" ), Array.Empty<byte>() );
        }

        Should.Throw<IOException>( () => store.WriteGeneration( 0, _commitTime, new byte[] { 1 } ) );

        store.NextGeneration.ShouldBe( 1 );
        FileNames( folder, "*.tmp" ).ShouldBeEmpty();
        FileNames( folder ).Length.ShouldBe( 101 );
    }

    [Test]
    public void list_is_newest_first()
    {
        var store = new GenerationStore( TestStore.NewDirectory(), "D" );
        WriteMany( store, 3 );
        store.ListGenerations().Select( g => g.Generation ).ShouldBe( new long[] { 3, 2, 1 } );
    }

    [Test]
    public void domain_name_with_dots_and_plus_parses()
    {
        var folder = TestStore.NewDirectory();
        var store = new GenerationStore( folder, "A.B+C" );
        WriteMany( store, 2 );
        var reopened = new GenerationStore( folder, "A.B+C" );
        reopened.NextGeneration.ShouldBe( 3 );
        reopened.ListGenerations().Count.ShouldBe( 2 );
        GenerationStore.GetLockFilePath( folder, "A.B+C" ).ShouldBe( Path.Combine( folder, "A.B+C.lock" ) );
    }

    [Test]
    public void foreign_files_are_ignored_and_never_deleted()
    {
        var folder = TestStore.NewDirectory();
        var foreign = new[] { "README.txt", "Other.0000000000000000001.od", "D.notanumber.od" };
        foreach( var n in foreign ) File.WriteAllBytes( Path.Combine( folder, n ), Array.Empty<byte>() );
        var store = new GenerationStore( folder, "D" );
        WriteMany( store, 7 );
        store.ApplyRetention( NewMonitor(), 5, TimeSpan.FromDays( 7 ), _t );

        foreach( var n in foreign ) File.Exists( Path.Combine( folder, n ) ).ShouldBeTrue( n );
        store.ListGenerations().Count.ShouldBe( 5 );
    }

    [Test]
    public void orphan_temp_files_are_deleted()
    {
        var folder = TestStore.NewDirectory();
        var a = Path.Combine( folder, $"D.{Guid.NewGuid():N}.tmp" );
        var b = Path.Combine( folder, $"D.{Guid.NewGuid():N}.tmp" );
        var other = Path.Combine( folder, $"Other.{Guid.NewGuid():N}.tmp" );
        foreach( var p in new[] { a, b, other } ) File.WriteAllBytes( p, Array.Empty<byte>() );
        var store = new GenerationStore( folder, "D" );

        store.DeleteOrphanTempFiles( NewMonitor() ).ShouldBe( 2 );
        File.Exists( a ).ShouldBeFalse();
        File.Exists( b ).ShouldBeFalse();
        File.Exists( other ).ShouldBeTrue();
    }

    [Test]
    public void failed_rename_leaves_no_od_and_no_tmp()
    {
        var folder = TestStore.NewDirectory();
        var store = new GenerationStore( folder, "D" );
        var blocker = Path.Combine( folder, "D.0000000000000000001.od" );

        // The folder appears after the check of the name and before the rename.
        Should.Throw<IOException>( () => store.WriteGeneration( 0, _commitTime, new byte[] { 1 }, () => Directory.CreateDirectory( blocker ) ) );
        FileNames( folder, "*.tmp" ).ShouldBeEmpty();
        store.NextGeneration.ShouldBe( 1 );

        Directory.Delete( blocker );
        var written = store.WriteGeneration( 0, _commitTime, new byte[] { 1 } );
        written.Generation.ShouldBe( 1 );
        File.Exists( blocker ).ShouldBeTrue();
        store.NextGeneration.ShouldBe( 2 );
    }

    // Windows only: on Unix, rename(2) ignores the FileShare emulation of .NET, so the handle blocks no rename.
    [Test]
    [Platform( "Win" )]
    public void a_rename_blocked_for_a_short_time_succeeds_after_a_retry()
    {
        var folder = TestStore.NewDirectory();
        var store = new GenerationStore( folder, "D" );
        Task? release = null;

        // An antivirus opens the temporary file without FileShare.Delete and closes it after about 40 ms.
        // The retries wait 20, 40, 80 and 160 ms: a release at 40 ms leaves a large margin before the last try.
        var written = store.WriteGeneration( 0, _commitTime, new byte[] { 1 }, () =>
        {
            var temp = Path.Combine( folder, FileNames( folder, "*.tmp" ).ShouldHaveSingleItem() );
            var handle = new FileStream( temp, FileMode.Open, FileAccess.Read, FileShare.Read );
            release = Task.Delay( 40 ).ContinueWith( _ => handle.Dispose() );
        } );
        release.ShouldNotBeNull().Wait();

        written.Generation.ShouldBe( 1 );
        FileNames( folder ).ShouldBe( new[] { "D.0000000000000000001.od" } );
        store.NextGeneration.ShouldBe( 2 );
    }

    // Windows only: see a_rename_blocked_for_a_short_time_succeeds_after_a_retry.
    [Test]
    [Platform( "Win" )]
    public void a_rename_blocked_longer_than_the_retries_fails()
    {
        var folder = TestStore.NewDirectory();
        var store = new GenerationStore( folder, "D" );
        FileStream? handle = null;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            Should.Throw<IOException>( () => store.WriteGeneration( 0, _commitTime, new byte[] { 1 }, () =>
            {
                var temp = Path.Combine( folder, FileNames( folder, "*.tmp" ).ShouldHaveSingleItem() );
                handle = new FileStream( temp, FileMode.Open, FileAccess.Read, FileShare.Read );
            } ) );
            // The retries wait 20, 40, 80 and 160 ms.
            watch.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo( 290 );
        }
        finally
        {
            handle?.Dispose();
        }
        store.NextGeneration.ShouldBe( 1 );
        FileNames( folder, "*.od" ).ShouldBeEmpty();
        // The handle also blocked the delete of the temporary file. The next start deletes it.
        store.DeleteOrphanTempFiles( NewMonitor() );
        FileNames( folder ).ShouldBeEmpty();
    }

    [Test]
    public void retention_keeps_the_newest_count_and_never_deletes_unloadable()
    {
        var folder = TestStore.NewDirectory();
        var store = new GenerationStore( folder, "D" );
        WriteMany( store, 7 );
        var oldest = store.ListGenerations().Single( g => g.Generation == 1 );
        store.TryMark( NewMonitor(), oldest, GenerationMark.Unloadable, _t ).ShouldBeTrue();

        store.ApplyRetention( NewMonitor(), 5, TimeSpan.FromDays( 7 ), _t.AddDays( 365 ) );

        store.ListGenerations().Select( g => g.Generation ).ShouldBe( new long[] { 7, 6, 5, 4, 3 } );
        File.Exists( Path.Combine( folder, "D.0000000000000000001.od.unloadable" ) ).ShouldBeTrue();
    }

    [Test]
    public void retention_deletes_old_marks_only_after_the_delay()
    {
        var folder = TestStore.NewDirectory();
        var store = new GenerationStore( folder, "D" );
        WriteMany( store, 2 );
        var list = store.ListGenerations();
        store.TryMark( NewMonitor(), list[0], GenerationMark.Damaged, _t ).ShouldBeTrue();
        store.TryMark( NewMonitor(), list[1], GenerationMark.Superseded, _t ).ShouldBeTrue();
        FileNames( folder ).ShouldBe( new[] { "D.0000000000000000001.od.superseded", "D.0000000000000000002.od.damaged" } );

        store.ApplyRetention( NewMonitor(), 5, TimeSpan.FromDays( 7 ), _t.AddDays( 6 ) );
        FileNames( folder ).Length.ShouldBe( 2 );

        store.ApplyRetention( NewMonitor(), 5, TimeSpan.FromDays( 7 ), _t.AddDays( 8 ) );
        FileNames( folder ).ShouldBeEmpty();
    }

    [Test]
    public void before_rename_runs_after_the_flush_and_before_the_rename()
    {
        var folder = TestStore.NewDirectory();
        var store = new GenerationStore( folder, "D" );
        bool called = false;

        store.WriteGeneration( 0, _commitTime, new byte[] { 1, 2 }, () =>
        {
            called = true;
            // The temporary file is complete and closed. The generation file does not exist yet.
            var temp = FileNames( folder, "*.tmp" ).ShouldHaveSingleItem();
            new FileInfo( Path.Combine( folder, temp ) ).Length.ShouldBe( SnapshotFileFormat.HeaderLength + 2 );
            File.Exists( Path.Combine( folder, "D.0000000000000000001.od" ) ).ShouldBeFalse();
        } );

        called.ShouldBeTrue();
        FileNames( folder ).ShouldBe( new[] { "D.0000000000000000001.od" } );
    }

    [Test]
    public void a_throwing_before_rename_leaves_no_file()
    {
        var folder = TestStore.NewDirectory();
        var store = new GenerationStore( folder, "D" );

        Should.Throw<InvalidOperationException>( () => store.WriteGeneration( 0, _commitTime, new byte[] { 1 }, () => throw new InvalidOperationException() ) );

        FileNames( folder ).ShouldBeEmpty();
        store.NextGeneration.ShouldBe( 1 );
    }

    // Windows only: on Unix, rename(2) ignores the FileShare emulation of .NET (an advisory flock),
    // so the handle blocks no rename and the production code has nothing to handle.
    [Test]
    [Platform( "Win" )]
    public void a_blocked_rename_returns_false_and_leaves_the_od_file()
    {
        var folder = TestStore.NewDirectory();
        var store = new GenerationStore( folder, "D" );
        WriteMany( store, 1 );
        var file = store.ListGenerations()[0];

        // An antivirus that holds the file without FileShare.Delete blocks the rename.
        using( new FileStream( file.Path, FileMode.Open, FileAccess.Read, FileShare.Read ) )
        {
            store.TryMark( NewMonitor(), file, GenerationMark.Superseded, _t ).ShouldBeFalse();
        }
        FileNames( folder ).ShouldBe( new[] { "D.0000000000000000001.od" } );

        // The next try renames the file.
        store.TryMark( NewMonitor(), file, GenerationMark.Superseded, _t ).ShouldBeTrue();
        FileNames( folder ).ShouldBe( new[] { "D.0000000000000000001.od.superseded" } );
    }

    [Test]
    public void each_mark_logs_one_line_with_the_file_and_the_mark()
    {
        var folder = TestStore.NewDirectory();
        var store = new GenerationStore( folder, "D" );
        WriteMany( store, 2 );
        var list = store.ListGenerations();
        var monitor = new ActivityMonitor();
        using var infos = monitor.CollectEntries( out var entries, LogLevelFilter.Info );

        store.ApplyMarks( monitor, new[] { new GenerationFileMark( list[0], GenerationMark.Damaged ),
                                           new GenerationFileMark( list[1], GenerationMark.Superseded ) }, _t ).ShouldBeEmpty();

        entries.Count.ShouldBe( 2 );
        entries[0].Text.ShouldContain( list[0].Path );
        entries[0].Text.ShouldContain( "Damaged" );
        entries[1].Text.ShouldContain( list[1].Path );
        entries[1].Text.ShouldContain( "Superseded" );
    }

    [Test]
    public void a_failed_time_set_is_retried_once()
    {
        var folder = TestStore.NewDirectory();
        var store = new GenerationStore( folder, "D" );
        WriteMany( store, 1 );
        int calls = 0;
        store.SetLastWriteTimeUtc = ( path, time ) =>
        {
            if( ++calls == 1 ) throw new IOException( "The first time set fails." );
            File.SetLastWriteTimeUtc( path, time );
        };
        var monitor = new ActivityMonitor();
        using var errors = monitor.CollectEntries( out var entries, LogLevelFilter.Error );

        store.TryMark( monitor, store.ListGenerations()[0], GenerationMark.Damaged, _t ).ShouldBeTrue();

        calls.ShouldBe( 2 );
        entries.ShouldBeEmpty();
        File.GetLastWriteTimeUtc( Path.Combine( folder, "D.0000000000000000001.od.damaged" ) ).ShouldBe( _t );
    }

    [Test]
    public void a_time_set_that_fails_twice_logs_an_error_and_a_later_try_sets_the_time()
    {
        var folder = TestStore.NewDirectory();
        var store = new GenerationStore( folder, "D" );
        WriteMany( store, 1 );
        var file = store.ListGenerations()[0];
        var marked = file.Path + ".superseded";
        bool fails = true;
        store.SetLastWriteTimeUtc = ( path, time ) =>
        {
            if( fails ) throw new IOException( "The time set fails." );
            File.SetLastWriteTimeUtc( path, time );
        };
        var monitor = new ActivityMonitor();
        using var errors = monitor.CollectEntries( out var entries, LogLevelFilter.Error );

        // The rename succeeds, but the file keeps its old time: it is not safely marked.
        store.TryMark( monitor, file, GenerationMark.Superseded, _t ).ShouldBeFalse();
        File.Exists( marked ).ShouldBeTrue();
        File.GetLastWriteTimeUtc( marked ).ShouldNotBe( _t );
        entries.ShouldContain( e => e.MaskedLevel == LogLevel.Error );

        // A later try finds the renamed file and sets only its time.
        fails = false;
        store.TryMark( monitor, file, GenerationMark.Superseded, _t ).ShouldBeTrue();
        File.GetLastWriteTimeUtc( marked ).ShouldBe( _t );
        FileNames( folder ).ShouldBe( new[] { "D.0000000000000000001.od.superseded" } );
    }

    [Test]
    public void retention_never_deletes_a_protected_generation()
    {
        var folder = TestStore.NewDirectory();
        var store = new GenerationStore( folder, "D" );
        WriteMany( store, 8 );
        var list = store.ListGenerations();
        // Generation 8 has an old mark time: its time set failed.
        store.TryMark( NewMonitor(), list.Single( g => g.Generation == 8 ), GenerationMark.Superseded, _t.AddDays( -30 ) ).ShouldBeTrue();

        store.ApplyRetention( NewMonitor(), 3, TimeSpan.FromDays( 7 ), _t, new HashSet<long> { 1, 2, 8 } );

        // The protected generations stay and do not count in the 3 kept generations.
        store.ListGenerations().Select( g => g.Generation ).ShouldBe( new long[] { 7, 6, 5, 2, 1 } );
        File.Exists( Path.Combine( folder, "D.0000000000000000008.od.superseded" ) ).ShouldBeTrue();
    }

    [Test]
    public void retention_keeps_a_mark_dated_in_the_future()
    {
        var folder = TestStore.NewDirectory();
        var store = new GenerationStore( folder, "D" );
        WriteMany( store, 1 );
        store.TryMark( NewMonitor(), store.ListGenerations()[0], GenerationMark.Damaged, _t.AddDays( 30 ) ).ShouldBeTrue();

        store.ApplyRetention( NewMonitor(), 5, TimeSpan.FromDays( 7 ), _t );
        FileNames( folder ).ShouldBe( new[] { "D.0000000000000000001.od.damaged" } );
    }
}
