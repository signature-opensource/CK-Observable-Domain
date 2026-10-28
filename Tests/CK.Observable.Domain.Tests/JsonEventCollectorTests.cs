using CK.BinarySerialization;
using CK.Core;
using CK.Observable.Domain.Tests.Clients;
using NUnit.Framework;
using Shouldly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Domain.Tests;

[TestFixture]
public class JsonEventCollectorTests
{
    sealed class PushedEvents
    {
        readonly List<JsonEventCollector.TransactionEvent> _events = new();

        public PushedEvents( JsonEventCollector collector )
        {
            collector.LastEventChanged.Sync += ( m, e ) => { lock( _events ) _events.Add( e ); };
        }

        public IReadOnlyList<JsonEventCollector.TransactionEvent> Events
        {
            get { lock( _events ) return _events.ToArray(); }
        }

        /// <summary>
        /// Waits for the pushed transaction number (the events are pushed by a background loop).
        /// </summary>
        public async Task WaitForAsync( int transactionNumber )
        {
            var limit = DateTime.UtcNow.AddSeconds( 5 );
            while( !Events.Any( e => e.TransactionNumber == transactionNumber ) )
            {
                if( DateTime.UtcNow > limit ) Assert.Fail( $"Transaction n°{transactionNumber} has not been pushed." );
                await Task.Delay( 5 );
            }
        }

        /// <summary>
        /// Waits for a number of pushed events.
        /// </summary>
        public async Task WaitForCountAsync( int count )
        {
            var limit = DateTime.UtcNow.AddSeconds( 5 );
            while( Events.Count < count )
            {
                if( DateTime.UtcNow > limit ) Assert.Fail( $"Only {Events.Count} events pushed, {count} expected." );
                await Task.Delay( 5 );
            }
        }

        /// <summary>
        /// Waits a little, to give the background loop a chance to push an unexpected event.
        /// </summary>
        public Task SettleAsync() => Task.Delay( 100 );
    }

    [Test]
    public async Task a_no_op_transaction_is_pushed_with_empty_events_and_contiguous_numbers_Async()
    {
        using var d = new ObservableDomain( TestHelper.Monitor, nameof( a_no_op_transaction_is_pushed_with_empty_events_and_contiguous_numbers_Async ), startTimer: false );
        var collector = new JsonEventCollector( d );
        var pushed = new PushedEvents( collector );

        ObservableList<int>? list = null;
        await d.ModifyThrowAsync( TestHelper.Monitor, () => list = new ObservableList<int>() );
        await d.ModifyThrowAsync( TestHelper.Monitor, () => list!.Add( 1 ) );
        await d.ModifyThrowAsync( TestHelper.Monitor, null );
        await d.ModifyThrowAsync( TestHelper.Monitor, () => list!.Add( 2 ) );
        d.TransactionSerialNumber.ShouldBe( 4 );
        await pushed.WaitForAsync( 4 );

        var events = pushed.Events;
        events.Select( e => e.TransactionNumber ).ShouldBe( new[] { 1, 2, 3, 4 } );
        events[2].ExportedEvents.ShouldBeEmpty( "Transaction n°3 changed nothing." );
        events[1].ExportedEvents.ShouldNotBeEmpty();
        events[3].ExportedEvents.ShouldNotBeEmpty();
        foreach( var e in events.Skip( 1 ) )
        {
            e.LastExportedTransactionNumber.ShouldBe( e.TransactionNumber - 1, "Every transaction is exported: the previous one is the last exported." );
            e.DomainExport.ShouldBeNull( "A transaction that follows the previous one needs no full export." );
        }
    }

    [Test]
    public async Task the_initial_transaction_is_pushed_with_a_full_export_Async()
    {
        using var d = new ObservableDomain( TestHelper.Monitor, nameof( the_initial_transaction_is_pushed_with_a_full_export_Async ), startTimer: false );
        var collector = new JsonEventCollector( d );
        var pushed = new PushedEvents( collector );

        await d.ModifyThrowAsync( TestHelper.Monitor, () => new ObservableList<int>() );
        await pushed.WaitForAsync( 1 );

        // A store that is recreated resets the collector: n°1 can reach clients that have a greater number.
        var e1 = pushed.Events.Single();
        e1.DomainExport.ShouldBe( d.ExportToString() );
    }

