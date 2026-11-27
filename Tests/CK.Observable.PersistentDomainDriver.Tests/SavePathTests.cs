using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CK.BinarySerialization;
using CK.Core;
using NUnit.Framework;
using Shouldly;
using static CK.Observable.PersistentDomainDriverTests.DriverTestKit;

namespace CK.Observable.PersistentDomainDriverTests;

/// <summary>The target of a reminder whose handler throws: the transaction that raises it fails and rolls back.</summary>
[SerializationVersion( 0 )]
public sealed class OThrowingTicker : ObservableObject
{
    public OThrowingTicker()
    {
    }

    OThrowingTicker( IBinaryDeserializer d, ITypeReadInfo info )
        : base( Sliced.Instance )
    {
    }

    public static void Write( IBinarySerializer s, in OThrowingTicker o )
    {
    }

    public void OnElapsed( object sender, ObservableReminderEventArgs e )
    {
        throw new InvalidOperationException( "The reminder fails (test)." );
    }
}

/// <summary>
/// Emulates the rules of the TypeScript client (CK.TS.ObservableDomain/Res/ObservableDomain.ts, tag v18.0.8):
/// applyDomainExport, applyWatchEvent and applyEvent. It keeps only the transaction number and the object count.
/// </summary>
public sealed class TsClientEmulator
{
    bool _isInitialExport = true;

    public int TranNum { get; private set; }

    public int ObjCount { get; private set; }

    /// <summary>The same as the "new ObservableDomain( export )" constructor and applyDomainExport.</summary>
    public void ApplyDomainExport( string export )
    {
        using var doc = JsonDocument.Parse( export );
        TranNum = doc.RootElement.GetProperty( "N" ).GetInt32();
        ObjCount = doc.RootElement.GetProperty( "C" ).GetInt32();
        _isInitialExport = true;
    }

    /// <summary>The same as applyWatchEvent (ObservableDomain.ts lines 175-226).</summary>
    public void ApplyWatchEvent( string json )
    {
        using var doc = JsonDocument.Parse( json );
        var e = doc.RootElement;
        if( e.TryGetProperty( "E", out var events ) )
        {
            int n = e.GetProperty( "N" ).GetInt32();
            int firstTransactionNumber = n - events.GetArrayLength() + 1;
            // In JS, "if (e.L)" is false for undefined and for 0.
            if( e.TryGetProperty( "L", out var lProp ) && lProp.GetInt32() != 0 )
            {
                int l = lProp.GetInt32();
                if( l != TranNum )
                {
                    if( TranNum > l && TranNum < n && _isInitialExport )
                    {
                        TranNum = firstTransactionNumber - 1;
                    }
                    else
                    {
                        throw new InvalidOperationException( $"Event claims last event was TN {l}, current TN is {TranNum}" );
                    }
                }
                else
                {
                    TranNum = firstTransactionNumber - 1;
                }
            }
            int i = 0;
            foreach( var one in events.EnumerateArray() )
            {
                ApplyEvent( firstTransactionNumber + i++, one );
            }
        }
        else if( e.TryGetProperty( "P", out _ ) )
        {
            ApplyDomainExport( json );
        }
        else
        {
            throw new InvalidOperationException( "Unknown WatchEvent: " + json );
        }
    }

    /// <summary>The same as applyEvent (ObservableDomain.ts lines 244-247 and 345-346). Only "N" and "D" change the count.</summary>
    void ApplyEvent( int n, JsonElement events )
    {
        if( TranNum + 1 != n )
        {
            throw new InvalidOperationException( $"Invalid transaction number. Expected: {TranNum + 1}, got {n}." );
        }
        foreach( var ev in events.EnumerateArray() )
        {
            var code = ev[0].GetString();
            if( code == "N" ) ObjCount++;
            else if( code == "D" ) ObjCount--;
        }
        TranNum = n;
        _isInitialExport = false;
    }
}

/// <summary>
/// The save path without driver transactions (D1): the timer, the retry and the stop copy the committed state
/// under the domain read lock and write it outside the lock.
/// </summary>
[TestFixture]
public class SavePathTests
{
    static readonly IEnumerable<IObservableDomainConfigurator<TestDriver>> _noConfigurators = Enumerable.Empty<IObservableDomainConfigurator<TestDriver>>();
    static readonly IEnumerable<IPersistentDomainLoadHandler<TestDriver>> _noHandlers = Enumerable.Empty<IPersistentDomainLoadHandler<TestDriver>>();

