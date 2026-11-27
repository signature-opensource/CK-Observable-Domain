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
public class DomainLoaderTests
{
    const string _domainName = "Test";

    /// <summary>
    /// Writes one generation per value: each commit sets the counter to the value and the saver writes the file.
    /// </summary>
    static async Task WriteGenerationsAsync( string folder, params int[] counts )
    {
        var monitor = new ActivityMonitor();
        var options = new PersistentDomainOptions();
        var store = new GenerationStore( folder, _domainName );
        var client = new PersistentDomainClient( _domainName, options, null );
        using var domain = client.CreateNew( monitor );
        var saver = new SnapshotSaver( store, options, m => client.CaptureCommittedSnapshot( m, domain ), () => client.LastCommittedTransactionNumber );
        client.Saver = saver;
        foreach( int count in counts )
        {
            await domain.ModifyThrowAsync( monitor, () =>
            {
                var counter = domain.AllObjects.Items.OfType<OCounter>().FirstOrDefault() ?? new OCounter();
                counter.Count = count;
            }, waitForDomainPostActionsCompletion: true );
        }
        await saver.StopAsync();
        saver.Dispose();
    }

    sealed class Loader
    {
        readonly GenerationStore _store;
        readonly PersistentDomainOptions _options;

        public Loader( string folder, int fallbackGenerationCount = 0, bool createOnLoadError = false )
        {
            Monitor = new ActivityMonitor();
            _options = new PersistentDomainOptions { FallbackGenerationCount = fallbackGenerationCount, CreateOnLoadError = createOnLoadError };
            _store = new GenerationStore( folder, _domainName );
        }

        public ActivityMonitor Monitor { get; }

        public GenerationStore Store => _store;

        public int ClientCount { get; private set; }

        public DomainLoadResult Load()
        {
            return DomainLoader.Load( Monitor, _store, _options, () =>
            {
                ++ClientCount;
                return new PersistentDomainClient( _domainName, _options, null );
            } );
        }
    }

    static int CounterOf( DomainLoadResult result )
    {
        result.Domain.ShouldNotBeNull();
        return result.Domain.AllObjects.Items.OfType<OCounter>().Single().Count;
    }

    static string[] OdFiles( string folder ) => Directory.GetFiles( folder, "*.od" ).OrderBy( p => p, StringComparer.Ordinal ).ToArray();

    /// <summary>Gets the name and the content hash of each file, as a comparable map.</summary>
    static Dictionary<string, string> Content( string folder )
        => Directory.GetFiles( folder ).ToDictionary( p => Path.GetFileName( p ), p => Convert.ToHexString( System.Security.Cryptography.SHA256.HashData( File.ReadAllBytes( p ) ) ) );

    static void ZeroFill( string path )
    {
        long length = new FileInfo( path ).Length;
        File.WriteAllBytes( path, new byte[length] );
    }

    static void Truncate( string path )
    {
        var bytes = File.ReadAllBytes( path );
        File.WriteAllBytes( path, bytes.AsSpan( 0, bytes.Length / 2 ).ToArray() );
    }

    static void ChangeOneByte( string path )
    {
        var bytes = File.ReadAllBytes( path );
        bytes[bytes.Length - 1] ^= 0xFF;
        File.WriteAllBytes( path, bytes );
    }

    // A valid file whose payload (version byte 1, then zeros) does not deserialize.
    static GenerationFile WriteUnloadable( string folder )
    {
        var payload = new byte[64];
        payload[0] = 1;
        return new GenerationStore( folder, _domainName ).WriteGeneration( 1, DateTime.UtcNow, payload );
    }

    [Test]
    public void empty_folder_is_Created()
    {
        var folder = TestStore.NewDirectory();
        var loader = new Loader( folder );
        var result = loader.Load();
        using var domain = result.Domain;
        result.Outcome.Kind.ShouldBe( PersistentDomainLoadKind.Created );
        result.Outcome.IsNewDomain.ShouldBeTrue();
        result.Outcome.FolderPath.ShouldBe( folder );
        result.Outcome.LoadedGeneration.ShouldBeNull();
        result.Outcome.LoadedTransactionSerialNumber.ShouldBeNull();
        result.Outcome.SkippedFiles.ShouldBeEmpty();
        result.Outcome.SupersededFiles.ShouldBeEmpty();
        domain.ShouldNotBeNull();
        result.Client.ShouldNotBeNull();
        Directory.GetFiles( folder ).ShouldBeEmpty();
    }