    [Test]
    public async Task GetTransactionEvents_returns_contiguous_transactions_with_no_op_transactions_Async()
    {
        using var d = new ObservableDomain( TestHelper.Monitor, nameof( GetTransactionEvents_returns_contiguous_transactions_with_no_op_transactions_Async ), startTimer: false );
        var collector = new JsonEventCollector( d );

        ObservableList<int>? list = null;
        await d.ModifyThrowAsync( TestHelper.Monitor, () => list = new ObservableList<int>() );
        for( int i = 2; i <= 5; ++i )
        {
            await d.ModifyThrowAsync( TestHelper.Monitor, () => list!.Add( i ) );
        }
        // Transaction n°6 changes nothing: a client at n°5 must receive n°6, not n°5 again.
        await d.ModifyThrowAsync( TestHelper.Monitor, null );
        var (n, events) = collector.GetTransactionEvents( 5 );
        n.ShouldBe( 6 );
        events.ShouldNotBeNull().Select( e => e.TransactionNumber ).ShouldBe( new[] { 6 } );
        events[0].ExportedEvents.ShouldBeEmpty();

        // Transaction n°7 emits events: a client at n°5 must receive n°6 and n°7, not n°5 and n°7.
        await d.ModifyThrowAsync( TestHelper.Monitor, () => list!.Add( 7 ) );
        (n, events) = collector.GetTransactionEvents( 5 );
        n.ShouldBe( 7 );
        events.ShouldNotBeNull().Select( e => e.TransactionNumber ).ShouldBe( new[] { 6, 7 } );

        (n, events) = collector.GetTransactionEvents( 3 );
        events.ShouldNotBeNull().Select( e => e.TransactionNumber ).ShouldBe( new[] { 4, 5, 6, 7 } );
    }

    [Test]
    public async Task a_rollback_transaction_is_not_pushed_Async()
    {
        using var d = new ObservableDomain<TestObservableRootObject>( TestHelper.Monitor,
                                                                      nameof( a_rollback_transaction_is_not_pushed_Async ),
                                                                      startTimer: false,
                                                                      new ConcreteMemoryTransactionProviderClient() );
        var collector = new JsonEventCollector( d );
        var pushed = new PushedEvents( collector );

        await d.ModifyThrowAsync( TestHelper.Monitor, () => d.Root.Prop1 = "One" );
        await d.ModifyThrowAsync( TestHelper.Monitor, () => d.Root.Prop1 = "Two" );
        await pushed.WaitForAsync( 2 );

        // The rollback commits a transaction that keeps the restored serial number n°2.
        var rollback = await d.TryModifyAsync( TestHelper.Monitor, () =>
        {
            d.Root.Prop1 = "Never committed.";
            throw new Exception( "Failing transaction. This is a test exception." );
        }, considerRolledbackAsFailure: false );
        rollback.RollbackedInfo.ShouldNotBeNull();
        d.TransactionSerialNumber.ShouldBe( 2 );
        await pushed.SettleAsync();
        pushed.Events.Select( e => e.TransactionNumber ).ShouldBe( new[] { 1, 2 }, "The rollback transaction n°2 is not pushed a second time." );
        collector.GetTransactionEvents( 1 ).Events.ShouldNotBeNull().Select( e => e.TransactionNumber )
                 .ShouldBe( new[] { 2 }, "The state is the one of n°2: the stored transactions are still valid." );

        await d.ModifyThrowAsync( TestHelper.Monitor, () => d.Root.Prop1 = "Three" );
        await pushed.WaitForAsync( 3 );
        pushed.Events.Select( e => e.TransactionNumber ).ShouldBe( new[] { 1, 2, 3 } );
        pushed.Events[2].LastExportedTransactionNumber.ShouldBe( 2 );
        pushed.Events[2].DomainExport.ShouldBeNull( "After a safe rollback, the clients can apply n°3." );
        collector.GetTransactionEvents( 2 ).Events.ShouldNotBeNull().Select( e => e.TransactionNumber ).ShouldBe( new[] { 3 } );
        collector.GetTransactionEvents( 1 ).Events.ShouldNotBeNull().Select( e => e.TransactionNumber ).ShouldBe( new[] { 2, 3 } );
    }

