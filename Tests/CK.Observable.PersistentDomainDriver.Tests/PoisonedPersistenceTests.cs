using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CK.BinarySerialization;
using CK.Core;
using NUnit.Framework;
using Shouldly;
using static CK.Observable.PersistentDomainDriverTests.DriverTestKit;

namespace CK.Observable.PersistentDomainDriverTests;

/// <summary>
/// An object whose deserialization or serialization fails when a static test flag is set.
/// The tests do not run in parallel, and each test clears the flags at its end.
/// </summary>
[SerializationVersion( 0 )]
public sealed class OBomb : ObservableObject
{
    public static volatile bool ThrowOnRead;
    public static volatile bool ThrowOnWrite;

    public OBomb()
    {
    }

    OBomb( IBinaryDeserializer d, ITypeReadInfo info )
        : base( Sliced.Instance )
    {
        Value = d.Reader.ReadInt32();
        if( ThrowOnRead ) throw new InvalidOperationException( "OBomb: the deserialization fails (test flag)." );
    }

    public static void Write( IBinarySerializer s, in OBomb o )
    {
        if( ThrowOnWrite ) throw new InvalidOperationException( "OBomb: the serialization fails (test flag)." );
        s.Writer.Write( o.Value );
    }

    public int Value { get; set; }
}

/// <summary>
/// D3 (R5): after a failed restore of the memory snapshot, or after a snapshot creation that throws, the memory
/// state cannot be trusted. The persistence then refuses every save until the next start.
/// </summary>
[TestFixture]
public class PoisonedPersistenceTests
{
    static readonly IEnumerable<IObservableDomainConfigurator<TestDriver>> _noConfigurators = Enumerable.Empty<IObservableDomainConfigurator<TestDriver>>();
    static readonly IEnumerable<IPersistentDomainLoadHandler<TestDriver>> _noHandlers = Enumerable.Empty<IPersistentDomainLoadHandler<TestDriver>>();

    [TearDown]
    public void ClearFlags()
    {
        OBomb.ThrowOnRead = false;
        OBomb.ThrowOnWrite = false;
    }

    // R5a: a transaction fails, and the restore of the memory snapshot throws half-way. The domain is half-loaded.
    [Test]
    public async Task after_a_failed_rollback_the_persistence_writes_nothing_and_a_restart_loads_the_good_state()
    {
        var monitor = new ActivityMonitor();
        var store = TestStore.NewDirectory();
        var services = NewServices( store, "TestDomain" );
        var driver = new TestDriver();
        await driver.DoHostStartAsync( monitor, services, _noConfigurators, _noHandlers );
        using var fatal = monitor.CollectEntries( out var fatals, LogLevelFilter.Fatal );
        try
        {
            var goodState = await CommitGoodStateAsync( monitor, driver );
            var before = Content( store );
            driver.IsPersistenceHealthy.ShouldBeTrue();

            OBomb.ThrowOnRead = true;
            var failed = await ModifyAsync( monitor, driver, () =>
            {
                driver.Domain.AllObjects.Items.OfType<OTestSingleton>().Single().Marker = "never committed";
                throw new InvalidOperationException( "The transaction fails (test)." );
            } );
            OBomb.ThrowOnRead = false;
            failed.ClientError.ShouldNotBeNull( "The restore must fail." );

            driver.IsPersistenceHealthy.ShouldBeFalse();
            await AssertNothingIsWrittenAsync( monitor, driver, store, before );
            fatals.Count( e => e.MaskedLevel == LogLevel.Fatal && e.Text.Contains( "writes no snapshot file" ) ).ShouldBe( 1 );

            await RestartAndCheckAsync( monitor, services, goodState );
        }
        finally
        {
            OBomb.ThrowOnRead = false;
            await driver.DoHostStopAsync( monitor );
        }
    }