    [Test]
    public async Task valid_newest_is_Loaded_with_its_state()
    {
        var folder = TestStore.NewDirectory();
        await WriteGenerationsAsync( folder, 1, 2, 3 );
        var newest = new GenerationStore( folder, _domainName ).ListGenerations()[0];
        var loader = new Loader( folder );
        var result = loader.Load();
        using var domain = result.Domain;
        result.Outcome.Kind.ShouldBe( PersistentDomainLoadKind.Loaded );
        result.Outcome.IsNewDomain.ShouldBeFalse();
        result.Outcome.LoadedGeneration.ShouldBe( newest.Generation );
        result.Outcome.LoadedTransactionSerialNumber.ShouldNotBeNull();
        result.Outcome.SkippedFiles.ShouldBeEmpty();
        result.Outcome.SupersededFiles.ShouldBeEmpty();
        CounterOf( result ).ShouldBe( 3 );
        domain!.TransactionSerialNumber.ShouldBe( result.Outcome.LoadedTransactionSerialNumber!.Value );
    }

    [Test]
    public async Task damaged_newest_fails_by_default_and_changes_no_file()
    {
        var folder = TestStore.NewDirectory();
        await WriteGenerationsAsync( folder, 1, 2 );
        ZeroFill( OdFiles( folder )[^1] );
        var before = Content( folder );
        var loader = new Loader( folder );

        var first = loader.Load();
        first.Outcome.Kind.ShouldBe( PersistentDomainLoadKind.Failed );
        first.Domain.ShouldBeNull();
        first.Client.ShouldBeNull();
        first.Outcome.SkippedFiles.Count.ShouldBe( 1 );
        first.Outcome.SkippedFiles[0].Result.ShouldBe( SnapshotFileResult.Damaged );
        Content( folder ).ShouldBe( before );

        var second = loader.Load();
        second.Outcome.Kind.ShouldBe( PersistentDomainLoadKind.Failed );
        Content( folder ).ShouldBe( before );
    }

    [Test]
    public async Task damaged_newest_falls_back_when_allowed()
    {
        var folder = TestStore.NewDirectory();
        await WriteGenerationsAsync( folder, 1, 2 );
        var files = new GenerationStore( folder, _domainName ).ListGenerations();
        ZeroFill( files[0].Path );
        var loader = new Loader( folder, fallbackGenerationCount: 1 );
        var result = loader.Load();
        using var domain = result.Domain;
        result.Outcome.Kind.ShouldBe( PersistentDomainLoadKind.LoadedFromFallback );
        result.Outcome.IsNewDomain.ShouldBeFalse();
        result.Outcome.LoadedGeneration.ShouldBe( files[1].Generation );
        CounterOf( result ).ShouldBe( 1 );
        result.Outcome.SkippedFiles.Count.ShouldBe( 1 );
        var skipped = result.Outcome.SkippedFiles[0];
        skipped.Result.ShouldBe( SnapshotFileResult.Damaged );
        skipped.Path.ShouldBe( files[0].Path );
        skipped.Generation.ShouldBe( files[0].Generation );
        result.Outcome.SupersededFiles.ShouldBeEmpty();
    }

    [TestCase( "truncated" )]
    [TestCase( "changed byte" )]
    public async Task truncated_and_changed_byte_newest_are_damaged( string damage )
    {
        var folder = TestStore.NewDirectory();
        await WriteGenerationsAsync( folder, 1, 2 );
        var files = new GenerationStore( folder, _domainName ).ListGenerations();
        if( damage == "truncated" ) Truncate( files[0].Path ); else ChangeOneByte( files[0].Path );
        var loader = new Loader( folder, fallbackGenerationCount: 1 );
        var result = loader.Load();
        using var domain = result.Domain;
        result.Outcome.Kind.ShouldBe( PersistentDomainLoadKind.LoadedFromFallback );
        CounterOf( result ).ShouldBe( 1 );
        result.Outcome.SkippedFiles.Count.ShouldBe( 1 );
        result.Outcome.SkippedFiles[0].Result.ShouldBe( SnapshotFileResult.Damaged );
    }