    [Test]
    public async Task a_dangerous_rollback_is_pushed_with_its_previous_number_Async()
    {
        // With SkipTransactionCount = 2, the snapshots are n°0, n°3, n°6...: a failure at n°5 rolls back to n°3.
        using var d = new ObservableDomain<TestObservableRootObject>( TestHelper.Monitor,
                                                                      nameof( a_dangerous_rollback_is_pushed_with_its_previous_number_Async ),
                                                                      startTimer: false,
                                                                      new ConcreteMemoryTransactionProviderClient() { SkipTransactionCount = 2 } );
        var collector = new JsonEventCollector( d );
        var pushed = new PushedEvents( collector );
        for( int i = 1; i <= 5; ++i )
        {
            await d.ModifyThrowAsync( TestHelper.Monitor, () => d.Root.Prop1 = $"n°{i}" );
        }
        await pushed.WaitForAsync( 5 );

        var rollback = await d.TryModifyAsync( TestHelper.Monitor, () =>
        {
            d.Root.Prop1 = "Never committed.";
            throw new Exception( "Failing transaction. This is a test exception." );
        }, considerRolledbackAsFailure: false );
        rollback.RollbackedInfo.ShouldNotBeNull().IsDangerousRollback.ShouldBeTrue();
        d.TransactionSerialNumber.ShouldBe( 3 );

        // A client at n°5 has a state that the domain lost: the push of n°3 carries the full export of the restored state.
        await pushed.WaitForCountAsync( 6 );
        var e3 = pushed.Events.Last();
        e3.TransactionNumber.ShouldBe( 3 );
        e3.LastExportedTransactionNumber.ShouldBe( 2 );
        e3.DomainExport.ShouldBe( d.ExportToString() );
        collector.GetTransactionEvents( 2 ).Events.ShouldBeNull( "The rollback n°3 is not a delta from n°2: a client at n°2 needs a full export." );
    }

    [Test]
    public async Task a_dangerous_rollback_that_reaches_the_last_number_is_pushed_with_a_full_export_Async()
    {
        // With SkipTransactionCount = 2, the snapshots are n°0, n°3, n°6...: a failure at n°5 rolls back to n°3.
        // The rollback instantiates the sidekick of the restored object: this increments the restored number to n°4.
        using var d = new ObservableDomain( TestHelper.Monitor,
                                            nameof( a_dangerous_rollback_that_reaches_the_last_number_is_pushed_with_a_full_export_Async ),
                                            startTimer: false,
                                            new ConcreteMemoryTransactionProviderClient() { SkipTransactionCount = 2 } );
        var collector = new JsonEventCollector( d );
        var pushed = new PushedEvents( collector );
        SidekickTests.ObjWithSKSimple? o = null;
        await d.ModifyThrowAsync( TestHelper.Monitor, () => o = new SidekickTests.ObjWithSKSimple() );
        for( int i = 2; i <= 4; ++i )
        {
            await d.ModifyThrowAsync( TestHelper.Monitor, () => o!.CommandMessage = $"n°{i}" );
        }
        await pushed.WaitForAsync( 4 );

        var rollback = await d.TryModifyAsync( TestHelper.Monitor, () =>
        {
            o!.CommandMessage = "Never committed.";
            throw new Exception( "Failing transaction. This is a test exception." );
        }, considerRolledbackAsFailure: false );
        rollback.RollbackedInfo.ShouldNotBeNull().IsDangerousRollback.ShouldBeTrue();
        d.TransactionSerialNumber.ShouldBe( 4, "The restored n°3 is incremented by the sidekick instantiation." );

        // A client at n°4 has the message "n°4": the domain lost it, the clients must receive the restored state.
        await pushed.WaitForCountAsync( 5 );
        var e4 = pushed.Events.Last();
        e4.TransactionNumber.ShouldBe( 4 );
        e4.DomainExport.ShouldBe( d.ExportToString() );
        collector.GetTransactionEvents( 3 ).Events.ShouldBeNull( "The stored n°4 is the lost one: a client at n°3 needs a full export." );
    }

