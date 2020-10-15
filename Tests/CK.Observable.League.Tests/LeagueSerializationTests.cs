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
    public class LeagueSearializationTests
    {
        [Test]
        public void coordinator_serialization()
        {
            var d = new ObservableDomain<Coordinator>( TestHelper.Monitor, String.Empty );
            var services = new SimpleServiceContainer();
            services.Add<ObservableDomain>( new ObservableDomain<Coordinator>( TestHelper.Monitor, String.Empty ) );
            BinarySerializer.IdempotenceCheck( d.Root, services );
        }

        [Test]
        public async Task empty_league_serialization()
        {
            var store = BasicLeagueTests.CreateStore( nameof( empty_league_serialization ) );
            var league = await ObservableLeague.LoadAsync( TestHelper.Monitor, store );
            await league.CloseAsync( TestHelper.Monitor );
            var league2 = await ObservableLeague.LoadAsync( TestHelper.Monitor, store );
            await league2.CloseAsync( TestHelper.Monitor );
        }

        [Test]
        public async Task one_domain_league_serialization()
        {
            var store = BasicLeagueTests.CreateStore( nameof( empty_league_serialization ) );
            var league = await ObservableLeague.LoadAsync( TestHelper.Monitor, store );
            await league.Coordinator.ModifyAsync( TestHelper.Monitor, ( m, d ) => d.Root.CreateDomain( "First", typeof( Model.School ).AssemblyQualifiedName ) );
            // Using the non generic IObservableDomain.
            await using( var f = await league.Find( "First" ).LoadAsync( TestHelper.Monitor ) )
            {
                await f.ModifyAsync( TestHelper.Monitor, ( m, d ) => ((IObservableDomain<Model.School>)d).Root.Persons.Add( new Model.Person() { FirstName = "A" } ) );
            }
            await league.CloseAsync( TestHelper.Monitor );

            var league2 = await ObservableLeague.LoadAsync( TestHelper.Monitor, store );
            league2.Coordinator.Read( TestHelper.Monitor, ( m, d ) => d.Root.Domains.Count ).Should().Be( 1 );
            var first2 = league2.Find( "First" );
            first2.Should().NotBeNull();
            // Using the strongly typed IObservableDomain<T>.
            await using( var f = await first2.LoadAsync<Model.School>( TestHelper.Monitor ) )
            {
                f.Read( TestHelper.Monitor, ( m, d ) => d.Root.Persons[0].FirstName ).Should().Be( "A" );
            }
            await league2.CloseAsync( TestHelper.Monitor );
        }


    }

}