    [Test]
    public async Task unloadable_newest_stops_the_fallback()
    {
        var folder = TestStore.NewDirectory();
        await WriteGenerationsAsync( folder, 1 );
        // The unloadable file is written last, so it is the newest.
        WriteUnloadable( folder );
        var files = new GenerationStore( folder, _domainName ).ListGenerations();
        files.Count.ShouldBe( 2 );
        var before = Content( folder );
        var loader = new Loader( folder, fallbackGenerationCount: 3 );
        var result = loader.Load();
        result.Outcome.Kind.ShouldBe( PersistentDomainLoadKind.Failed );
        result.Domain.ShouldBeNull();
        result.Outcome.SkippedFiles.Count.ShouldBe( 1 );
        var skipped = result.Outcome.SkippedFiles[0];
        skipped.Result.ShouldBe( SnapshotFileResult.Unloadable );
        skipped.Error.ShouldNotBeNull();
        skipped.Path.ShouldBe( files[0].Path );
        // One client for the unloadable file only: the older valid file was not opened.
        loader.ClientCount.ShouldBe( 1 );
        Content( folder ).ShouldBe( before );
    }

    [Test]
    public async Task a_newer_format_version_is_unloadable_and_stops_the_fallback()
    {
        // After a package downgrade, the newest file has a format version that this code does not know.
        var folder = TestStore.NewDirectory();
        await WriteGenerationsAsync( folder, 1, 2 );
        var files = new GenerationStore( folder, _domainName ).ListGenerations();
        var bytes = File.ReadAllBytes( files[0].Path );
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian( bytes.AsSpan( 4, 2 ), 2 );
        // A newer version keeps the header checksum at the same place over the same prefix.
        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian( bytes.AsSpan( 44, 8 ), System.IO.Hashing.XxHash64.HashToUInt64( bytes.AsSpan( 0, 44 ) ) );
        File.WriteAllBytes( files[0].Path, bytes );
        var before = Content( folder );

        var loader = new Loader( folder, fallbackGenerationCount: 1 );
        var result = loader.Load();

        result.Outcome.Kind.ShouldBe( PersistentDomainLoadKind.Failed );
        var skipped = result.Outcome.SkippedFiles.ShouldHaveSingleItem();
        skipped.Result.ShouldBe( SnapshotFileResult.Unloadable );
        skipped.Error.ShouldNotBeNull();
        skipped.Path.ShouldBe( files[0].Path );
        // The older file was not tried.
        loader.ClientCount.ShouldBe( 0 );
        Content( folder ).ShouldBe( before );
    }

    [Test]
    public async Task a_bit_flip_in_the_version_field_is_damage_and_the_fallback_continues()
    {
        var folder = TestStore.NewDirectory();
        await WriteGenerationsAsync( folder, 1, 2 );
        var files = new GenerationStore( folder, _domainName ).ListGenerations();
        var bytes = File.ReadAllBytes( files[0].Path );
        // The version field becomes 3. The header checksum does not match.
        bytes[4] ^= 0x02;
        File.WriteAllBytes( files[0].Path, bytes );

        var loader = new Loader( folder, fallbackGenerationCount: 1 );
        var result = loader.Load();
        using var domain = result.Domain;

        result.Outcome.Kind.ShouldBe( PersistentDomainLoadKind.LoadedFromFallback );
        CounterOf( result ).ShouldBe( 1 );
        var skipped = result.Outcome.SkippedFiles.ShouldHaveSingleItem();
        skipped.Result.ShouldBe( SnapshotFileResult.Damaged );
        skipped.Path.ShouldBe( files[0].Path );
    }

    [Test]
    public void the_outcome_copies_its_lists()
    {
        var skipped = new List<SkippedSnapshotFile> { new SkippedSnapshotFile( "a", 2, SnapshotFileResult.Damaged, null ) };
        var superseded = new List<GenerationFile> { new GenerationFile( 1, "b" ) };
        var outcome = new PersistentDomainLoadOutcome( PersistentDomainLoadKind.CreatedAfterLoadFailure, "f", null, null, skipped, superseded );

        skipped.Clear();
        superseded.Clear();

        outcome.SkippedFiles.Count.ShouldBe( 1 );
        outcome.SupersededFiles.ShouldBe( new[] { "b" } );
        outcome.SupersededGenerations.Count.ShouldBe( 1 );
    }