    // R2: a TransactionDone handler fails in each transaction. The domain then skips the post actions: no commit saves.
    // The stop must still write the last committed state.
    [Test]
    public async Task a_stop_after_a_failing_TransactionDone_handler_writes_the_last_committed_state()
    {
        var monitor = new ActivityMonitor();
        var store = TestStore.NewDirectory();
        var services = NewServices( store, "TestDomain", snapshotSaveDelay: "-00:00:01" );
        var driver = new TestDriver();
        await driver.DoHostStartAsync( monitor, services, _noConfigurators, _noHandlers );
        driver.Domain.TransactionDone += ( sender, e ) => throw new InvalidOperationException( "The handler fails (test)." );

        var result = await driver.Domain.ModifyAsync( monitor, () => SetMarker( driver, "last" ),
                                                      throwException: false,
                                                      millisecondsTimeout: -1,
                                                      considerRolledbackAsFailure: true,
                                                      parallelDomainPostActions: true,
                                                      waitForDomainPostActionsCompletion: true );
        result.Success.ShouldBeFalse( "The handler error is an error of the transaction." );
        await driver.DoHostStopAsync( monitor );

        driver.Saver.ShouldNotBeNull().SavedTransactionNumber.ShouldBe( driver.Domain.TransactionSerialNumber );
        MarkerOf( monitor, OdFiles( store )[^1] ).ShouldBe( "last" );
    }

    // R2: a reminder is due and its handler throws. Each transaction raises it first, so each transaction rolls back.
    // The stop must still write the last committed state.
    [Test]
    public async Task a_stop_with_a_due_timed_event_that_rolls_back_writes_the_last_committed_state()
    {
        var monitor = new ActivityMonitor();
        var store = TestStore.NewDirectory();
        var services = NewServices( store, "TestDomain", snapshotSaveDelay: "-00:00:01" );
        var driver = new TestDriver();
        await driver.DoHostStartAsync( monitor, services, _noConfigurators, _noHandlers );
        await driver.Domain.ModifyThrowAsync( monitor, () =>
        {
            SetMarker( driver, "last" );
            driver.Domain.TimeManager.Start();
            var ticker = new OThrowingTicker();
            var reminder = new ObservableReminder( DateTime.UtcNow.AddMilliseconds( 100 ) );
            reminder.Elapsed += ticker.OnElapsed;
        }, waitForDomainPostActionsCompletion: true );
        // The reminder is due. A transaction that raises it fails.
        await Task.Delay( 300 );
        var probe = await driver.Domain.ModifyAsync( monitor, () => { }, throwException: false, millisecondsTimeout: -1,
                                                     considerRolledbackAsFailure: true, parallelDomainPostActions: true,
                                                     waitForDomainPostActionsCompletion: true );
        probe.Success.ShouldBeFalse( "The due reminder must fail the transaction." );

        await driver.DoHostStopAsync( monitor );

        MarkerOf( monitor, OdFiles( store )[^1] ).ShouldBe( "last" );
    }

    // R12: a stop with no change writes no file. A restart with no change writes one file: the first save of the start.
    [Test]
    public async Task an_idle_restart_adds_at_most_one_generation_and_a_stop_with_no_change_writes_none()
    {
        var monitor = new ActivityMonitor();
        var store = TestStore.NewDirectory();
        var services = NewServices( store, "TestDomain" );

        var driver = new TestDriver();
        await driver.DoHostStartAsync( monitor, services, _noConfigurators, _noHandlers );
        OdFiles( store ).Length.ShouldBe( 1 );
        await driver.Domain.ModifyThrowAsync( monitor, () => SetMarker( driver, "changed" ), waitForDomainPostActionsCompletion: true );
        OdFiles( store ).Length.ShouldBe( 2 );
        await driver.DoHostStopAsync( monitor );
        OdFiles( store ).Length.ShouldBe( 2, "The commit saved the last state: the stop writes no file." );

        for( int i = 0; i < 2; ++i )
        {
            var again = new TestDriver();
            await again.DoHostStartAsync( monitor, services, _noConfigurators, _noHandlers );
            await again.DoHostStopAsync( monitor );
            OdFiles( store ).Length.ShouldBe( 3 + i, "An idle restart adds one generation." );
        }
        MarkerOf( monitor, OdFiles( store )[^1] ).ShouldBe( "changed" );
    }

