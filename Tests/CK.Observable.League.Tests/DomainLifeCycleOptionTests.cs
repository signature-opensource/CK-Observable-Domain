using CK.Core;
using Shouldly;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.League.Tests;

public class DomainLifeCycleOptionTests
{
    [SetUp]
    public void Setup()
    {
        GC.Collect();
        GC.WaitForFullGCComplete();
    }

    [Test]
    public async Task always_option_keeps_the_domain_in_memory_Async()
    {
        var store = BasicLeagueTests.CreateStore( nameof( always_option_keeps_the_domain_in_memory_Async ) );
        var league = await ObservableLeague.LoadAsync( TestHelper.Monitor, store );
        Debug.Assert( league != null );

        await league.Coordinator.ModifyThrowAsync( TestHelper.Monitor, ( m, d ) =>
        {
            var alwaysLoaded = d.Root.CreateDomain( "AlwaysLoaded", typeof( Model.School ).AssemblyQualifiedName! );
            alwaysLoaded.Options = alwaysLoaded.Options.SetLifeCycleOption( DomainLifeCycleOption.Always );
        }, waitForDomainPostActionsCompletion: true );

        var loader = league["AlwaysLoaded"];
        Debug.Assert( loader != null );

        // On the CI, the configuration from the Coordinator takes some time to be applied...
        await Task.Delay( 200 );

        loader.IsLoaded.ShouldBeTrue( "The domain is kept alive." );

        var tr = await league.Coordinator.ModifyThrowAsync( TestHelper.Monitor, ( m, d ) =>
        {
            ODomain loaded = d.Root.Domains["AlwaysLoaded"];
            loaded.Options = loaded.Options.SetLifeCycleOption( DomainLifeCycleOption.HonorTimedEvent );
        }, waitForDomainPostActionsCompletion: true );
        var domainError = await tr.DomainPostActionsError;
        domainError.ShouldBeNull();
        loader.IsLoaded.ShouldBeFalse( "The domain is no more alive since its LoadOption is Default and there is no active timed events." );
    }

    static bool OnTimerCalled = false;

    // The SafeEventHandler must be a static or a method of a IDestroyableObject.
    static void OnTimer( object sender, ObservableReminderEventArgs arg )
    {
        OnTimerCalled = true;
        arg.Monitor.Info( $"Reminder fired. Tag: '{arg.Reminder.Tag}'." );
    }

    [Test]
    public async Task never_option_unloads_the_domain_even_if_there_is_active_timed_events_Async()
    {
        var store = BasicLeagueTests.CreateStore( nameof( never_option_unloads_the_domain_even_if_there_is_active_timed_events_Async ) );
        var league = await ObservableLeague.LoadAsync( TestHelper.Monitor, store );
        Debug.Assert( league != null );

        OnTimerCalled = false;

        await league.Coordinator.ModifyThrowAsync( TestHelper.Monitor, ( m, d ) =>
        {
            var alwaysLoaded = d.Root.CreateDomain( "NeverLoaded", typeof( Model.School ).AssemblyQualifiedName! );
            alwaysLoaded.Options = alwaysLoaded.Options.SetLifeCycleOption( DomainLifeCycleOption.Never );
        }, waitForDomainPostActionsCompletion: true );

        var loader = league["NeverLoaded"];
        Debug.Assert( loader != null );

        loader.IsLoaded.ShouldBeFalse( "The domain is NEVER loaded." );

        await using( var shell = await loader.LoadAsync( TestHelper.Monitor, startTimer: true ) )
        {
            Debug.Assert( shell != null );
            await shell.ModifyThrowAsync( TestHelper.Monitor, ( m, d ) =>
            {
                d.TimeManager.IsRunning.ShouldBeTrue();
                d.TimeManager.Remind( DateTime.UtcNow.AddMilliseconds( 100 ), OnTimer, null, null );
            } );
        }
        loader.IsLoaded.ShouldBeFalse( "The domain is not alive." );
        await Task.Delay( 200 );
        OnTimerCalled.ShouldBeFalse( "The reminder planned in 100 ms has not fired, even after 200 ms." );
        loader.IsLoaded.ShouldBeFalse( "The domain is obviously not alive." );

        await using( var shell = await loader.LoadAsync( TestHelper.Monitor ) )
        {
        }
        OnTimerCalled.ShouldBeTrue( "Loading the domain triggered the reminder." );
    }
}