    [Test]
    public async Task all_damaged_with_CreateOnLoadError_is_CreatedAfterLoadFailure()
    {
        var folder = TestStore.NewDirectory();
        await WriteGenerationsAsync( folder, 1, 2 );
        foreach( var file in OdFiles( folder ) ) ZeroFill( file );
        var loader = new Loader( folder, fallbackGenerationCount: 1, createOnLoadError: true );
        var result = loader.Load();
        using var domain = result.Domain;
        result.Outcome.Kind.ShouldBe( PersistentDomainLoadKind.CreatedAfterLoadFailure );
        result.Outcome.IsNewDomain.ShouldBeTrue();
        result.Outcome.LoadedGeneration.ShouldBeNull();
        result.Outcome.LoadedTransactionSerialNumber.ShouldBeNull();
        result.Outcome.SkippedFiles.Count.ShouldBe( 2 );
        result.Outcome.SkippedFiles.ShouldAllBe( s => s.Result == SnapshotFileResult.Damaged );
        result.Outcome.SupersededFiles.ShouldBeEmpty();
        domain.ShouldNotBeNull();
        result.Client.ShouldNotBeNull();
    }

    [Test]
    public async Task a_reset_after_damage_logs_an_error_that_names_the_folder()
    {
        var folder = TestStore.NewDirectory();
        await WriteGenerationsAsync( folder, 1, 2 );
        foreach( var file in OdFiles( folder ) ) ZeroFill( file );
        var loader = new Loader( folder, fallbackGenerationCount: 1, createOnLoadError: true );
        using var errors = loader.Monitor.CollectEntries( out var entries, LogLevelFilter.Error );

        var result = loader.Load();
        using var domain = result.Domain;

        result.Outcome.Kind.ShouldBe( PersistentDomainLoadKind.CreatedAfterLoadFailure );
        var error = entries.ShouldHaveSingleItem();
        error.MaskedLevel.ShouldBe( LogLevel.Error );
        error.Text.ShouldContain( "CreatedAfterLoadFailure" );
        error.Text.ShouldContain( folder );
    }

    [Test]
    public async Task the_outcome_line_names_the_folder()
    {
        var empty = TestStore.NewDirectory();
        var loader = new Loader( empty );
        using( loader.Monitor.CollectEntries( out var created, LogLevelFilter.Info ) )
        {
            using var domain = loader.Load().Domain;
            created.ShouldContain( e => e.Text.Contains( "Created" ) && e.Text.Contains( empty ) );
        }

        var folder = TestStore.NewDirectory();
        await WriteGenerationsAsync( folder, 1 );
        loader = new Loader( folder );
        using( loader.Monitor.CollectEntries( out var loaded, LogLevelFilter.Info ) )
        {
            using var domain = loader.Load().Domain;
            loaded.ShouldContain( e => e.Text.Contains( "Loaded" ) && e.Text.Contains( folder ) );
        }
    }

    [Test]
    public async Task untried_valid_files_are_superseded()
    {
        var folder = TestStore.NewDirectory();
        await WriteGenerationsAsync( folder, 1, 2, 3, 4 );
        var files = new GenerationStore( folder, _domainName ).ListGenerations();
        ZeroFill( files[0].Path );
        ZeroFill( files[1].Path );
        var loader = new Loader( folder, fallbackGenerationCount: 1, createOnLoadError: true );
        var result = loader.Load();
        using var domain = result.Domain;
        result.Outcome.Kind.ShouldBe( PersistentDomainLoadKind.CreatedAfterLoadFailure );
        result.Outcome.SkippedFiles.Count.ShouldBe( 2 );
        result.Outcome.SupersededFiles.ShouldBe( new[] { files[2].Path, files[3].Path } );
    }

    [Test]
    public async Task locked_newest_is_a_read_error_and_fails_whatever_the_options()
    {
        var folder = TestStore.NewDirectory();
        await WriteGenerationsAsync( folder, 1, 2 );
        var files = new GenerationStore( folder, _domainName ).ListGenerations();
        var before = Content( folder );
        var loader = new Loader( folder, fallbackGenerationCount: 3, createOnLoadError: true );
        DomainLoadResult result;
        using( new FileStream( files[0].Path, FileMode.Open, FileAccess.Read, FileShare.None ) )
        {
            result = loader.Load();
        }
        result.Outcome.Kind.ShouldBe( PersistentDomainLoadKind.Failed );
        result.Domain.ShouldBeNull();
        result.Outcome.SkippedFiles.Count.ShouldBe( 1 );
        var skipped = result.Outcome.SkippedFiles[0];
        skipped.Result.ShouldBe( SnapshotFileResult.ReadError );
        skipped.Error.ShouldNotBeNull();
        skipped.Path.ShouldBe( files[0].Path );
        loader.ClientCount.ShouldBe( 0 );
        Content( folder ).ShouldBe( before );
    }

