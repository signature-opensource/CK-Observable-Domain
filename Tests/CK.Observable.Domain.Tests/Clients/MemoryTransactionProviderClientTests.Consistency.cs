using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CK.Core;
using NUnit.Framework;
using Shouldly;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Domain.Tests.Clients;

public partial class MemoryTransactionProviderClientTests
{
    static string RandomString( int length )
    {
        // Random bytes in Base64: this does not compress well.
        var bytes = new byte[length * 3 / 4];
        Random.Shared.NextBytes( bytes );
        return Convert.ToBase64String( bytes );
    }

    static Task<TransactionResult> FailingTransactionAsync( ObservableDomain<TestObservableRootObject> d, IActivityMonitor? monitor = null )
    {
        return d.TryModifyAsync( monitor ?? TestHelper.Monitor, () =>
        {
            d.Root.Prop1 = "Never committed.";
            throw new Exception( "Failing transaction. This is a test exception." );
        }, considerRolledbackAsFailure: false );
    }

    [TestCase( CompressionKind.None )]
    [TestCase( CompressionKind.GZiped )]
    public async Task WriteSnapshot_after_a_rollback_writes_the_exact_snapshot_Async( CompressionKind kind )
    {
        var name = nameof( WriteSnapshot_after_a_rollback_writes_the_exact_snapshot_Async );
        var client = new SnapshotTestClient( name ) { CompressionKind = kind };
        using var d = new ObservableDomain<TestObservableRootObject>( TestHelper.Monitor, name, startTimer: false, client );
        // A large snapshot, then a smaller one: the memory keeps stale bytes after the smaller snapshot.
        await d.ModifyThrowAsync( TestHelper.Monitor, () => d.Root.Prop1 = RandomString( 200_000 ) );
        await d.ModifyThrowAsync( TestHelper.Monitor, () => d.Root.Prop1 = RandomString( 50_000 ) );
        var expected = client.WriteSnapshotToArray();

        var tr = await FailingTransactionAsync( d );
        tr.Success.ShouldBeTrue();
        tr.RollbackedInfo.ShouldNotBeNull();

        client.WriteSnapshotToArray().ShouldBe( expected );
        (await client.WriteSnapshotToArrayAsync()).ShouldBe( expected );
        client.ToString().ShouldStartWith( $"Snapshot {expected.Length} bytes" );
    }

    [TestCase( CompressionKind.None )]
    [TestCase( CompressionKind.GZiped )]
    public async Task WriteSnapshot_after_LoadOrCreateAndInitializeSnapshot_writes_the_loaded_snapshot_Async( CompressionKind kind )
    {
        var name = nameof( WriteSnapshot_after_LoadOrCreateAndInitializeSnapshot_writes_the_loaded_snapshot_Async );
        var source = new SnapshotTestClient( name ) { CompressionKind = kind };
        byte[] snapshot;
        using( var d0 = new ObservableDomain<TestObservableRootObject>( TestHelper.Monitor, name, startTimer: false, source ) )
        {
            await d0.ModifyThrowAsync( TestHelper.Monitor, () => d0.Root.Prop1 = RandomString( 100_000 ) );
            snapshot = source.WriteSnapshotToArray();
        }
        var client = new SnapshotTestClient( name );
        using var d = client.Load( snapshot );
        client.CurrentSerialNumber.ShouldBe( 1 );
        client.WriteSnapshotToArray().ShouldBe( snapshot );

        // A rollback before the first commit reads the loaded snapshot.
        var tr = await FailingTransactionAsync( d );
        tr.Success.ShouldBeTrue();
        client.WriteSnapshotToArray().ShouldBe( snapshot );
    }