    // R7: a trailing save of the timer runs no transaction. A client that exported before it applies the next real event.
    [Test]
    public async Task a_trailing_save_runs_no_transaction_and_a_client_applies_the_next_real_event()
    {
        var monitor = new ActivityMonitor();
        var store = TestStore.NewDirectory();
        var services = NewServices( store, "TestDomain", snapshotSaveDelay: "00:00:00.500" );
        await PrepareQuietDomainAsync( monitor, services );

        var driver = new TestDriver();
        await driver.DoHostStartAsync( monitor, services, _noConfigurators, _noHandlers );
        try
        {
            // Commit 1 claims the save window. Commit 2 is skipped and starts the trailing timer.
            await driver.Domain.ModifyThrowAsync( monitor, () => { }, waitForDomainPostActionsCompletion: true );
            await driver.Domain.ModifyThrowAsync( monitor, () => { }, waitForDomainPostActionsCompletion: true );
            int skipped = driver.Domain.TransactionSerialNumber;
            var clientA = new TsClientEmulator();
            clientA.ApplyDomainExport( driver.Domain.ExportToString()! );
            // Since CK.Observable.Domain 20.1 the collector also pushes empty transactions. The recorder starts
            // after the export, so it records only the pushes that the client must apply.
            var pushes = new PushRecorder( driver );

            var saver = driver.Saver.ShouldNotBeNull();
            var limit = DateTime.UtcNow.AddSeconds( 5 );
            while( saver.SavedTransactionNumber < skipped && DateTime.UtcNow < limit ) await Task.Delay( 20 );
            saver.SavedTransactionNumber.ShouldBe( skipped, "The trailing save writes the skipped commit." );
            driver.Domain.TransactionSerialNumber.ShouldBe( skipped, "The trailing save runs no transaction." );

            await driver.Domain.ModifyThrowAsync( monitor, () => new OCounter { Count = 1 }, waitForDomainPostActionsCompletion: true );
            var realPush = await pushes.WaitForAsync( 1 );
            Should.NotThrow( () => clientA.ApplyWatchEvent( realPush ) );
        }
        finally
        {
            await driver.DoHostStopAsync( monitor );
        }
    }

    // R7: the stop runs no transaction. A stop with a change to save writes it without a transaction too.
    [Test]
    public async Task a_stop_runs_no_transaction()
    {
        var monitor = new ActivityMonitor();
        var store = TestStore.NewDirectory();
        var services = NewServices( store, "TestDomain", snapshotSaveDelay: "-00:00:01" );
        await PrepareQuietDomainAsync( monitor, services );

        var driver = new TestDriver();
        await driver.DoHostStartAsync( monitor, services, _noConfigurators, _noHandlers );
        await driver.Domain.ModifyThrowAsync( monitor, () => SetMarker( driver, "to save" ), waitForDomainPostActionsCompletion: true );
        int before = driver.Domain.TransactionSerialNumber;

        await driver.DoHostStopAsync( monitor );

        driver.Domain.TransactionSerialNumber.ShouldBe( before );
        driver.Saver.ShouldNotBeNull().SavedTransactionNumber.ShouldBe( before );
        MarkerOf( monitor, OdFiles( store )[^1] ).ShouldBe( "to save" );
    }

