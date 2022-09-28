using CK.Core;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.League.Tests
{
    public class BasicLeagueTests
    {
        readonly static NormalizedPath TestFolder = TestHelper.TestProjectFolder.AppendPart( "TestStores" );

        public static DirectoryStreamStore CreateStore( string name )
        {
            NormalizedPath p = GetStorePath( name );
            return new DirectoryStreamStore( p );
        }

        public static NormalizedPath GetStorePath( string name )
        {
            var p = TestFolder.AppendPart( name );
            TestHelper.CleanupFolder( p );
            return p;
        }

        [Test]
        public async Task simple_play_with_first_domain_Async()
        {
            var store = CreateStore( nameof( simple_play_with_first_domain_Async ) );
            var league = await ObservableLeague.LoadAsync( TestHelper.Monitor, store )!;

            Debug.Assert( league != null );

            league.Find( "FirstDomain" ).Should().BeNull( "Empty store. Not created yet." );

            await league.Coordinator.ModifyThrowAsync( TestHelper.Monitor, ( m, d ) => d.Root.CreateDomain( "FirstDomain", typeof(Model.School).AssemblyQualifiedName! ) );

            var loader = league.Find( "FirstDomain" );
            Debug.Assert( loader != null );
            loader.IsDestroyed.Should().BeFalse();
            loader.IsLoaded.Should().BeFalse();

            await using( var shell = await loader.LoadAsync<Model.School>( TestHelper.Monitor ) )
            {
                shell.Should().BeSameAs( loader, "Since the ActivityMonitor is the same, the loader is the shell." );

                // Closing the league only disposes the domains that are preloaded (by the domain options)
                // and are not still in use (all shells have been disposed).
                await league.CloseAsync( TestHelper.Monitor );

                var afterClosing = league.Find( "FirstDomain" );
                afterClosing.Should().BeNull( "league has been closed. Domain cannot be found anymore." );

                (await loader.LoadAsync( TestHelper.Monitor )).Should().BeNull( "league has been closed. Loader cannot give us a shell." );

                // The previously obtained shell is still here: the domain is available.
                await shell.ModifyThrowAsync( TestHelper.Monitor, ( m, d ) =>
                {
                    d.Root.Persons.Add( new Model.Person() { FirstName = "X", LastName = "Y", Age = 22 } );
                } );
                TestHelper.Monitor.Info( "Disposing the shell on 'FirstDomain'." );
            }
            TestHelper.Monitor.Info( "The 'FirstDomain' has been saved and disposed." );

            // Creates a brand new league on the store.
            var league2 = await ObservableLeague.LoadAsync( TestHelper.Monitor, store );
            Debug.Assert( league2 != null );

            var loader2 = league2.Find( "FirstDomain" );
            Debug.Assert( loader2 != null );

            await using( var shell2 = await loader2.LoadAsync<Model.School>( TestHelper.Monitor ) )
            {
                Debug.Assert( shell2 != null );

                shell2.Read( TestHelper.Monitor, ( m, d ) =>
                {
                    var p = d.Root.Persons[0];
                    p.FirstName.Should().Be( "X" );
                    p.LastName.Should().Be( "Y" );
                    return p.Age;
                } ).Should().Be( 22 );

                // We dispose the Domain in the Coordinator domain: it is now marked as Destroyed.
                await league2.Coordinator.ModifyThrowAsync( TestHelper.Monitor, ( m, d ) =>
                {
                    d.Root.Domains["FirstDomain"].Destroy();
                } );
                shell2.IsDestroyed.Should().BeTrue( "The loader and shells expose the IsDestroyed marker." );
                loader2.IsDestroyed.Should().BeTrue();

                league2.Find( "FirstDomain" ).Should().BeNull( "One cannot Find a destroyed domain anymore..." );
                (await loader2.LoadAsync( TestHelper.Monitor )).Should().BeNull( "...and cannot obtain new shells to play with them." );

                await shell2.ModifyThrowAsync( TestHelper.Monitor, ( m, d ) => d.Root.Persons[0].Age = 42 );
                shell2.TryRead( TestHelper.Monitor,
                                ( m, d ) => d.Root.Persons[0].Age.Should().Be( 42, "Existing shells CAN continue to work with Destroyed domains." ),
                                millisecondsTimeout: -1 )
                .Should().BeTrue();
            }
        }

        [SerializationVersion(0)]
        class Root1 : ObservableRootObject
        {
            public Root1()
            {
            }

            Root1( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
                : base( BinarySerialization.Sliced.Instance )
            {
            }

            public static void Write( BinarySerialization.IBinarySerializer w, in Root1 o )
            {
            }
        }

        [SerializationVersion(0)]
        class Root2 : ObservableRootObject
        {
            public Root2()
            {
            }

            Root2( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
                : base( BinarySerialization.Sliced.Instance )
            {
            }

            public static void Write( BinarySerialization.IBinarySerializer w, in Root2 o )
            {
            }
        }

        [SerializationVersion(0)]
        class Root3 : ObservableRootObject
        {
            public Root3()
            {
            }

            Root3( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
                : base( BinarySerialization.Sliced.Instance )
            {
            }

            public static void Write( BinarySerialization.IBinarySerializer w, in Root3 o )
            {
            }
        }

        [SerializationVersion(0)]
        class Root4 : ObservableRootObject
        {
            public Root4()
            {
            }

            Root4( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
                : base( BinarySerialization.Sliced.Instance )
            {
            }

            public static void Write( BinarySerialization.IBinarySerializer w, in Root4 o )
            {
            }
        }

        [Test]
        public async Task up_to_4_typed_roots_are_supported_Async()
        {
            var store = CreateStore( nameof( up_to_4_typed_roots_are_supported_Async ) );
            var league = await ObservableLeague.LoadAsync( TestHelper.Monitor, store );

            var roots = new[] { typeof( Root1 ), typeof( Root2 ), typeof( Root3 ), typeof( Root4 ) };

            await league.Coordinator.ModifyThrowAsync( TestHelper.Monitor, ( m, d ) =>
            {
                ODomain newOne = d.Root.CreateDomain( "4Roots", roots.Select( t => t.AssemblyQualifiedName ) );
                newOne.DomainName.Should().Be( "4Roots" );
                newOne.RootTypes.Should().BeEquivalentTo( roots.Select( t => t.AssemblyQualifiedName ) );
            } );

            await Check4RootsDomainAsync( league );
            await league.CloseAsync( TestHelper.Monitor );

            var league2 = await ObservableLeague.LoadAsync( TestHelper.Monitor, store );

            await Check4RootsDomainAsync( league2 );

            await league2.CloseAsync( TestHelper.Monitor );
        }

        static async Task Check4RootsDomainAsync( ObservableLeague theLeague )
        {
            await using( var d = await theLeague["4Roots"].LoadAsync<Root1, Root2, Root3, Root4>( TestHelper.Monitor ) )
            {
                d.Read( TestHelper.Monitor, ( m, d ) =>
                {
                    d.Root1.Should().BeOfType<Root1>();
                    d.Root2.Should().BeOfType<Root2>();
                    d.Root3.Should().BeOfType<Root3>();
                    return d.Root4.GetType();
                } )
                .Should().Be( typeof( Root4 ) );
            }
        }
    }
}