    // Windows only: the retry handles the Windows codes of a sharing violation and an access denied error.
    // On Unix, a file locked by the FileShare emulation of .NET gives another code.
    [Test]
    [Platform( "Win" )]
    public async Task a_newest_file_locked_for_a_short_time_loads_after_a_retry()
    {
        var folder = TestStore.NewDirectory();
        await WriteGenerationsAsync( folder, 1, 2 );
        var files = new GenerationStore( folder, _domainName ).ListGenerations();
        var loader = new Loader( folder );
        // An antivirus scans the newest file at boot and closes it after about 40 ms.
        // The retries wait 20, 40, 80 and 160 ms: a release at 40 ms leaves a large margin before the last try.
        var handle = new FileStream( files[0].Path, FileMode.Open, FileAccess.Read, FileShare.None );
        var release = Task.Delay( 40 ).ContinueWith( _ => handle.Dispose() );

        var result = loader.Load();
        await release;
        using var domain = result.Domain;

        result.Outcome.Kind.ShouldBe( PersistentDomainLoadKind.Loaded );
        result.Outcome.SkippedFiles.ShouldBeEmpty();
        CounterOf( result ).ShouldBe( 2 );
    }

    [Test]
    public async Task apply_marks_renames_skipped_and_superseded_files()
    {
        var folder = TestStore.NewDirectory();
        await WriteGenerationsAsync( folder, 1, 2, 3, 4 );
        var files = new GenerationStore( folder, _domainName ).ListGenerations();
        ZeroFill( files[0].Path );
        ZeroFill( files[1].Path );
        var loader = new Loader( folder, fallbackGenerationCount: 1, createOnLoadError: true );
        var result = loader.Load();
        using var domain = result.Domain;
        Directory.GetFiles( folder, "*.od" ).Length.ShouldBe( 4 );

        loader.Store.ApplyMarks( loader.Monitor, DomainLoader.GetMarks( result.Outcome ), DateTime.UtcNow ).ShouldBeEmpty();

        Directory.GetFiles( folder, "*.od" ).ShouldBeEmpty();
        Directory.GetFiles( folder, "*.damaged" ).Length.ShouldBe( 2 );
        Directory.GetFiles( folder, "*.superseded" ).Length.ShouldBe( 2 );
        File.Exists( files[0].Path + ".damaged" ).ShouldBeTrue();
        File.Exists( files[3].Path + ".superseded" ).ShouldBeTrue();
        new GenerationStore( folder, _domainName ).NextGeneration.ShouldBeGreaterThan( files.Max( f => f.Generation ) );
    }

    [Test]
    public async Task apply_marks_renames_an_unloadable_file()
    {
        var folder = TestStore.NewDirectory();
        await WriteGenerationsAsync( folder, 1 );
        var unloadable = WriteUnloadable( folder );
        var loader = new Loader( folder, createOnLoadError: true );
        var result = loader.Load();
        using var domain = result.Domain;
        result.Outcome.Kind.ShouldBe( PersistentDomainLoadKind.CreatedAfterLoadFailure );
        result.Outcome.SkippedFiles.Count.ShouldBe( 1 );
        result.Outcome.SkippedFiles[0].Result.ShouldBe( SnapshotFileResult.Unloadable );
        result.Outcome.SupersededFiles.Count.ShouldBe( 1 );

        loader.Store.ApplyMarks( loader.Monitor, DomainLoader.GetMarks( result.Outcome ), DateTime.UtcNow ).ShouldBeEmpty();

        File.Exists( unloadable.Path + ".unloadable" ).ShouldBeTrue();
        Directory.GetFiles( folder, "*.superseded" ).Length.ShouldBe( 1 );
        Directory.GetFiles( folder, "*.od" ).ShouldBeEmpty();
    }
}
