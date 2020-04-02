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
            await league.Coordinator.ModifyAsync( TestHelper.Monitor, ( m, d ) => d.Root.CreateDomain( "First", typeof(Model.School).AssemblyQualifiedName ) );
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

        [Test]
        public async Task simple_play_with_first_domain()
        {
            var store = CreateStore( nameof( simple_play_with_first_domain ) );
            var league = await ObservableLeague.LoadAsync( TestHelper.Monitor, store );

            league.Find( "FirstDomain" ).Should().BeNull( "Empty store. Not created yet." );
            await league.Coordinator.ModifyAsync( TestHelper.Monitor, ( m, d ) => d.Root.CreateDomain( "FirstDomain", typeof(Model.School).AssemblyQualifiedName ) );

            var loader = league.Find( "FirstDomain" );
            loader.Should().NotBeNull();
            loader.IsDestroyed.Should().BeFalse();
            loader.IsLoaded.Should().BeFalse();
            await using( var shell = await loader.LoadAsync<Model.School>( TestHelper.Monitor ) )
            {
                shell.Should().BeSameAs( loader, "Since the ActivityMonitor is the same, the loader is the shell." );

                await league.CloseAsync( TestHelper.Monitor );

                var afterClosing = league.Find( "FirstDomain" );
                afterClosing.Should().BeNull( "league has been closed." );
                (await loader.LoadAsync( TestHelper.Monitor )).Should().BeNull( "league has been closed." );

                await shell.ModifyAsync( TestHelper.Monitor, ( m, d ) =>
                {
                    d.Root.Persons.Add( new Model.Person() { FirstName = "X", LastName = "Y", Age = 22 } );
                } );
            }

            var league2 = await ObservableLeague.LoadAsync( TestHelper.Monitor, store );
            var loader2 = league2.Find( "FirstDomain" );
            await using( var shell2 = await loader2.LoadAsync<Model.School>( TestHelper.Monitor ) )
            {
                shell2.Read( TestHelper.Monitor, ( m, d ) =>
                {
                    var p = d.Root.Persons[0];
                    p.FirstName.Should().Be( "X" );
                    p.LastName.Should().Be( "Y" );
                    p.Age.Should().Be( 22 );
                } );
                // We dispose the Domain in the Coordinator domain: it is now marked as Destroyed.
                await league2.Coordinator.ModifyAsync( TestHelper.Monitor, ( m, d ) =>
                {
                    d.Root.Domains["FirstDomain"].Dispose();
                } );
                shell2.IsDestroyed.Should().BeTrue( "The loader and shells expose the IsDestroyed marker." );
                loader2.IsDestroyed.Should().BeTrue();

                league2.Find( "FirstDomain" ).Should().BeNull( "One cannot Find a destroyed domain anymore..." );
                (await loader2.LoadAsync( TestHelper.Monitor )).Should().BeNull( "...and cannot obtain new shells to play with them." );

                await shell2.ModifyAsync( TestHelper.Monitor, ( m, d ) => d.Root.Persons[0].Age = 42 );
                shell2.Read( TestHelper.Monitor, ( m, d ) =>
                {
                    d.Root.Persons[0].Age.Should().Be( 42, "Existing shells CAN continue to work with Destroyed domains." );
                } );
            }
        }

        [SerializationVersion(0)]
        class Root1 : ObservableRootObject
        {
            public Root1()
            {
            }

            Root1( IBinaryDeserializerContext ctx )
                : base( ctx )
            {
                var r = ctx.StartReading();
            }

            void Write( BinarySerializer w )
            {
            }
        }

        [SerializationVersion(0)]
        class Root2 : ObservableRootObject
        {
            public Root2()
            {
            }

            Root2( IBinaryDeserializerContext ctx )
                : base( ctx )
            {
                var r = ctx.StartReading();
            }

            void Write( BinarySerializer w )
            {
            }
        }

        [SerializationVersion(0)]
        class Root3 : ObservableRootObject
        {
            public Root3()
            {
            }

            Root3( IBinaryDeserializerContext ctx )
                : base( ctx )
            {
                var r = ctx.StartReading();
            }

            void Write( BinarySerializer w )
            {
            }
        }

        [SerializationVersion(0)]
        class Root4 : ObservableRootObject
        {
            public Root4()
            {
            }

            Root4( IBinaryDeserializerContext ctx )
                : base( ctx )
            {
                var r = ctx.StartReading();
            }

            void Write( BinarySerializer w )
            {
            }
        }

        [Test]
        public async Task up_to_4_typed_roots_are_supported()
        {
            var store = CreateStore( nameof( up_to_4_typed_roots_are_supported ) );
            var league = await ObservableLeague.LoadAsync( TestHelper.Monitor, store );

            var roots = new[] { typeof( Root1 ), typeof( Root2 ), typeof( Root3 ), typeof( Root4 ) };

            await league.Coordinator.ModifyAsync( TestHelper.Monitor, ( m, d ) =>
            {
                Domain newOne = d.Root.CreateDomain( "4Roots", roots.Select( t => t.AssemblyQualifiedName ) );
                newOne.DomainName.Should().Be( "4Roots" );
                newOne.RootTypes.Should().BeEquivalentTo( roots.Select( t => t.AssemblyQualifiedName ) );
            } );

            await Check4RootsDomain( league );
            await league.CloseAsync( TestHelper.Monitor );

            var league2 = await ObservableLeague.LoadAsync( TestHelper.Monitor, store );

            await Check4RootsDomain( league2 );

            await league2.CloseAsync( TestHelper.Monitor );
        }

        static async Task Check4RootsDomain( ObservableLeague theLeague )
        {
            await using( var d = await theLeague["4Roots"].LoadAsync<Root1, Root2, Root3, Root4>( TestHelper.Monitor ) )
            {
                d.Read( TestHelper.Monitor, ( m, d ) =>
                {
                    d.Root1.Should().BeOfType<Root1>();
                    d.Root2.Should().BeOfType<Root2>();
                    d.Root3.Should().BeOfType<Root3>();
                    d.Root4.Should().BeOfType<Root4>();
                } );
            }
        }
    }
}
