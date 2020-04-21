using CK.Core;
using CK.Text;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.League.Tests
{
    public class PreLoadOptionTests
    {
        [Test]
        public async Task always_option_keeps_the_domain_in_memory()
        {
            var store = BasicLeagueTests.CreateStore( nameof( always_option_keeps_the_domain_in_memory ) );
            var league = await ObservableLeague.LoadAsync( TestHelper.Monitor, store );

            await league.Coordinator.ModifyThrowAsync( TestHelper.Monitor, ( m, d ) =>
            {
                var alwaysLoaded = d.Root.CreateDomain( "AlwaysLoaded", typeof( Model.School ).AssemblyQualifiedName );
                alwaysLoaded.Options = alwaysLoaded.Options.SetLoadOption( DomainPreLoadOption.Always );
            } );

            league["AlwaysLoaded"].IsLoaded.Should().BeTrue( "The domain is kept alive." );

            await league.Coordinator.ModifyThrowAsync( TestHelper.Monitor, ( m, d ) =>
            {
                Domain loaded = d.Root.Domains["AlwaysLoaded"];
                loaded.Options = loaded.Options.SetLoadOption( DomainPreLoadOption.Default );
            } );

            league["AlwaysLoaded"].IsLoaded.Should().BeFalse( "The domain is no more alive since its LoadOption is Default and there is no active timed events." );
        }

        static bool OnTimerCalled = false;

        // The SafeEventHandler must be a static or a method of a IDisposableObject.
        static void OnTimer( object sender, ObservableReminderEventArgs arg )
        {
            OnTimerCalled = true;
        }

        [Test]
        public async Task never_option_unloads_the_domain_even_if_there_is_active_timed_events()
        {
            var store = BasicLeagueTests.CreateStore( nameof( never_option_unloads_the_domain_even_if_there_is_active_timed_events ) );
            var league = await ObservableLeague.LoadAsync( TestHelper.Monitor, store );
            OnTimerCalled = false;

            await league.Coordinator.ModifyThrowAsync( TestHelper.Monitor, ( m, d ) =>
            {
                var alwaysLoaded = d.Root.CreateDomain( "NeverLoaded", typeof( Model.School ).AssemblyQualifiedName );
                alwaysLoaded.Options = alwaysLoaded.Options.SetLoadOption( DomainPreLoadOption.Never );
            } );

            var loader = league["NeverLoaded"];

            loader.IsLoaded.Should().BeFalse( "The domain is NEVER loaded." );

            await using( var shell = await loader.LoadAsync( TestHelper.Monitor ) )
            {
                await shell.ModifyThrowAsync( TestHelper.Monitor, ( m, d ) =>
                {
                    d.TimeManager.Remind( DateTime.UtcNow.AddMilliseconds( 100 ), OnTimer, null );
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
            OnTimerCalled = false;

            await league.Coordinator.ModifyThrowAsync( TestHelper.Monitor, ( m, d ) =>
            {
                var alwaysLoaded = d.Root.CreateDomain( "DefaultLoaded", typeof( Model.School ).AssemblyQualifiedName );
            } );

            var loader = league["DefaultLoaded"];

            loader.IsLoaded.Should().BeFalse( "The domain has been unloaded since there is no active timed events." );


            await using( var shell = await loader.LoadAsync( TestHelper.Monitor ) )
            {
                await shell.ModifyThrowAsync( TestHelper.Monitor, ( m, d ) =>
                {
                    d.TimeManager.AllObservableTimedEvents.Should().BeEmpty();

                    d.TimeManager.Remind( DateTime.UtcNow.AddMilliseconds( 400 ), OnTimer, null );

                    d.TimeManager.AllObservableTimedEvents.Should().HaveCount( 1 );
                    d.TimeManager.Timers.Should().BeEmpty();
                    d.TimeManager.Reminders.Should().HaveCount( 1 );
                } );
            }

            loader.IsLoaded.Should().BeTrue( "An active timed event keep the domain in memory." );
            await Task.Delay( 10 );
            loader.IsLoaded.Should().BeTrue( "An active timed event keep the domain in memory." );
            OnTimerCalled.Should().BeFalse( "The reminder has not fired yet." );

            await Task.Delay( 490 );
            OnTimerCalled.Should().BeTrue( "The reminder has eventually fired." );
            loader.IsLoaded.Should().BeFalse( "The reminder fired: there is no more need to keep the domain in memory." );
        }
    }
}
