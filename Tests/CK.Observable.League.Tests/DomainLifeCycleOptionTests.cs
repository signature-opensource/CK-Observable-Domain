using CK.Core;
using CK.Text;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.League.Tests
{
    public class DomainLifeCycleOptionTests
    {
        [SetUp]
        public void Setup()
        {
            GC.Collect();
            GC.WaitForFullGCComplete();
        }

        [Test]
        public async Task always_option_keeps_the_domain_in_memory()
        {
            var store = BasicLeagueTests.CreateStore( nameof( always_option_keeps_the_domain_in_memory ) );
            var league = await ObservableLeague.LoadAsync( TestHelper.Monitor, store );
            Debug.Assert( league != null );

            await league.Coordinator.ModifyThrowAsync( TestHelper.Monitor, ( m, d ) =>
            {
                var alwaysLoaded = d.Root.CreateDomain( "AlwaysLoaded", typeof( Model.School ).AssemblyQualifiedName! );
                alwaysLoaded.Options = alwaysLoaded.Options.SetLifeCycleOption( DomainLifeCycleOption.Always );
            } );

            var loader = league["AlwaysLoaded"];
            Debug.Assert( loader != null );

            loader.IsLoaded.Should().BeTrue( "The domain is kept alive." );

            var tr = await league.Coordinator.ModifyThrowAsync( TestHelper.Monitor, ( m, d ) =>
            {
                Domain loaded = d.Root.Domains["AlwaysLoaded"];
                loaded.Options = loaded.Options.SetLifeCycleOption( DomainLifeCycleOption.Default );
            } );
            var domainError = await tr.DomainPostActionsError;
            domainError.Should().BeNull();
            loader.IsLoaded.Should().BeFalse( "The domain is no more alive since its LoadOption is Default and there is no active timed events." );
        }

        static bool OnTimerCalled = false;

        // The SafeEventHandler must be a static or a method of a IDestroyableObject.
        static void OnTimer( object sender, ObservableReminderEventArgs arg )
        {
            OnTimerCalled = true;
            arg.Monitor.Info( $"Reminder fired. Tag: '{arg.Reminder.Tag}'." );
        }

        [Test]
        public async Task never_option_unloads_the_domain_even_if_there_is_active_timed_events()
        {
            var store = BasicLeagueTests.CreateStore( nameof( never_option_unloads_the_domain_even_if_there_is_active_timed_events ) );
            var league = await ObservableLeague.LoadAsync( TestHelper.Monitor, store );
            Debug.Assert( league != null );

            OnTimerCalled = false;

            await league.Coordinator.ModifyThrowAsync( TestHelper.Monitor, ( m, d ) =>
            {
                var alwaysLoaded = d.Root.CreateDomain( "NeverLoaded", typeof( Model.School ).AssemblyQualifiedName! );
                alwaysLoaded.Options = alwaysLoaded.Options.SetLifeCycleOption( DomainLifeCycleOption.Never );
            } );

            var loader = league["NeverLoaded"];
            Debug.Assert( loader != null );

            loader.IsLoaded.Should().BeFalse( "The domain is NEVER loaded." );

            await using( var shell = await loader.LoadAsync( TestHelper.Monitor, startTimer: true ) )
            {
                Debug.Assert( shell != null );
                await shell.ModifyThrowAsync( TestHelper.Monitor, ( m, d ) =>
                {
                    d.TimeManager.IsRunning.Should().BeTrue();
                    d.TimeManager.Remind( DateTime.UtcNow.AddMilliseconds( 100 ), OnTimer, null, null );
                } );
            }
            loader.IsLoaded.Should().BeFalse( "The domain is not alive." );
            await Task.Delay( 200 );
            OnTimerCalled.Should().BeFalse( "The reminder planned in 100 ms has not fired, even after 200 ms." );
            loader.IsLoaded.Should().BeFalse( "The domain is obviously not alive." );

            await using( var shell = await loader.LoadAsync( TestHelper.Monitor ) )
            {
            }
            OnTimerCalled.Should().BeTrue( "Loading the domain triggered the reminder." );
        }

        [Test]
        public async Task default_option_is_to_keep_the_domain_in_memory_as_long_as_there_is_active_timed_events()
        {
            var store = BasicLeagueTests.CreateStore( nameof( default_option_is_to_keep_the_domain_in_memory_as_long_as_there_is_active_timed_events ) );
            var league = await ObservableLeague.LoadAsync( TestHelper.Monitor, store );
            Debug.Assert( league != null );

            OnTimerCalled = false;

            await league.Coordinator.ModifyThrowAsync( TestHelper.Monitor, ( m, d ) =>
            {
                var alwaysLoaded = d.Root.CreateDomain( "DefaultLoaded", typeof( Model.School ).AssemblyQualifiedName! );
            } );

            var loader = league["DefaultLoaded"];
            Debug.Assert( loader != null );

            loader.IsLoaded.Should().BeFalse( "The domain has been unloaded since there is no active timed events." );

            await using( var shell = await loader.LoadAsync( TestHelper.Monitor, startTimer: true ) )
            {
                Debug.Assert( shell != null );
                await shell.ModifyThrowAsync( TestHelper.Monitor, ( m, d ) =>
                {
                    d.TimeManager.IsRunning.Should().BeTrue();
                    d.TimeManager.AllObservableTimedEvents.Should().BeEmpty();

                    d.TimeManager.Remind( DateTime.UtcNow.AddMilliseconds( 100 ), OnTimer, null, null );

                    d.TimeManager.AllObservableTimedEvents.Should().HaveCount( 1 );
                    d.TimeManager.Timers.Should().BeEmpty();
                    d.TimeManager.Reminders.Should().HaveCount( 1 );
                } );
            }

            OnTimerCalled.Should().BeFalse( "The reminder has not fired yet. (1)" );
            loader.IsLoaded.Should().BeTrue( "An active timed event keep the domain in memory. (1)" );

            await Task.Delay( 10 );

            OnTimerCalled.Should().BeFalse( "The reminder has not fired yet. (2)" );
            loader.IsLoaded.Should().BeTrue( "An active timed event keep the domain in memory. (2)" );

            await Task.Delay( 250 );
            OnTimerCalled.Should().BeTrue( "The reminder has eventually fired." );
            loader.IsLoaded.Should().BeFalse( "The reminder fired: there is no more need to keep the domain in memory." );
        }
    }
}
