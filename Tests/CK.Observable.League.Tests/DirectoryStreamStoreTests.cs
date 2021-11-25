using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using CK.Text;
using FluentAssertions;
using NUnit.Framework;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.League.Tests
{
    public class DirectoryStreamStoreTests
    {
        private readonly static RandomNumberGenerator Random = RandomNumberGenerator.Create();
        readonly static NormalizedPath TestFolder = TestHelper.TestProjectFolder.AppendPart( "TestStores" );

        public static DirectoryStreamStore CreateStore( string name )
        {
            var p = TestFolder.AppendPart( name );
            TestHelper.CleanupFolder( p );
            return new DirectoryStreamStore( p );
        }

        public static MemoryStream CreateDataStream( int size = 1000 )
        {
            byte[] buffer = new byte[size];
            Random.GetBytes( buffer );
            return new MemoryStream( buffer );
        }

        [Test]
        public async Task housekeeping_does_not_clean_when_disabled()
        {
            var store = CreateStore( nameof( housekeeping_does_not_clean_when_disabled ) );
            IBackupStreamStore streamStore = store;
            int backupCount = 100;
            string resourceName = "domain";

            for( int i = 0; i < backupCount; i++ )
            {
                using( var ms = CreateDataStream() )
                {
                    await streamStore.UpdateAsync( resourceName, ( s ) => ms.CopyToAsync( s ), allowCreate: true );
                }
            }

            streamStore.CleanBackups( TestHelper.Monitor, resourceName, TimeSpan.Zero, 0 );

            var backupNames = streamStore.GetBackupNames( resourceName );

            backupNames.Should().HaveCount( backupCount - 1,
                $"{backupCount} - 1 snapshot backups should have been written and no cleanup should have been performed." );
        }

        [Test]
        public async Task housekeeping_can_clean_by_timespan()
        {
            var store = CreateStore( nameof( housekeeping_can_clean_by_timespan ) );
            IBackupStreamStore streamStore = store;
            int backupCount = 10;
            string resourceName = "domain";

            for( int i = 0; i < backupCount; i++ )
            {
                using( var ms = CreateDataStream() )
                {
                    await streamStore.UpdateAsync( resourceName, ( s ) => ms.CopyToAsync( s ), allowCreate: true );
                }
            }

            await Task.Delay( 1000 );

            for( int i = 0; i < backupCount; i++ )
            {
                using( var ms = CreateDataStream() )
                {
                    await streamStore.UpdateAsync( resourceName, ( s ) => ms.CopyToAsync( s ), allowCreate: true );
                }
            }

            streamStore.CleanBackups( TestHelper.Monitor, resourceName, TimeSpan.FromMilliseconds( 1000 ), 0 );

            var backupNames = streamStore.GetBackupNames( resourceName );

            backupNames.Should().HaveCount( backupCount,
                $"{backupCount} snapshot backups should remain after cleanup." );
        }

        [Test]
        public async Task housekeeping_can_clean_by_total_size()
        {
            var store = CreateStore( nameof( housekeeping_can_clean_by_total_size ) );
            IBackupStreamStore streamStore = store;
            int backupCount = 10;
            int filesize = 1000;
            string resourceName = "domain";

            for( int i = 0; i < backupCount; i++ )
            {
                using( var ms = CreateDataStream( filesize ) )
                {
                    await streamStore.UpdateAsync( resourceName, ( s ) => ms.CopyToAsync( s ), allowCreate: true );
                }
            }

            streamStore.CleanBackups( TestHelper.Monitor, resourceName, TimeSpan.Zero, filesize * (backupCount / 2) );

            var backupNames = streamStore.GetBackupNames( resourceName );

            backupNames.Should().HaveCount( backupCount / 2,
                $"{backupCount} / 2 snapshot backups should remain after cleanup." );
        }

        [Test]
        public async Task housekeeping_can_clean_by_total_size_and_timespan_with_size_matching()
        {
            var store = CreateStore( nameof( housekeeping_can_clean_by_total_size_and_timespan_with_size_matching ) );
            IBackupStreamStore streamStore = store;
            int backupCount = 10;
            int filesize = 1000;
            string resourceName = "domain";

            for( int i = 0; i < backupCount; i++ )
            {
                using( var ms = CreateDataStream( filesize ) )
                {
                    await streamStore.UpdateAsync( resourceName, ( s ) => ms.CopyToAsync( s ), allowCreate: true );
                }
            }

            await Task.Delay( 1000 );

            for( int i = 0; i < backupCount; i++ )
            {
                using( var ms = CreateDataStream( filesize ) )
                {
                    await streamStore.UpdateAsync( resourceName, ( s ) => ms.CopyToAsync( s ), allowCreate: true );
                }
            }

            streamStore.CleanBackups( TestHelper.Monitor, resourceName, TimeSpan.FromMilliseconds( 1000 ), 1 );

            var backupNames = streamStore.GetBackupNames( resourceName );

            backupNames.Should().HaveCount( backupCount,
                $"{backupCount} snapshot backups should remain after cleanup." );
        }

        [Test]
        public async Task housekeeping_can_clean_by_total_size_and_timespan_with_timespan_matching()
        {
            var store = CreateStore( nameof( housekeeping_can_clean_by_total_size_and_timespan_with_timespan_matching ) );
            IBackupStreamStore streamStore = store;
            int backupCount = 10;
            int filesize = 1000;
            string resourceName = "domain";

            for( int i = 0; i < backupCount; i++ )
            {
                using( var ms = CreateDataStream( filesize ) )
                {
                    await streamStore.UpdateAsync( resourceName, ( s ) => ms.CopyToAsync( s ), allowCreate: true );
                }
            }

            await Task.Delay( 50 );

            for( int i = 0; i < backupCount; i++ )
            {
                using( var ms = CreateDataStream( filesize ) )
                {
                    await streamStore.UpdateAsync( resourceName, ( s ) => ms.CopyToAsync( s ), allowCreate: true );
                }
            }
            await Task.Delay( 100 );

            streamStore.CleanBackups( TestHelper.Monitor, resourceName, TimeSpan.FromMilliseconds( 1 ), backupCount * filesize );

            var backupNames = streamStore.GetBackupNames( resourceName );

            backupNames.Should().HaveCount( backupCount,
                $"{backupCount} snapshot backups should remain after cleanup." );
        }

        [Test]
        public async Task housekeeping_is_triggered_on_league_load()
        {
            string domainName = "domain";
            string resourceName = $"d-{domainName.ToLowerInvariant()}";
            var store = CreateStore( nameof( housekeeping_is_triggered_on_league_load ) );

            var league = await ObservableLeague.LoadAsync( TestHelper.Monitor, store );
            Debug.Assert( league != null, nameof( league ) + " != null" );

            var rootTypes = new string[] { typeof( Model.School ).AssemblyQualifiedName! };
            var options = new ManagedDomainOptions( DomainLifeCycleOption.Default, CompressionKind.None, 0,
                TimeSpan.Zero, TimeSpan.FromMilliseconds( 300 ), 0, TimeSpan.FromDays( 1 ), 100000, 10 );

            await league.Coordinator.ModifyAsync( TestHelper.Monitor, ( m, d ) =>
                d.Root.CreateDomain( domainName, rootTypes, options ) );
            var loader = league.Find( domainName );
            Debug.Assert( loader != null, nameof( loader ) + " != null" );

            // Create 10 backups
            for( int i = 0; i < 10; i++ )
            {
                await using( var shell = await loader.LoadAsync<Model.School>( TestHelper.Monitor ) )
                {
                    Debug.Assert( shell != null, nameof( shell ) + " != null" );
                    await shell.ModifyAsync( TestHelper.Monitor, ( m, d ) =>
                    {
                        d.Root.Persons.Add( new Model.Person() { FirstName = "X", LastName = "Y", Age = 22 } );
                    } );
                }
            }

            // Wait 300 ms
            await Task.Delay( 300 );

            // Create 10 backups
            for( int i = 0; i < 10; i++ )
            {
                await using( var shell = await loader.LoadAsync<Model.School>( TestHelper.Monitor ) )
                {
                    Debug.Assert( shell != null, nameof( shell ) + " != null" );
                    await shell.ModifyAsync( TestHelper.Monitor, ( m, d ) =>
                    {
                        d.Root.Persons.Add( new Model.Person() { FirstName = "X", LastName = "Y", Age = 22 } );
                    } );
                }
            }

            store.GetBackupNames( resourceName ).Should().HaveCountLessOrEqualTo( 10, "At least 10 backups should have been deleted with the time rule" );
        }
    }
}
