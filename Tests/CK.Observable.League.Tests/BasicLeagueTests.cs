using CK.Text;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.League.Tests
{
    public class BasicLeagueTests
    {
        readonly static NormalizedPath TestFolder = TestHelper.TestProjectFolder.AppendPart( "TestStores" );

        static DirectoryStreamStore CreateStore( string name )
        {
            var p = TestFolder.AppendPart( name );
            TestHelper.CleanupFolder( p );
            return new DirectoryStreamStore( p );
        }

        [Test]
        public async Task empty_league_serialization()
        {
            var store = CreateStore( nameof( empty_league_serialization ) );
            var league = await ObservableLeague.LoadAsync( TestHelper.Monitor, store );
            await league.CloseAsync( TestHelper.Monitor );
            var league2 = await ObservableLeague.LoadAsync( TestHelper.Monitor, store );
            await league2.CloseAsync( TestHelper.Monitor );
        }

        [Test]
        public async Task one_domain_league_serialization()
        {
            var store = CreateStore( nameof( empty_league_serialization ) );
            var league = await ObservableLeague.LoadAsync( TestHelper.Monitor, store );
            await league.ModifyCoordinatorAsync( TestHelper.Monitor, ( m, d ) => d.Root.CreateDomain( "First", new [] { typeof(Model.School).AssemblyQualifiedName } ) );
            await using( var f = await league.Find( "First" ).LoadAsync( TestHelper.Monitor ) )
            {
                await f.ModifyAsync( TestHelper.Monitor, ( m, d ) => ((IObservableDomain<Model.School>)d).Root.Persons.Add( new Model.Person() { FirstName = "A" } ) );
            }
            await league.CloseAsync( TestHelper.Monitor );

            var league2 = await ObservableLeague.LoadAsync( TestHelper.Monitor, store );
            league2.Read( TestHelper.Monitor, ( m, d ) => d.Root.Domains.Count ).Should().Be( 1 );
            var first2 = league2.Find( "First" );
            first2.Should().NotBeNull();
            await using( var f = await first2.LoadAsync( TestHelper.Monitor ) )
            {
                f.Read( TestHelper.Monitor, ( m, d ) => ((Model.School)d.AllRoots[0]).Persons[0].FirstName ).Should().Be( "A" );
            }
            await league2.CloseAsync( TestHelper.Monitor );
        }

        [Test]
        public async Task simple_play_with_first_domain()
        {
            var store = CreateStore( nameof( simple_play_with_first_domain ) );
            var league = await ObservableLeague.LoadAsync( TestHelper.Monitor, store );

            league.Find( "FirstDomain" ).Should().BeNull();
            await league.ModifyCoordinatorAsync( TestHelper.Monitor, ( m, d ) => d.Root.CreateDomain( "FirstDomain", null ) );

            var loader = league.Find( "FirstDomain" );
            loader.Should().NotBeNull();
            loader.IsDestroyed.Should().BeFalse();
            loader.IsLoaded.Should().BeFalse();
            await using( var shell = await loader.LoadAsync( TestHelper.Monitor ) )
            {
                shell.Should().BeSameAs( loader, "Since the ActivityMonitor is the same, the loader is the shell." );

                await league.CloseAsync( TestHelper.Monitor );

                var afterClosing = league.Find( "FirstDomain" );
                afterClosing.Should().BeNull( "league has been closed." );
                (await loader.LoadAsync( TestHelper.Monitor )).Should().BeNull( "league has been closed." );

                await shell.ModifyAsync( TestHelper.Monitor, ( m, d ) => new Model.Person() { FirstName = "X", LastName = "Y", Age = 22 } );
            }

            var league2 = await ObservableLeague.LoadAsync( TestHelper.Monitor, store );
            var loader2 = league2.Find( "FirstDomain" );
            await using( var shell2 = await loader2.LoadAsync( TestHelper.Monitor ) )
            {
                shell2.Read( TestHelper.Monitor, ( m, d ) =>
                {
                    var p = (Model.Person)d.AllObjects.Single();
                    p.FirstName.Should().Be( "X" );
                    p.LastName.Should().Be( "Y" );
                    p.Age.Should().Be( 22 );
                } );
            }

        }
    }
}
