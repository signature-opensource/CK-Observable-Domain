using CK.BinarySerialization;
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
    [TestFixture]
    public class HouseKeepingTests
    {

        [TestCase( 1 )]
        [TestCase( 2 )]
        [TestCase( 20 )]
        public async Task housekeeping_based_on_time_only_Async( int housekeepingRate )
        {
            string domainName = "domain";
            string resourceName = $"d-{domainName.ToLowerInvariant()}";
            var store = BasicLeagueTests.CreateStore( nameof( housekeeping_based_on_time_only_Async ) );

            var league = await ObservableLeague.LoadAsync( TestHelper.Monitor, store );
            Debug.Assert( league != null, nameof( league ) + " != null" );

            var rootTypes = new string[] { typeof( Model.School ).AssemblyQualifiedName! };
            var options = new ManagedDomainOptions(
                    // Keep the domain in memory (do not call Dispose) even
                    // when there's not timer and it's not used.
                    DomainLifeCycleOption.Always,
                    // Don't compress since we want file with a size.
                    CompressionKind.None,
                    // Default transactional mode. Each commit triggers a snapshot.
                    skipTransactionCount: 0,
                    // Snapshots are always saved.
                    snapshotSaveDelay: TimeSpan.Zero,
                    // Guaranty 500 milliseconds of backups. 
                    snapshotKeepDuration: TimeSpan.FromMilliseconds( 500 ),
                    // No size limit: only time matters here.
                    snapshotMaximalTotalKiB: 0,
                    // We don't care of events here.
                    eventKeepDuration: TimeSpan.Zero,
                    // Minimum value is 1.
                    eventKeepLimit: 1,
                    // Cleanup at each N saves.
                    housekeepingRate: housekeepingRate );

            await league.Coordinator.ModifyThrowAsync( TestHelper.Monitor, ( m, d ) => d.Root.CreateDomain( domainName, rootTypes, options ) );
            var loader = league.Find( domainName );
            Debug.Assert( loader != null, nameof( loader ) + " != null" );

            // Create 10 backups
            for( int i = 0; i < 10; i++ )
            {
                await using( var shell = await loader.LoadAsync<Model.School>( TestHelper.Monitor ) )
                {
                    Debug.Assert( shell != null, nameof( shell ) + " != null" );
                    await shell.ModifyThrowAsync( TestHelper.Monitor, ( m, d ) =>
                    {
                        d.Root.Persons.Add( new Model.Person() { FirstName = "X", LastName = "Y", Age = 22 } );
                    } );
                }
            }
            store.GetBackupNames( resourceName ).Should().HaveCount( 10, "The cleanup should not be done because of the 500 milliseconds guaranty." );

            // Wait 500 ms
            await Task.Delay( 500 );

            // Create 10 backups
            for( int i = 0; i < 10; i++ )
            {
                await using( var shell = await loader.LoadAsync<Model.School>( TestHelper.Monitor ) )
                {
                    Debug.Assert( shell != null );
                    await shell.ModifyThrowAsync( TestHelper.Monitor, ( m, d ) =>
                    {
                        d.Root.Persons.Add( new Model.Person() { FirstName = "X", LastName = "Y", Age = 22 } );
                    } );
                }
            }
            store.GetBackupNames( resourceName ).Should().HaveCountLessOrEqualTo( 10, "At least 10 backups should have been deleted with the time rule." );
        }

        [SerializationVersion(0)]
        class OJustForTheSize : InternalObject
        {
            public string? Payload { get; set; }

            public OJustForTheSize( string payload )
            {
                Payload = payload;
            }

            OJustForTheSize( CK.BinarySerialization.IBinaryDeserializer d, ITypeReadInfo info )
            {
                Payload = d.Reader.ReadNullableString();
            }

            public static void Write( IBinarySerializer s, in OJustForTheSize o )
            {
                s.Writer.WriteNullableString( o.Payload );
            }
        }


        [Test]
        public async Task housekeeping_based_on_time_and_size_Async()
        {
            var store = BasicLeagueTests.CreateStore( nameof( housekeeping_based_on_time_and_size_Async ) );
            var league = (await ObservableLeague.LoadAsync( TestHelper.Monitor, store ))!;
            await league.Coordinator.ModifyThrowAsync( TestHelper.Monitor, ( m, coodinator ) =>
            {
                var d = coodinator.Root.CreateDomain( "M" );
                d.Options = new ManagedDomainOptions( // Keep the domain in memory.
                                                      DomainLifeCycleOption.Always,
                                                      // Don't compress since we want ~6KiB files.
                                                      CompressionKind.None,
                                                      // Default transactional mode. Each commit triggers a snapshot.
                                                      skipTransactionCount: 0,
                                                      // Snapshots are always saved.
                                                      snapshotSaveDelay: TimeSpan.Zero,
                                                      // Guaranty 300 milliseconds of backups. 
                                                      snapshotKeepDuration: TimeSpan.FromMilliseconds( 300 ),
                                                      // Keep only 10 KiB cumulated max.
                                                      snapshotMaximalTotalKiB: 10,
                                                      // We don't care of events here.
                                                      eventKeepDuration: TimeSpan.Zero,
                                                      // Minimum value is 1.
                                                      eventKeepLimit: 1,
                                                      // Cleanup on every save.
                                                      housekeepingRate: 1
                );
            } );
            
            // Keeps a handle on the shell (this doesn't change anything).
            await using var shell = (await league.Find( "M" )!.LoadAsync( TestHelper.Monitor ))!;
            
            using( TestHelper.Monitor.OpenInfo( "Initializing domain. This doesn't create a backup." ) )
            {
                await shell.ModifyThrowAsync( TestHelper.Monitor, ( m, d ) =>
                {
                    var o = new OJustForTheSize( new String( 'g', 6*1024 ) );
                } );
            }
            store.GetBackupNames( "d-m" ).Should().HaveCount( 1 );
            // An empty transaction is committed: this creates 4 backups.
            await shell.ModifyThrowAsync( TestHelper.Monitor, ( m, d ) => { } );
            await shell.ModifyThrowAsync( TestHelper.Monitor, ( m, d ) => { } );
            await shell.ModifyThrowAsync( TestHelper.Monitor, ( m, d ) => { } );
            await shell.ModifyThrowAsync( TestHelper.Monitor, ( m, d ) => { } );
            store.GetBackupNames( "d-m" ).Should().HaveCount( 5 );

            await Task.Delay( 300 );

            await shell.ModifyThrowAsync( TestHelper.Monitor, ( m, d ) => { } );
            store.GetBackupNames( "d-m" ).Should().HaveCount( 1 );

        }
    }
}