    [Test]
    public async Task a_collector_on_a_loaded_domain_starts_at_its_transaction_number_Async()
    {
        var name = nameof( a_collector_on_a_loaded_domain_starts_at_its_transaction_number_Async );
        var source = new SnapshotTestClient( name );
        byte[] snapshot;
        using( var d0 = new ObservableDomain<TestObservableRootObject>( TestHelper.Monitor, name, startTimer: false, source ) )
        {
            await d0.ModifyThrowAsync( TestHelper.Monitor, () => d0.Root.Prop1 = "One" );
            await d0.ModifyThrowAsync( TestHelper.Monitor, () => d0.Root.Prop1 = "Two" );
            snapshot = source.WriteSnapshotToArray();
        }
        using var d = new SnapshotTestClient( name ).Load( snapshot );
        d.TransactionSerialNumber.ShouldBe( 2 );
        var collector = new JsonEventCollector( d );
        var pushed = new PushedEvents( collector );

        collector.GetTransactionEvents( 1 ).Events.ShouldBeNull( "A client at n°1 needs a full export." );
        var (n, events) = collector.GetTransactionEvents( 2 );
        n.ShouldBe( 2 );
        events.ShouldNotBeNull().ShouldBeEmpty( "A client at n°2 is up to date." );

        // The first transaction fails: the rollback commits a transaction that keeps the loaded number n°2.
        var rollback = await d.TryModifyAsync( TestHelper.Monitor, () =>
        {
            d.Root.Prop1 = "Never committed.";
            throw new Exception( "Failing transaction. This is a test exception." );
        }, considerRolledbackAsFailure: false );
        rollback.RollbackedInfo.ShouldNotBeNull();
        d.TransactionSerialNumber.ShouldBe( 2 );
        await pushed.SettleAsync();
        pushed.Events.ShouldBeEmpty( "A client at n°2 already has the state of n°2." );

        await d.ModifyThrowAsync( TestHelper.Monitor, () => d.Root.Prop1 = "Three" );
        await pushed.WaitForAsync( 3 );
        pushed.Events.Single().LastExportedTransactionNumber.ShouldBe( 2 );
        collector.GetTransactionEvents( 2 ).Events.ShouldNotBeNull().Select( e => e.TransactionNumber ).ShouldBe( new[] { 3 } );
    }

    [Test]
    public async Task a_gap_in_transaction_numbers_is_pushed_with_the_previous_number_Async()
    {
        using var d = new ObservableDomain( TestHelper.Monitor, nameof( a_gap_in_transaction_numbers_is_pushed_with_the_previous_number_Async ), startTimer: false );
        var collector = new JsonEventCollector( d );
        var pushed = new PushedEvents( collector );

        ObservableList<int>? list = null;
        await d.ModifyThrowAsync( TestHelper.Monitor, () => list = new ObservableList<int>() );
        await d.ModifyThrowAsync( TestHelper.Monitor, () => list!.Add( 2 ) );
        await pushed.WaitForAsync( 2 );

        // Transactions n°3 and n°4 are missed by the collector.
        collector.Detach();
        await d.ModifyThrowAsync( TestHelper.Monitor, () => list!.Add( 3 ) );
        await d.ModifyThrowAsync( TestHelper.Monitor, () => list!.Add( 4 ) );
        collector.CollectEvent( d, clearEvents: false );

        await d.ModifyThrowAsync( TestHelper.Monitor, () => list!.Add( 5 ) );
        await pushed.WaitForAsync( 5 );
        var e5 = pushed.Events.Single( e => e.TransactionNumber == 5 );
        e5.LastExportedTransactionNumber.ShouldBe( 4, "A client at n°2 must not apply n°5 directly: it missed n°3 and n°4." );
        e5.DomainExport.ShouldBe( d.ExportToString(), "A client at n°2 resynchronizes with the full export." );
        collector.GetTransactionEvents( 2 ).Events.ShouldBeNull( "A client at n°2 needs a full export." );
        collector.GetTransactionEvents( 4 ).Events.ShouldNotBeNull().Select( e => e.TransactionNumber ).ShouldBe( new[] { 5 } );
    }

    [Test]
    public async Task a_load_of_an_older_snapshot_that_reaches_the_last_number_is_pushed_with_a_full_export_Async()
    {
        using var d = new ObservableDomain<TestObservableRootObject>( TestHelper.Monitor,
                                                                      nameof( a_load_of_an_older_snapshot_that_reaches_the_last_number_is_pushed_with_a_full_export_Async ),
                                                                      startTimer: false );
        var collector = new JsonEventCollector( d );
        var pushed = new PushedEvents( collector );
        await d.ModifyThrowAsync( TestHelper.Monitor, () => d.Root.Prop1 = "One" );
        await d.ModifyThrowAsync( TestHelper.Monitor, () => d.Root.Prop1 = "Two" );
        var snapshot = new MemoryStream();
        d.Save( TestHelper.Monitor, snapshot ).ShouldBeTrue();
        await d.ModifyThrowAsync( TestHelper.Monitor, () => d.Root.Prop1 = "Three" );
        await pushed.WaitForAsync( 3 );

        // The load restores n°2 and the transaction commits n°3: a client at n°3 has the lost "Three".
        snapshot.Position = 0;
        await d.ModifyThrowAsync( TestHelper.Monitor, () => d.Load( TestHelper.Monitor, RewindableStream.FromStream( snapshot ) ) );
        d.TransactionSerialNumber.ShouldBe( 3 );
        d.Root.Prop1.ShouldBe( "Two" );

        await pushed.WaitForCountAsync( 4 );
        var e3 = pushed.Events.Last();
        e3.TransactionNumber.ShouldBe( 3 );
        e3.DomainExport.ShouldBe( d.ExportToString() );
    }

