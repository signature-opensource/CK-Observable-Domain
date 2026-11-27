using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CK.BinarySerialization;
using CK.Core;
using NUnit.Framework;
using Shouldly;
using static CK.Observable.PersistentDomainDriverTests.DriverTestKit;

namespace CK.Observable.PersistentDomainDriverTests;

/// <summary>The event log of the timer tests. The tests do not run in parallel.</summary>
public static class TimerLog
{
    public static readonly ConcurrentQueue<string> Entries = new ConcurrentQueue<string>();
}

/// <summary>A driver that starts the timer of its domain at the end of the initial transaction.</summary>
public sealed class TimerDriver : PersistentDomainDriver<TimerDriver>
{
    public TimerDriver()
        : base( "TimerDomain", autoStartTimer: true )
    {
    }

    protected override void PreConfigure( IActivityMonitor monitor, ObservableDomain domain )
    {
        TimerLog.Entries.Enqueue( "PreConfigure" );
        domain.CreateSingleton<OTestSingleton>();
    }
}

/// <summary>A driver on the folder of <see cref="TimerDriver"/> that does not start the timer.</summary>
public sealed class StoppedTimerDriver : PersistentDomainDriver<StoppedTimerDriver>
{
    public StoppedTimerDriver()
        : base( "TimerDomain", autoStartTimer: false )
    {
    }
}

/// <summary>A configurator that refuses the configuration of a <see cref="TimerDriver"/>: the initial transaction fails.</summary>
public sealed class RefusingTimerConfigurator : IObservableDomainConfigurator<TimerDriver>
{
    public bool ConfigureDomain( IActivityMonitor monitor, ObservableDomain observableDomain ) => false;
}

/// <summary>The target of a reminder. The handler writes to <see cref="TimerLog"/>.</summary>
[SerializationVersion( 0 )]
public sealed class OTicker : ObservableObject
{
    public OTicker()
    {
    }

    OTicker( IBinaryDeserializer d, ITypeReadInfo info )
        : base( Sliced.Instance )
    {
        Count = d.Reader.ReadInt32();
    }

    public static void Write( IBinarySerializer s, in OTicker o )
    {
        s.Writer.Write( o.Count );
    }

    public int Count { get; set; }

    public void OnElapsed( object sender, ObservableReminderEventArgs e )
    {
        Count++;
        TimerLog.Entries.Enqueue( "Elapsed" );
    }
}

/// <summary>
/// A configurator that adds a domain post action to the initial transaction. The action ends when the task ends.
/// </summary>
public sealed class DomainPostActionConfigurator : IObservableDomainConfigurator<TestDriver>
{
    readonly Task _action;

    public DomainPostActionConfigurator( Task action )
    {
        _action = action;
    }

    public bool ConfigureDomain( IActivityMonitor monitor, ObservableDomain observableDomain )
    {
        EventHandler<TransactionDoneEventArgs>? handler = null;
        handler = ( sender, e ) =>
        {
            observableDomain.TransactionDone -= handler;
            e.DomainPostActions.Add( ctx => _action );
        };
        observableDomain.TransactionDone += handler;
        return true;
    }
}

[TestFixture]
public class StartSequenceTests
{
    static readonly IEnumerable<IObservableDomainConfigurator<TestDriver>> _noConfigurators = Enumerable.Empty<IObservableDomainConfigurator<TestDriver>>();
    static readonly IEnumerable<IPersistentDomainLoadHandler<TestDriver>> _noHandlers = Enumerable.Empty<IPersistentDomainLoadHandler<TestDriver>>();
    static readonly IEnumerable<IObservableDomainConfigurator<TimerDriver>> _noTimerConfigurators = Enumerable.Empty<IObservableDomainConfigurator<TimerDriver>>();
    static readonly IEnumerable<IPersistentDomainLoadHandler<TimerDriver>> _noTimerHandlers = Enumerable.Empty<IPersistentDomainLoadHandler<TimerDriver>>();

    // Before the fix, the first start of a process changed the folder in each run (a cold JIT). Later starts changed it less often.
    const int _iterations = 5;

