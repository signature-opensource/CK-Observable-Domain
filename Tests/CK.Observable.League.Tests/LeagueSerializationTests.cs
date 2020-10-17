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

        [SerializationVersion(0)]
        public class WriteCounter : ObservableRootObject
        {
            public WriteCounter()
            {
            }

            WriteCounter( IBinaryDeserializerContext d )
                : base( d )
            {
                var r = d.StartReading().Reader;
                WriteCount = r.ReadInt32();
            }

            void Write( BinarySerializer w )
            {
                w.Write( ++WriteCount );
            }

            public int WriteCount { get; private set; }

            public string ThisIsInitializedByInitializer { get; internal set; }
        }

        [Test]
        public async Task domain_initializer_is_applied_at_the_creation_and_at_each_load()
        {
            var store = BasicLeagueTests.CreateStore( nameof( domain_initializer_is_applied_at_the_creation_and_at_each_load ) );

            Action<IActivityMonitor, ObservableDomain> initA = ( m, d ) =>
            {
                d.Should().BeAssignableTo<IObservableDomain<WriteCounter>>();
                var r = (WriteCounter)d.AllRoots[0];
                r.ThisIsInitializedByInitializer = d.TransactionSerialNumber == 0 ? "Just created." : $"Loaded after transaction n°{d.TransactionSerialNumber}.";
            };

            var league = await ObservableLeague.LoadAsync( TestHelper.Monitor, store, name => name == "A" ? initA : null );

            await league.Coordinator.ModifyThrowAsync( TestHelper.Monitor, ( m, d ) =>
            {
                var a = d.Root.CreateDomain( "A", typeof( WriteCounter ).AssemblyQualifiedName );
                a.Options = a.Options.SetLifeCycleOption( DomainLifeCycleOption.Always );
            } );

            league["A"].IsLoaded.Should().BeTrue();
            await using( var shell = await league["A"].LoadAsync<WriteCounter>( TestHelper.Monitor ) )
            {
                await shell.ModifyThrowAsync( TestHelper.Monitor, ( monitor, d ) =>
                {
                    d.Root.ThisIsInitializedByInitializer.Should().Be( "Just created." );
                    d.Root.WriteCount.Should().Be( 1, "The first transaction of initialization has been saved." );
                } );
                shell.Read( TestHelper.Monitor, ( monitor, d ) =>
                {
                    d.Root.ThisIsInitializedByInitializer.Should().Be( "Just created.", "We have not Unload/Load the domain." );
                    d.Root.WriteCount.Should().Be( 2, "The transaction above has been saved." );
                } );
            }

            await league.Coordinator.ModifyThrowAsync( TestHelper.Monitor, ( m, d ) =>
            {
                var a = d.Root.Domains["A"];
                a.Options = a.Options.SetLifeCycleOption( DomainLifeCycleOption.Never );
            } );

            league["A"].IsLoaded.Should().BeFalse();
            await using( var shell = await league["A"].LoadAsync<WriteCounter>( TestHelper.Monitor ) )
            {
                shell.Read( TestHelper.Monitor, ( monitor, d ) =>
                {
                    d.Root.ThisIsInitializedByInitializer.Should().Be( "Loaded after transaction n°2.", "Unload/Load done." );
                    d.Root.WriteCount.Should().Be( 2, "No need to write it once more: it has just been restored." );
                } );
                // Transaction n°3:
                await shell.ModifyAsync( TestHelper.Monitor, ( monitor, d ) => { } );
                shell.Read( TestHelper.Monitor, ( monitor, d ) =>
                {
                    d.Root.ThisIsInitializedByInitializer.Should().Be( "Loaded after transaction n°2.", "The initialization stays the same." );
                    d.Root.WriteCount.Should().Be( 3, "A 3rd snapshot has been taken for the empty transaction above." );
                } );
            }
            league["A"].IsLoaded.Should().BeFalse();
            await using( var shell = await league["A"].LoadAsync<WriteCounter>( TestHelper.Monitor ) )
            {
                shell.Read( TestHelper.Monitor, ( monitor, d ) =>
                {
                    d.Root.ThisIsInitializedByInitializer.Should().Be( "Loaded after transaction n°3.", "Unload/Load done." );
                    d.Root.WriteCount.Should().Be( 3, "No need to write it once more: it has just been restored." );
                } );
            }

        }

    }
}
