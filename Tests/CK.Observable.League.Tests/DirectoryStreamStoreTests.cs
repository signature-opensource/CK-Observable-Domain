using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using CK.Core;
using FluentAssertions;
using NUnit.Framework;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.League.Tests;

public class DirectoryStreamStoreTests
{
    private readonly static RandomNumberGenerator Random = RandomNumberGenerator.Create();
    readonly static NormalizedPath TestFolder = TestHelper.TestProjectFolder.AppendPart( "TestStores" );

    public static MemoryStream CreateDataStream( int size = 1000 )
    {
        byte[] buffer = new byte[size];
        Random.GetBytes( buffer );
        return new MemoryStream( buffer );
    }

    [Test]
    public async Task housekeeping_does_not_clean_when_disabled_Async()
    {
        var store = BasicLeagueTests.CreateStore( nameof( housekeeping_does_not_clean_when_disabled_Async ) );
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
    public async Task housekeeping_can_clean_by_timespan_Async()
    {
        var store = BasicLeagueTests.CreateStore( nameof( housekeeping_can_clean_by_timespan_Async ) );
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
    public async Task housekeeping_can_clean_by_total_size_Async()
    {
        var store = BasicLeagueTests.CreateStore( nameof( housekeeping_can_clean_by_total_size_Async ) );
        IBackupStreamStore streamStore = store;
        int backupCount = 10;
        int filesize = 1000;
        string resourceName = "domain";

        // Create 10 files of 1000 bytes each
        for( int i = 0; i < backupCount; i++ )
        {
            using( var ms = CreateDataStream( filesize ) )
            {
                await streamStore.UpdateAsync( resourceName, ( s ) => ms.CopyToAsync( s ), allowCreate: true );
            }
        }

        // Keep 5000 bytes
        streamStore.CleanBackups( TestHelper.Monitor, resourceName, TimeSpan.Zero, filesize * (backupCount / 2) );

        var backupNames = streamStore.GetBackupNames( resourceName );

        backupNames.Should().HaveCount( backupCount / 2,
            $"{backupCount} / 2 snapshot backups should remain after cleanup." );
    }

    [Test]
    public async Task housekeeping_can_clean_by_total_size_and_timespan_with_size_matching_Async()
    {
        var store = BasicLeagueTests.CreateStore( nameof( housekeeping_can_clean_by_total_size_and_timespan_with_size_matching_Async ) );
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
    public async Task housekeeping_can_clean_by_total_size_and_timespan_with_timespan_matching_Async()
    {
        var store = BasicLeagueTests.CreateStore( nameof( housekeeping_can_clean_by_total_size_and_timespan_with_timespan_matching_Async ) );
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

}
