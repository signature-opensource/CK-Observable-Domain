using CK.Core;
using NUnit.Framework;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.League.Tests;

[TestFixture]
public class ObservableWatcherTests
{
    sealed class TestWatcher : ObservableWatcher
    {
        readonly List<WatchEvent> _events = new();

        public TestWatcher( IObservableLeague league )
            : base( league )
        {
        }

        public IReadOnlyList<WatchEvent> Events
        {
            get { lock( _events ) return _events.ToArray(); }
        }

        /// <summary>
        /// Waits for a number of handled events (the transaction events are pushed by a background loop).
        /// </summary>
        public async Task WaitForCountAsync( int count )
        {
            var limit = DateTime.UtcNow.AddSeconds( 5 );
            while( Events.Count < count )
            {
                if( DateTime.UtcNow > limit ) Assert.Fail( $"Only {Events.Count} events handled, {count} expected." );
                await Task.Delay( 5 );
            }
        }

        protected override ValueTask HandleEventAsync( IActivityMonitor monitor, WatchEvent e )
        {
            lock( _events ) _events.Add( e );
            return default;
        }

        protected override string WatcherId => nameof( TestWatcher );
    }

    static async Task<IObservableDomainLoader> CreateDomainAsync( ObservableLeague league, string domainName, int skipTransactionCount )
    {
        var options = new ManagedDomainOptions( DomainLifeCycleOption.Always,
                                                CompressionKind.None,
                                                skipTransactionCount,
                                                snapshotSaveDelay: TimeSpan.Zero,
                                                snapshotKeepDuration: TimeSpan.Zero,
                                                snapshotMaximalTotalKiB: 0,
                                                eventKeepDuration: TimeSpan.FromMinutes( 5 ),
                                                eventKeepLimit: 10,
                                                housekeepingRate: 0 );
        await league.Coordinator.ModifyThrowAsync( TestHelper.Monitor,
                                                   ( m, d ) => d.Root.CreateDomain( domainName, new[] { typeof( Model.School ).AssemblyQualifiedName! }, options ),
                                                   waitForDomainPostActionsCompletion: true );
        var loader = league.Find( domainName );
        loader.ShouldNotBeNull();
        return loader;
    }

    [Test]
    public async Task starting_a_watch_returns_a_full_export_whatever_the_client_transaction_number_Async()
    {
        var store = BasicLeagueTests.CreateStore( nameof( starting_a_watch_returns_a_full_export_whatever_the_client_transaction_number_Async ) );
        var league = await ObservableLeague.LoadAsync( TestHelper.Monitor, store );
        league.ShouldNotBeNull();
        var loader = await CreateDomainAsync( league, "D", skipTransactionCount: 0 );
        using var watcher = new TestWatcher( league );

        await using( var shell = await loader.LoadAsync<Model.School>( TestHelper.Monitor ) )
        {
            shell.ShouldNotBeNull();
            for( int i = 0; i < 3; ++i )
            {
                await shell.ModifyThrowAsync( TestHelper.Monitor, ( m, d ) => d.Root.Persons.Add( new Model.Person() { FirstName = $"P{i}" } ) );
            }
            var current = shell.Read( TestHelper.Monitor, ( m, d ) => d.TransactionSerialNumber );
            var export = shell.ExportToString();
            export.ShouldNotBeNull();

            // The client number cannot prove that the client state is the one of the domain:
            // after a restart from an older snapshot, the domain can reach this number with another history.
            foreach( var clientNumber in new[] { 0, 1, current - 1, current, current + 5 } )
            {
                var e = await watcher.GetStartOrRestartEventAsync( TestHelper.Monitor, "D", clientNumber );
                e.JsonExport.ShouldBe( export, $"A client at n°{clientNumber} receives the full export (current is n°{current})." );
            }
        }
        await league.CloseAsync( TestHelper.Monitor );
    }

    [Test]
    public async Task a_dangerous_rollback_is_pushed_as_a_full_export_Async()
    {
        var store = BasicLeagueTests.CreateStore( nameof( a_dangerous_rollback_is_pushed_as_a_full_export_Async ) );
        var league = await ObservableLeague.LoadAsync( TestHelper.Monitor, store );
        league.ShouldNotBeNull();
        var loader = await CreateDomainAsync( league, "D", skipTransactionCount: 2 );
        using var watcher = new TestWatcher( league );

        await using( var shell = await loader.LoadAsync<Model.School>( TestHelper.Monitor ) )
        {
            shell.ShouldNotBeNull();
            await watcher.StartOrRestartWatchAsync( TestHelper.Monitor, "D", 0 );
            for( int i = 0; i < 4; ++i )
            {
                await shell.ModifyThrowAsync( TestHelper.Monitor, ( m, d ) => d.Root.Persons.Add( new Model.Person() { FirstName = $"P{i}" } ) );
            }
            await watcher.WaitForCountAsync( 5 );

            var rollback = await shell.TryModifyAsync( TestHelper.Monitor, ( m, d ) =>
            {
                d.Root.Persons.Clear();
                throw new Exception( "Failing transaction. This is a test exception." );
            }, considerRolledbackAsFailure: false );
            rollback.RollbackedInfo.ShouldNotBeNull().IsDangerousRollback.ShouldBeTrue();

            // The clients have a state that the domain lost: they must receive the restored state.
            await watcher.WaitForCountAsync( 6 );
            watcher.Events.Last().JsonExport.ShouldBe( shell.ExportToString() );
        }
        await league.CloseAsync( TestHelper.Monitor );
    }
}