    [Test]
    public async Task a_load_of_the_initial_snapshot_restarts_the_transaction_events_Async()
    {
        using var d = new ObservableDomain<TestObservableRootObject>( TestHelper.Monitor,
                                                                      nameof( a_load_of_the_initial_snapshot_restarts_the_transaction_events_Async ),
                                                                      startTimer: false );
        var collector = new JsonEventCollector( d );
        var pushed = new PushedEvents( collector );
        var initial = new MemoryStream();
        d.Save( TestHelper.Monitor, initial ).ShouldBeTrue();
        for( int i = 1; i <= 3; ++i )
        {
            await d.ModifyThrowAsync( TestHelper.Monitor, () => d.Root.Prop1 = $"n°{i}" );
        }
        await pushed.WaitForAsync( 3 );

        // The load restores n°0 and the transaction commits n°1: its full export is pushed.
        initial.Position = 0;
        await d.ModifyThrowAsync( TestHelper.Monitor, () => d.Load( TestHelper.Monitor, RewindableStream.FromStream( initial ) ) );
        d.TransactionSerialNumber.ShouldBe( 1 );
        await d.ModifyThrowAsync( TestHelper.Monitor, () => d.Root.Prop1 = "After the load." );

        await pushed.WaitForCountAsync( 5 );
        var e2 = pushed.Events.Last();
        e2.TransactionNumber.ShouldBe( 2 );
        e2.DomainExport.ShouldBeNull( "N°2 follows the pushed export of n°1." );
        collector.GetTransactionEvents( 1 ).Events.ShouldNotBeNull().ShouldBe( new[] { e2 } );
    }

    [Test]
    public async Task a_collector_attached_to_a_domain_at_another_number_forgets_its_events_Async()
    {
        var name = nameof( a_collector_attached_to_a_domain_at_another_number_forgets_its_events_Async );
        var source = new SnapshotTestClient( name );
        using var d0 = new ObservableDomain<TestObservableRootObject>( TestHelper.Monitor, name, startTimer: false, source );
        var collector = new JsonEventCollector( d0 );
        var pushed = new PushedEvents( collector );
        await d0.ModifyThrowAsync( TestHelper.Monitor, () => d0.Root.Prop1 = "One" );
        await d0.ModifyThrowAsync( TestHelper.Monitor, () => d0.Root.Prop1 = "Two" );
        var snapshot = source.WriteSnapshotToArray();
        await d0.ModifyThrowAsync( TestHelper.Monitor, () => d0.Root.Prop1 = "Three" );
        await pushed.WaitForAsync( 3 );
        collector.Detach();

        // The domain is reloaded from n°2 (the save of n°3 failed): the stored n°3 belongs to the lost history.
        using var d = new SnapshotTestClient( name ).Load( snapshot );
        collector.CollectEvent( d, clearEvents: false );
        // Like a new collector on a loaded domain.
        collector.GetTransactionEvents( 1 ).Events.ShouldBeNull( "A client at n°1 needs a full export." );
        var (n, events) = collector.GetTransactionEvents( 2 );
        n.ShouldBe( 2 );
        events.ShouldNotBeNull().ShouldBeEmpty( "A client at n°2 is up to date." );
        (n, events) = collector.GetTransactionEvents( 3 );
        n.ShouldBe( 2, "A client at n°3 is ahead of the domain." );
        events.ShouldNotBeNull().ShouldBeEmpty();

        await d.ModifyThrowAsync( TestHelper.Monitor, () => d.Root.Prop1 = "Another Three" );
        await pushed.WaitForCountAsync( 4 );
        pushed.Events.Last().DomainExport.ShouldBe( d.ExportToString(), "A client at the lost n°3 must resynchronize." );
    }

    [Test]
    public async Task the_full_export_is_skipped_when_nobody_listens_Async()
    {
        using var d = new ObservableDomain( TestHelper.Monitor, nameof( the_full_export_is_skipped_when_nobody_listens_Async ), startTimer: false );
        var collector = new JsonEventCollector( d );

        await d.ModifyThrowAsync( TestHelper.Monitor, () => new ObservableList<int>() );
        collector.LastEvent.ShouldNotBeNull().DomainExport.ShouldBeNull( "Nobody receives the push of n°1." );
    }
}
