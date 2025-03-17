using CK.Core;
using Shouldly;
using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.League.Tests;

public class LeagueSearializationTests
{
    [Test]
    public void coordinator_serialization()
    {
        using var d = new ObservableDomain<OCoordinatorRoot>(TestHelper.Monitor, String.Empty, startTimer: true );
        var ctx = new BinarySerialization.BinaryDeserializerContext();
        ctx.Services.Add<ObservableDomain>( new ObservableDomain<OCoordinatorRoot>(TestHelper.Monitor, String.Empty, startTimer: true ) );
        BinarySerialization.BinarySerializer.IdempotenceCheck( d.Root, deserializerContext: ctx );
    }

    [Test]
    public async Task empty_league_serialization_Async()
    {
        var store = BasicLeagueTests.CreateStore( nameof( empty_league_serialization_Async ) );
        var league = await ObservableLeague.LoadAsync( TestHelper.Monitor, store );
        Debug.Assert( league != null );
        await league.CloseAsync( TestHelper.Monitor );
        var league2 = await ObservableLeague.LoadAsync( TestHelper.Monitor, store );
        Debug.Assert( league2 != null );
        await league2.CloseAsync( TestHelper.Monitor );
    }

    [Test]
    public async Task one_domain_league_serialization_Async()
    {
        var store = BasicLeagueTests.CreateStore( nameof( empty_league_serialization_Async ) );
        var league = await ObservableLeague.LoadAsync( TestHelper.Monitor, store )!;
        await league.Coordinator.ModifyThrowAsync( TestHelper.Monitor, ( m, d )
            => d.Root.CreateDomain( "First", typeof( Model.School ).AssemblyQualifiedName! ),
            waitForDomainPostActionsCompletion: true );
        // Using the non generic IObservableDomain.
        await using( var f = await league.Find( "First" )!.LoadAsync( TestHelper.Monitor ) )
        {
            await f.ModifyThrowAsync( TestHelper.Monitor, ( m, d ) => ((IObservableDomain<Model.School>)d).Root.Persons.Add( new Model.Person() { FirstName = "A" } ) );
        }
        await league.CloseAsync( TestHelper.Monitor );

        var league2 = await ObservableLeague.LoadAsync( TestHelper.Monitor, store );
        Debug.Assert( league2 != null );

        league2.Coordinator.Read( TestHelper.Monitor, ( m, d ) => d.Root.Domains.Count ).ShouldBe( 1 );

        var first2 = league2.Find( "First" )!;
        first2.ShouldNotBeNull();
        // Using the strongly typed IObservableDomain<T>.
        await using( var f = await first2.LoadAsync<Model.School>( TestHelper.Monitor ) )
        {
            f.Read( TestHelper.Monitor, ( m, d ) => d.Root.Persons[0].FirstName ).ShouldBe( "A" );
        }
        await league2.CloseAsync( TestHelper.Monitor );
    }

    [SerializationVersion( 0 )]
    public sealed class InstantiationTracker : ObservableRootObject
    {
        public InstantiationTracker()
        {
            ++ContructorCount;
        }

        InstantiationTracker( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
            : base( BinarySerialization.Sliced.Instance )
        {
            ++DeserializationCount;
        }

        public static void Write( BinarySerialization.IBinarySerializer w, in InstantiationTracker o )
        {
            ++WriteCount;
        }

        static public int WriteCount { get; set; }

        static public int ContructorCount { get; set; }

        static public int DeserializationCount { get; set; }
    }

    [Test]
    public async Task league_reload_domains_by_deserializing_Async()
    {
        var store = BasicLeagueTests.CreateStore( nameof( league_reload_domains_by_deserializing_Async ) );
        var league = await ObservableLeague.LoadAsync( TestHelper.Monitor, store );

        Debug.Assert( league != null );

        InstantiationTracker.ContructorCount = InstantiationTracker.DeserializationCount = InstantiationTracker.WriteCount = 0;

        await league.Coordinator.ModifyThrowAsync( TestHelper.Monitor, ( m, d ) =>
        {
            var a = d.Root.CreateDomain( "Witness", typeof( InstantiationTracker ).AssemblyQualifiedName! );
            a.Options = a.Options.SetLifeCycleOption( DomainLifeCycleOption.Never );
        }, waitForDomainPostActionsCompletion: true );

        var loader = league["Witness"];
        Debug.Assert( loader != null );

        loader.IsLoaded.ShouldBeFalse();
        InstantiationTracker.ContructorCount.ShouldBe( 0, "The ObservableDomain has NOT been created yet (DomainLifeCycleOption.Never)." );
        InstantiationTracker.DeserializationCount.ShouldBe( 0 );
        InstantiationTracker.WriteCount.ShouldBe( 0 );

        // Loads/Unload it: this triggers its creation.
        await (await loader.LoadAsync<InstantiationTracker>( TestHelper.Monitor ))!.DisposeAsync( TestHelper.Monitor );

        InstantiationTracker.ContructorCount.ShouldBe( 1 );
        InstantiationTracker.DeserializationCount.ShouldBe( 0 );
        InstantiationTracker.WriteCount.ShouldBe( 3, "One save after load, one after modify, one after dispose." );

        // Loads/Unload it again.
        await (await loader.LoadAsync<InstantiationTracker>( TestHelper.Monitor ))!.DisposeAsync( TestHelper.Monitor );

        InstantiationTracker.ContructorCount.ShouldBe( 1, "This will never change from now on: the domain is always deserialized." );
        InstantiationTracker.DeserializationCount.ShouldBe( 1 );
        InstantiationTracker.WriteCount.ShouldBe( 5 );

        // Loads/Unload it again and creates a transaction.
        await using( var shell = await loader.LoadAsync<InstantiationTracker>( TestHelper.Monitor ) )
        {
            Debug.Assert( shell != null );
            await shell.ModifyThrowAsync( TestHelper.Monitor, (m,d) => { } );
        }

        InstantiationTracker.ContructorCount.ShouldBe( 1, "This will never change from now on: the domain is always deserialized." );
        InstantiationTracker.DeserializationCount.ShouldBe( 2 );
        InstantiationTracker.WriteCount.ShouldBe( 8 );

    }


    [SerializationVersion(0)]
    public sealed class WriteCounter : ObservableRootObject
    {
        public WriteCounter()
        {
        }

        WriteCounter( BinarySerialization.IBinaryDeserializer r, BinarySerialization.ITypeReadInfo info )
            : base( BinarySerialization.Sliced.Instance )
        {
            WriteCount = r.Reader.ReadInt32();
        }

        public static void Write( BinarySerialization.IBinarySerializer w, in WriteCounter o )
        {
            w.Writer.Write( ++o.WriteCount );
        }

        public int WriteCount { get; private set; }

        public string? ThisIsInitializedByInitializer { get; internal set; }
    }

}