    static async Task RunWithConcurrentTransactionsAsync( ObservableDomain<TestObservableRootObject> d, Action check )
    {
        using var stop = new CancellationTokenSource();
        var writer = Task.Run( async () =>
        {
            var monitor = new ActivityMonitor( "Concurrent transactions." );
            int i = 0;
            while( !stop.IsCancellationRequested )
            {
                if( ++i % 3 == 0 ) await FailingTransactionAsync( d, monitor );
                else await d.ModifyThrowAsync( monitor, () => d.Root.Prop1 = RandomString( Random.Shared.Next( 1_000, 200_000 ) ) );
            }
            monitor.MonitorEnd();
        } );
        try
        {
            for( int i = 0; i < 25; ++i )
            {
                check();
                await Task.Delay( Random.Shared.Next( 1, 10 ) );
            }
        }
        finally
        {
            stop.Cancel();
            await writer;
        }
    }

    [TestCase( CompressionKind.None )]
    [TestCase( CompressionKind.GZiped )]
    public async Task WriteSnapshot_never_writes_a_torn_snapshot_during_transactions_Async( CompressionKind kind )
    {
        var name = nameof( WriteSnapshot_never_writes_a_torn_snapshot_during_transactions_Async );
        var client = new SnapshotTestClient( name ) { CompressionKind = kind };
        using var d = new ObservableDomain<TestObservableRootObject>( TestHelper.Monitor, name, startTimer: false, client );
        await d.ModifyThrowAsync( TestHelper.Monitor, () => d.Root.Prop1 = RandomString( 100_000 ) );
        await RunWithConcurrentTransactionsAsync( d, () =>
        {
            var snapshot = client.WriteSnapshotToArray();
            var (serialNumber, prop1) = SnapshotTestClient.ReadSnapshot( name, snapshot );
            serialNumber.ShouldBeGreaterThan( 0 );
            prop1.ShouldNotBeNull();
        } );
    }

    [TestCase( CompressionKind.None )]
    [TestCase( CompressionKind.GZiped )]
    public async Task CopySnapshot_returns_the_serial_number_of_the_copied_bytes_during_transactions_Async( CompressionKind kind )
    {
        var name = nameof( CopySnapshot_returns_the_serial_number_of_the_copied_bytes_during_transactions_Async );
        var client = new SnapshotTestClient( name ) { CompressionKind = kind };
        using var d = new ObservableDomain<TestObservableRootObject>( TestHelper.Monitor, name, startTimer: false, client );
        await d.ModifyThrowAsync( TestHelper.Monitor, () => d.Root.Prop1 = RandomString( 100_000 ) );
        await RunWithConcurrentTransactionsAsync( d, () =>
        {
            var (bytes, headerLength, serialNumber) = client.CopySnapshotToArray();
            headerLength.ShouldBe( 8 );
            SnapshotTestClient.ReadSnapshot( name, bytes ).SerialNumber.ShouldBe( serialNumber );
        } );
    }

    [Test]
    public void CopySnapshot_without_snapshot_is_empty()
    {
        var client = new SnapshotTestClient( nameof( CopySnapshot_without_snapshot_is_empty ) );
        var (bytes, headerLength, serialNumber) = client.CopySnapshotToArray();
        bytes.ShouldBeEmpty();
        headerLength.ShouldBe( 0 );
        serialNumber.ShouldBe( -1 );
        client.WriteSnapshotToArray().ShouldBeEmpty();
        client.WriteSnapshotToArray( skipSnapshotHeader: true ).ShouldBeEmpty();
    }

    [Test]
    public async Task WriteSnapshot_skips_the_legacy_2_bytes_header_Async()
    {
        var name = nameof( WriteSnapshot_skips_the_legacy_2_bytes_header_Async );
        var raw = new System.IO.MemoryStream();
        using( var d0 = new ObservableDomain<TestObservableRootObject>( TestHelper.Monitor, name, startTimer: false ) )
        {
            await d0.ModifyThrowAsync( TestHelper.Monitor, () => d0.Root.Prop1 = "Legacy." );
            d0.Save( TestHelper.Monitor, raw ).ShouldBeTrue();
        }
        // The legacy header: a 0 version byte and the CompressionKind byte.
        var legacy = new byte[] { 0, (byte)CompressionKind.None }.Concat( raw.ToArray() ).ToArray();
        var client = new SnapshotTestClient( name );
        using var d = client.Load( legacy );
        d.Root.Prop1.ShouldBe( "Legacy." );
        client.WriteSnapshotToArray().ShouldBe( legacy );
        client.WriteSnapshotToArray( skipSnapshotHeader: true ).ShouldBe( raw.ToArray() );
    }