    // R3: the files hold an overdue reminder and a running timer. The start fails in a configurator. No file may change.
    // The reminder must not run before PreConfigure: the load stops the timer, and the initial transaction starts it as its last step.
    [Test]
    public async Task a_failed_start_with_an_overdue_reminder_changes_no_file()
    {
        await RunOverdueReminderAsync( withRecovery: false );
    }

    // The same with a recovery: the newest file is damaged and FallbackGenerationCount is 1. No mark may be applied.
    [Test]
    public async Task a_failed_start_after_a_recovery_with_an_overdue_reminder_changes_no_file()
    {
        await RunOverdueReminderAsync( withRecovery: true );
    }

    static async Task RunOverdueReminderAsync( bool withRecovery )
    {
        int valid = 0;
        var failures = new StringBuilder();
        for( int i = 0; i < _iterations; ++i )
        {
            var r = await RunOverdueReminderIterationAsync( withRecovery );
            if( !r.Valid ) continue;
            ++valid;
            if( r.Failure != null ) failures.AppendLine( $"#{i}: {r.Failure}" );
        }
        valid.ShouldBeGreaterThan( 0 );
        failures.ToString().ShouldBeEmpty();
    }

    static async Task<(bool Valid, string? Failure)> RunOverdueReminderIterationAsync( bool withRecovery )
    {
        var monitor = new ActivityMonitor();
        var store = TestStore.NewDirectory();
        var extra = withRecovery
                    ? new Dictionary<string, string?> { ["CK-ObservableDomain:Domains:TimerDomain:FallbackGenerationCount"] = "1" }
                    : null;
        var services = NewServices( store, "TimerDomain", extra: extra );

        TimerLog.Entries.Clear();
        var driver1 = new TimerDriver();
        await driver1.DoHostStartAsync( monitor, services, _noTimerConfigurators, _noTimerHandlers );
        var due = DateTime.UtcNow.AddMilliseconds( 400 );
        await driver1.Domain.ModifyThrowAsync( monitor, () =>
        {
            var ticker = new OTicker();
            var reminder = new ObservableReminder( due );
            reminder.Elapsed += ticker.OnElapsed;
        }, waitForDomainPostActionsCompletion: true );
        // One more file: with the recovery, the damaged newest file is this one, and the fallback loads the reminder.
        await driver1.Domain.ModifyThrowAsync( monitor, () =>
        {
            driver1.Domain.AllObjects.Items.OfType<OTestSingleton>().Single().Marker = "after the reminder";
        }, waitForDomainPostActionsCompletion: true );
        await driver1.DoHostStopAsync( monitor );
        driver1.Domain.Dispose();
        // The reminder must not run before the stop: the files must hold an active reminder.
        bool valid = !TimerLog.Entries.Contains( "Elapsed" );
        if( withRecovery )
        {
            ZeroFill( OdFiles( store )[^1] );
        }
        var wait = due - DateTime.UtcNow + TimeSpan.FromMilliseconds( 200 );
        if( wait > TimeSpan.Zero ) await Task.Delay( wait );

        var before = Content( store );
        TimerLog.Entries.Clear();
        var driver2 = new TimerDriver();
        await Should.ThrowAsync<Exception>( () => driver2.DoHostStartAsync( monitor, services, new[] { new RefusingTimerConfigurator() }, _noTimerHandlers ) );
        // A late commit after the failed start must not write either.
        await Task.Delay( 100 );
        var after = Content( store );
        var entries = TimerLog.Entries.ToList();
        int elapsed = entries.IndexOf( "Elapsed" );
        int preConfigure = entries.IndexOf( "PreConfigure" );
        if( !SameContent( before, after ) )
        {
            return (valid, $"The folder changed. Before: [{Describe( before )}] After: [{Describe( after )}] Log: {string.Join( ",", entries )}");
        }
        if( elapsed >= 0 && (preConfigure < 0 || elapsed < preConfigure) )
        {
            return (valid, $"The reminder ran before PreConfigure. Log: {string.Join( ",", entries )}");
        }
        return (valid, null);
    }