    // R7: with no event-less transaction between two kept events, the restart reply of the watcher holds exactly the
    // events after the transaction number of the client.
    [Test]
    public async Task a_restarting_client_gets_a_correct_event_list_after_a_trailing_save()
    {
        var monitor = new ActivityMonitor();
        var store = TestStore.NewDirectory();
        var services = NewServices( store, "TestDomain", snapshotSaveDelay: "00:00:00.500" );
        await PrepareQuietDomainAsync( monitor, services );

        var driver = new TestDriver();
        await driver.DoHostStartAsync( monitor, services, _noConfigurators, _noHandlers );
        var pushes = new PushRecorder( driver );
        try
        {
            var live = new TsClientEmulator();
            live.ApplyDomainExport( driver.Domain.ExportToString()! );

            // Change 1 claims the save window. Change 2 is skipped and starts the trailing timer.
            await driver.Domain.ModifyThrowAsync( monitor, () => new OCounter { Count = 1 }, waitForDomainPostActionsCompletion: true );
            live.ApplyWatchEvent( await pushes.WaitForAsync( 1 ) );
            int afterFirst = live.TranNum;
            await driver.Domain.ModifyThrowAsync( monitor, () => new OCounter { Count = 2 }, waitForDomainPostActionsCompletion: true );
            live.ApplyWatchEvent( await pushes.WaitForAsync( 2 ) );

            var saver = driver.Saver.ShouldNotBeNull();
            var limit = DateTime.UtcNow.AddSeconds( 5 );
            while( saver.SavedTransactionNumber < live.TranNum && DateTime.UtcNow < limit ) await Task.Delay( 20 );
            saver.SavedTransactionNumber.ShouldBe( live.TranNum );

            await driver.Domain.ModifyThrowAsync( monitor, () => new OCounter { Count = 3 }, waitForDomainPostActionsCompletion: true );
            live.ApplyWatchEvent( await pushes.WaitForAsync( 3 ) );
            var truth = new TsClientEmulator();
            truth.ApplyDomainExport( driver.Domain.ExportToString()! );
            live.TranNum.ShouldBe( truth.TranNum );
            live.ObjCount.ShouldBe( truth.ObjCount );

            // A client that applied change 1 and lost the connection restarts with its transaction number.
            var (current, events) = driver.EventCollector.GetTransactionEvents( afterFirst );
            events.ShouldNotBeNull().Select( e => e.TransactionNumber ).ShouldBe( Enumerable.Range( afterFirst + 1, current - afterFirst ) );
            var restarting = new TsClientEmulator();
            restarting.ApplyDomainExport( $"{{\"N\":{afterFirst},\"C\":{truth.ObjCount - 2},\"P\":[],\"O\":[],\"R\":[],\"S\":[]}}" );
            restarting.ApplyWatchEvent( RestartReply( events ) );
            restarting.TranNum.ShouldBe( truth.TranNum );
            restarting.ObjCount.ShouldBe( truth.ObjCount );
        }
        finally
        {
            await driver.DoHostStopAsync( monitor );
        }
    }

    static void SetMarker( TestDriver driver, string marker )
    {
        driver.Domain.AllObjects.Items.OfType<OTestSingleton>().Single().Marker = marker;
    }

    /// <summary>Creates the domain, commits a change and stops: the next start loads a domain with data.</summary>
    static async Task PrepareQuietDomainAsync( IActivityMonitor monitor, IServiceProvider services )
    {
        var driver = new TestDriver();
        await driver.DoHostStartAsync( monitor, services, _noConfigurators, _noHandlers );
        await driver.Domain.ModifyThrowAsync( monitor, () =>
        {
            SetMarker( driver, "quiet" );
            new OCounter { Count = 0 };
        }, waitForDomainPostActionsCompletion: true );
        await driver.DoHostStopAsync( monitor );
    }

    /// <summary>The reply of ObservableDomainWatcher.StartOrRestartWatchAsync (WebSocketWatcher, tag v18.0.8, lines 76-117) for a list of events.</summary>
    static string RestartReply( IReadOnlyList<JsonEventCollector.TransactionEvent> events )
    {
        var b = new StringBuilder();
        b.Append( "{\"N\":" ).Append( events[^1].TransactionNumber ).Append( ",\"E\":[" );
        bool atLeastOne = false;
        foreach( var t in events )
        {
            if( atLeastOne ) b.Append( ',' );
            else atLeastOne = true;
            b.Append( '[' ).Append( t.ExportedEvents ).Append( ']' );
        }
        b.Append( "]}" );
        return b.ToString();
    }

    /// <summary>
    /// Records the pushes of the watcher: the same JSON as ObservableDomainWatcher.DomainSubscription.OnDomainChangedAsync
    /// (WebSocketWatcher, tag v18.0.8, lines 205-217) without the domain name.
    /// </summary>
    sealed class PushRecorder
    {
        readonly List<string> _pushes = new List<string>();

        public PushRecorder( IObservableDomainDriver driver )
        {
            driver.EventCollector.LastEventChanged.Async += ( monitor, e, cancel ) =>
            {
                string json = $"{{\"N\":{e.TransactionNumber},\"E\":[[{e.ExportedEvents}]],\"L\":{e.LastExportedTransactionNumber}}}";
                lock( _pushes ) _pushes.Add( json );
                return Task.CompletedTask;
            };
        }

        public async Task<string> WaitForAsync( int count )
        {
            var limit = DateTime.UtcNow.AddSeconds( 5 );
            while( DateTime.UtcNow < limit )
            {
                lock( _pushes )
                {
                    if( _pushes.Count >= count ) return _pushes[count - 1];
                }
                await Task.Delay( 10 );
            }
            lock( _pushes ) throw new TimeoutException( $"Push n°{count} did not come. Pushes: {string.Join( " ; ", _pushes )}" );
        }
    }
}