    [Test]
    public async Task a_failed_LoadOrCreateAndInitializeSnapshot_keeps_the_previous_snapshot_Async()
    {
        var name = nameof( a_failed_LoadOrCreateAndInitializeSnapshot_keeps_the_previous_snapshot_Async );
        var client = new SnapshotTestClient( name );
        using var d = new ObservableDomain<TestObservableRootObject>( TestHelper.Monitor, name, startTimer: false, client );
        await d.ModifyThrowAsync( TestHelper.Monitor, () => d.Root.Prop1 = "Previous." );
        var expected = client.WriteSnapshotToArray();

        // A valid header followed by bytes that cannot be deserialized.
        var invalid = new byte[] { 1 }.Concat( System.Text.Encoding.ASCII.GetBytes( "OD-None" ) ).Concat( new byte[32] ).ToArray();
        Util.Invokable( () => client.Load( invalid ) ).ShouldThrow<Exception>();

        client.CurrentSerialNumber.ShouldBe( 1 );
        client.WriteSnapshotToArray().ShouldBe( expected );
    }

    [Test]
    public void LoadOrCreateAndInitializeSnapshot_without_header_fails_without_assertion()
    {
        var client = new SnapshotTestClient( nameof( LoadOrCreateAndInitializeSnapshot_without_header_fails_without_assertion ) );
        // The first byte is neither 0 nor 1: there is no header.
        var noHeader = new byte[] { 2 }.Concat( new byte[32] ).ToArray();
        var ex = Util.Invokable( () => client.Load( noHeader ) ).ShouldThrow<Exception>();
        // A failed Debug.Assert is turned into a MonitoringFailFastException by the test trace listener.
        for( var e = ex; e != null; e = e.InnerException )
        {
            e.GetType().Name.ShouldNotBe( "MonitoringFailFastException", ex.ToString() );
        }
    }

    [TestCase( CompressionKind.None )]
    [TestCase( CompressionKind.GZiped )]
    public async Task a_throwing_Save_keeps_the_previous_snapshot_Async( CompressionKind kind )
    {
        var name = nameof( a_throwing_Save_keeps_the_previous_snapshot_Async );
        var client = new SnapshotTestClient( name ) { CompressionKind = kind };
        using var d = new ObservableDomain<TestObservableRootObject>( TestHelper.Monitor, name, startTimer: false, client );
        var prop1 = RandomString( 100_000 );
        await d.ModifyThrowAsync( TestHelper.Monitor, () => d.Root.Prop1 = prop1 );
        var expected = client.WriteSnapshotToArray();
        client.CurrentSerialNumber.ShouldBe( 1 );

        var tr = await d.TryModifyAsync( TestHelper.Monitor, () =>
        {
            d.Root.Prop1 = "Committed but not in the snapshot.";
            d.Root.TestBehavior__ThrowOnWrite = true;
        } );
        tr.IsCriticalError.ShouldBeTrue( "The snapshot failed." );

        client.CurrentSerialNumber.ShouldBe( 1 );
        client.WriteSnapshotToArray().ShouldBe( expected );

        // The rollback source is the previous complete snapshot.
        var rollback = await FailingTransactionAsync( d );
        rollback.Success.ShouldBeTrue();
        rollback.RollbackedInfo.ShouldNotBeNull();
        d.Read( TestHelper.Monitor, () =>
        {
            d.TransactionSerialNumber.ShouldBe( 1 );
            d.Root.Prop1.ShouldBe( prop1 );
            d.Root.TestBehavior__ThrowOnWrite.ShouldBeFalse();
        } );
    }
}