    // R5b: d.Save throws inside CreateSnapshot during a commit. The memory snapshot is half-written, and a later
    // rollback reads it. With a marker of the same length, the bytes line up by chance. With a longer marker, they do not.
    [TestCase( "half" )]
    [TestCase( "a marker that is longer than the good one" )]
    public async Task after_a_failed_snapshot_creation_the_persistence_writes_nothing_and_a_restart_loads_the_good_state( string failedMarker )
    {
        var monitor = new ActivityMonitor();
        var store = TestStore.NewDirectory();
        var services = NewServices( store, "TestDomain" );
        var driver = new TestDriver();
        await driver.DoHostStartAsync( monitor, services, _noConfigurators, _noHandlers );
        try
        {
            var goodState = await CommitGoodStateAsync( monitor, driver );
            var before = Content( store );

            OBomb.ThrowOnWrite = true;
            var halfCommit = await ModifyAsync( monitor, driver, () =>
            {
                driver.Domain.AllObjects.Items.OfType<OTestSingleton>().Single().Marker = failedMarker;
            } );
            OBomb.ThrowOnWrite = false;
            halfCommit.ClientError.ShouldNotBeNull( "The snapshot creation must fail." );
            driver.IsPersistenceHealthy.ShouldBeFalse();

            // A transaction fails: the rollback reads the half-written memory snapshot.
            await ModifyAsync( monitor, driver, () => throw new InvalidOperationException( "The transaction fails (test)." ) );

            await AssertNothingIsWrittenAsync( monitor, driver, store, before );
            await RestartAndCheckAsync( monitor, services, goodState );
        }
        finally
        {
            OBomb.ThrowOnWrite = false;
            await driver.DoHostStopAsync( monitor );
        }
    }

    // A real commit, the timer and the stop write nothing. The folder stays as it was before the failure.
    static async Task AssertNothingIsWrittenAsync( IActivityMonitor monitor, TestDriver driver, string store, Dictionary<string, string> before )
    {
        var next = await ModifyAsync( monitor, driver, () =>
        {
            var c = driver.Domain.AllObjects.Items.OfType<OCounter>().FirstOrDefault();
            if( c != null ) c.Count = 8;
            else new OCounter { Count = 99 };
        } );
        next.Success.ShouldBeTrue();
        // The check of the commit and the retry of the timer come after 1 second.
        await Task.Delay( 1500 );
        SameContent( Content( store ), before ).ShouldBeTrue( "A poisoned persistence must write nothing." );

        await driver.DoHostStopAsync( monitor );
        SameContent( Content( store ), before ).ShouldBeTrue( "The stop of a poisoned persistence must write nothing." );
    }

    static async Task RestartAndCheckAsync( IActivityMonitor monitor, IServiceProvider services, string goodState )
    {
        var again = new TestDriver();
        await again.DoHostStartAsync( monitor, services, _noConfigurators, _noHandlers );
        try
        {
            again.IsPersistenceHealthy.ShouldBeTrue();
            again.Domain.Read( monitor, () => Describe( again.Domain ) ).ShouldBe( goodState );
        }
        finally
        {
            await again.DoHostStopAsync( monitor );
        }
    }

    // Commits the good state: the marker "good", an OBomb(3712) and an OCounter(7). Returns its description.
    static async Task<string> CommitGoodStateAsync( IActivityMonitor monitor, TestDriver driver )
    {
        await driver.Domain.ModifyThrowAsync( monitor, () =>
        {
            driver.Domain.AllObjects.Items.OfType<OTestSingleton>().Single().Marker = "good";
            new OBomb { Value = 3712 };
            new OCounter { Count = 7 };
        }, waitForDomainPostActionsCompletion: true );
        var state = driver.Domain.Read( monitor, () => Describe( driver.Domain ) );
        state.ShouldBe( "Count=3 [OTestSingleton(\"good\"), OBomb(3712), OCounter(7)]" );
        return state;
    }

    static Task<TransactionResult> ModifyAsync( IActivityMonitor monitor, TestDriver driver, Action action )
    {
        return driver.Domain.ModifyAsync( monitor, action,
                                          throwException: false,
                                          millisecondsTimeout: -1,
                                          considerRolledbackAsFailure: true,
                                          parallelDomainPostActions: true,
                                          waitForDomainPostActionsCompletion: true );
    }

    static string Describe( ObservableDomain d )
    {
        var objects = d.AllObjects.Items.Select( o => o switch
        {
            OTestSingleton s => $"OTestSingleton(\"{s.Marker}\")",
            OCounter c => $"OCounter({c.Count})",
            OBomb b => $"OBomb({b.Value})",
            _ => o.GetType().Name
        } );
        return $"Count={d.AllObjects.Count} [{string.Join( ", ", objects )}]";
    }
}