    // D2: the load stops the timer. Only a driver with AutoStartTimer starts it again: the timer state of the file is not restored.
    [Test]
    public async Task the_timer_runs_after_the_start_only_with_AutoStartTimer()
    {
        var monitor = new ActivityMonitor();
        var store = TestStore.NewDirectory();
        var services = NewServices( store, "TimerDomain" );

        var timer = new TimerDriver();
        await timer.DoHostStartAsync( monitor, services, _noTimerConfigurators, _noTimerHandlers );
        timer.Domain.TimeManager.IsRunning.ShouldBeTrue();
        await timer.DoHostStopAsync( monitor );
        timer.Domain.Dispose();

        // The newest file holds a running timer.
        var stopped = new StoppedTimerDriver();
        await stopped.DoHostStartAsync( monitor, services, Enumerable.Empty<IObservableDomainConfigurator<StoppedTimerDriver>>(), Enumerable.Empty<IPersistentDomainLoadHandler<StoppedTimerDriver>>() );
        stopped.Domain.TimeManager.IsRunning.ShouldBeFalse();
        await stopped.DoHostStopAsync( monitor );
        stopped.Domain.Dispose();

        var again = new TimerDriver();
        await again.DoHostStartAsync( monitor, services, _noTimerConfigurators, _noTimerHandlers );
        again.Domain.TimeManager.IsRunning.ShouldBeTrue();
        await again.DoHostStopAsync( monitor );
        again.Domain.Dispose();
    }

    // R8: the start does not wait for the domain post actions of the initial transaction.
    [Test]
    public async Task a_domain_post_action_that_never_ends_does_not_block_the_start()
    {
        var monitor = new ActivityMonitor();
        var store = TestStore.NewDirectory();
        var services = NewServices( store, "TestDomain" );
        var release = new TaskCompletionSource( TaskCreationOptions.RunContinuationsAsynchronously );
        var driver = new TestDriver();
        try
        {
            var start = driver.DoHostStartAsync( monitor, services, new[] { new DomainPostActionConfigurator( release.Task ) }, _noHandlers );
            (await Task.WhenAny( start, Task.Delay( 5000 ) )).ShouldBe( start, "The start waits for the domain post action." );
            await start;
            OdFiles( store ).Length.ShouldBe( 1 );
        }
        finally
        {
            release.SetResult();
        }
        await driver.DoHostStopAsync( monitor );
        driver.Domain.Dispose();
    }

    // R8: the first save fails (all the names that the store can use are directories). The start fails and releases
    // the lock, also when a domain post action of the initial transaction never ends.
    [Test]
    public async Task a_failed_start_with_a_domain_post_action_that_never_ends_releases_the_lock()
    {
        var monitor = new ActivityMonitor();
        var store = TestStore.NewDirectory();
        var services = NewServices( store, "TestDomain" );
        for( int g = 1; g <= 101; ++g )
        {
            Directory.CreateDirectory( Path.Combine( store, $"TestDomain.{g:D19}.od" ) );
        }
        var release = new TaskCompletionSource( TaskCreationOptions.RunContinuationsAsynchronously );
        var driver = new TestDriver();
        try
        {
            var start = driver.DoHostStartAsync( monitor, services, new[] { new DomainPostActionConfigurator( release.Task ) }, _noHandlers );
            (await Task.WhenAny( start, Task.Delay( 10_000 ) )).ShouldBe( start, "The failed start waits for the domain post action." );
            await Should.ThrowAsync<InvalidOperationException>( () => start );
            // The saver is closed: a save post action that runs late writes nothing.
            var saver = driver.Saver.ShouldNotBeNull();
            saver.Offer( new PendingSnapshot( 1000, DateTime.UtcNow, new byte[] { 1, 2, 3 } ) );
            (await saver.SaveAsync( monitor )).ShouldBeFalse();
            using var folderLock = FolderLock.TryAcquire( GenerationStore.GetLockFilePath( store, "TestDomain" ) );
            folderLock.ShouldNotBeNull( "The failed start must release the lock." );
            Directory.GetFiles( store ).ShouldBe( new[] { GenerationStore.GetLockFilePath( store, "TestDomain" ) } );
        }
        finally
        {
            release.SetResult();
        }
    }
}
