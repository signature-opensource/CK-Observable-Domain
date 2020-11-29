using System;
using System.IO;
using System.Threading;
using CK.Core;
using CK.Testing;
using CK.Text;
using FluentAssertions;
using NUnit.Framework;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Domain.Tests.Clients
{
    public class FileTransactionProviderClientTests
    {
        [Test]
        public void File_can_be_written_manually_with_minimumDueTime_minus_1()
        {
            NormalizedPath folder = TestHelper.TestProjectFolder.AppendPart( "TestTemp" ).AppendPart( nameof( File_can_be_written_manually_with_minimumDueTime_minus_1 ) );
            TestHelper.CleanupFolder( folder );
            var file = new FileInfo( folder.AppendPart( "f.bin" ) );

            var client = new FileTransactionProviderClient( file.FullName, -1 );

            using var d = new ObservableDomain<TestObservableRootObject>(TestHelper.Monitor, nameof( File_can_be_written_manually_with_minimumDueTime_minus_1 ), startTimer: true, client: client );
            d.Modify( TestHelper.Monitor, () =>
            {
                d.Root.Prop1 = "Hello";
                d.Root.Prop2 = "World";
            } );
            file.Refresh();
            file.Exists.Should().BeFalse( "File is flushed manually, and should not exist yet." );

            client.Flush( TestHelper.Monitor );
            file.Refresh();
            file.Exists.Should().BeTrue( "File was flushed manually, and should now exist." );
        }

        [Test]
        public void File_is_written_on_every_snapshot_with_minimumDueTime_0()
        {
            NormalizedPath folder = TestHelper.TestProjectFolder.AppendPart( "TestTemp" ).AppendPart( nameof( File_is_written_on_every_snapshot_with_minimumDueTime_0 ) );
            TestHelper.CleanupFolder( folder );
            var file = new FileInfo( folder.AppendPart( "f.bin" ) );

            var client = new FileTransactionProviderClient( file.FullName, 0 );

            using var d = new ObservableDomain<TestObservableRootObject>(TestHelper.Monitor, nameof( File_is_written_on_every_snapshot_with_minimumDueTime_0 ), startTimer: true, client: client );
            d.Modify( TestHelper.Monitor, () =>
            {
                d.Root.Prop1 = "Hello";
                d.Root.Prop2 = "World";
            } );
            file.Refresh();
            file.Exists.Should().BeTrue( "File is flushed on every snapshot, and should exist" );
        }

        [Test]
        public void File_is_written_after_minimumDueTime_with_positive_minimumDueTime()
        {
            const int dueTimeMs = 300;
            NormalizedPath folder = TestHelper.TestProjectFolder.AppendPart( "TestTemp" ).AppendPart( nameof( File_is_written_after_minimumDueTime_with_positive_minimumDueTime ) );
            TestHelper.CleanupFolder( folder );
            var file = new FileInfo( folder.AppendPart( "f.bin" ) );

            var client = new FileTransactionProviderClient( file.FullName, dueTimeMs );

            using var d = new ObservableDomain<TestObservableRootObject>(TestHelper.Monitor, nameof(File_is_written_after_minimumDueTime_with_positive_minimumDueTime), startTimer: true, client: client );
            // Call once - doesn't trigger the DoWrite yet
            d.Modify( TestHelper.Monitor, () =>
            {
                d.Root.Prop1 = "Hello";
                d.Root.Prop2 = "World";
            } );

            file.Refresh();
            file.Exists.Should().BeFalse( "File is flushed before dueTime, and should not exist yet" );
            Thread.Sleep( 2 * dueTimeMs );
            // Call again - triggers the DoWrite
            d.Modify( TestHelper.Monitor, () =>
            {
            } );

            file.Refresh();
            file.Exists.Should().BeTrue( "File was flushed after dueTime, and should exist" );
        }

        [Test]
        public void File_due_time_is_properly_rescheduled()
        {

            int dueTimeMs = 300;
            int waitTimeMs = dueTimeMs + 150;

            NormalizedPath folder = TestHelper.TestProjectFolder.AppendPart( "TestTemp" ).AppendPart( nameof( File_is_written_after_minimumDueTime_with_positive_minimumDueTime ) );
            TestHelper.CleanupFolder( folder );
            var file = new FileInfo( folder.AppendPart( "f.bin" ) );

            var client1 = new FileTransactionProviderClient( file.FullName, dueTimeMs );

            using var d1 = new ObservableDomain<TestObservableRootObject>(TestHelper.Monitor, nameof(File_due_time_is_properly_rescheduled)+"(1)", startTimer: true, client: client1 );

            // Call once - doesn't trigger the DoWrite yet
            d1.Modify( TestHelper.Monitor, () =>
            {
                d1.Root.Prop1 = "Hello";
                d1.Root.Prop2 = "World";
            } );
            file.Refresh();
            file.Exists.Should().BeFalse( "File is flushed before dueTime, and should not exist yet" );

            Thread.Sleep( waitTimeMs );

            // Call again - triggers the DoWrite
            d1.Modify( TestHelper.Monitor, () =>
            {
            } );
            file.Refresh();
            file.Exists.Should().BeTrue( "File was flushed after dueTime, and should exist" );

            Thread.Sleep( waitTimeMs );

            file.Refresh();

            // Call again - change properties and trigger DoWrite
            d1.Modify( TestHelper.Monitor, () =>
            {
                d1.Root.Prop1 = "This should";
                d1.Root.Prop2 = "Have been saved to file";
            } );

            // Load file with another client and domain to ensure it has the new values

            var client2 = new FileTransactionProviderClient( file.FullName, -1 );
            using var d2 = new ObservableDomain<TestObservableRootObject>(TestHelper.Monitor, nameof( File_due_time_is_properly_rescheduled ) + "(1)", startTimer: true, client: client2 );
            using( d2.AcquireReadLock() )
            {
                d2.Root.Prop1.Should().Be( "This should" );
                d2.Root.Prop2.Should().Be( "Have been saved to file" );
            }
        }

        [Test]
        public void Load_throws_InvalidDataException_on_invalid_file()
        {
            var client = CreateClient( 0 );
            File.WriteAllText( client.FilePath, "(INVALID FILE CONTENTS)" );
            FileInfo fi = new FileInfo( client.FilePath );

            Action act = () =>
            {
                var willThrow = new ObservableDomain<TestObservableRootObject>(TestHelper.Monitor, nameof( Load_throws_InvalidDataException_on_invalid_file), startTimer: true, client: client );
            };

            // EndOfStreamException is an IOException.
            act.Should().Throw<InvalidDataException>( "ObservableDomain can't be created when given an invalid file." );
        }

        [Test]
        public void Flush_returns_false_when_file_directory_does_not_exist()
        {
            NormalizedPath path = Path.Combine(
                Path.GetTempPath(),
                "this-directory-definitely-shouldnt-exist",
                "ObservableDomain.bin"
            );
            var client = CreateClient( -1, path );


            using var d = new ObservableDomain<TestObservableRootObject>(TestHelper.Monitor, nameof( Flush_returns_false_when_file_directory_does_not_exist), startTimer: true, client: client );
            d.Modify( TestHelper.Monitor, () =>
            {
                d.Root.Prop1 = "Hello";
                d.Root.Prop2 = "World";

            } ).Success.Should().BeTrue();
            bool isSuccess = client.Flush( TestHelper.Monitor );

            isSuccess.Should().BeFalse( "ObservableDomain can't be flushed when target path does not exist" );

        }

        [Test]
        public void TransactionResult_has_ClientError_when_file_directory_does_not_exist()
        {
            NormalizedPath path = Path.Combine(
                Path.GetTempPath(),
                "this-directory-definitely-shouldnt-exist",
                "ObservableDomain.bin"
            );
            var client = CreateClient( 0, path );
            using var d = new ObservableDomain<TestObservableRootObject>(TestHelper.Monitor, nameof( TransactionResult_has_ClientError_when_file_directory_does_not_exist), startTimer: true, client: client );

            var transactionResult = d.Modify( TestHelper.Monitor, () =>
            {
                d.Root.Prop1 = "Hello";
                d.Root.Prop2 = "World";
            } );

            transactionResult.ClientError.Should().NotBeNull( "ObservableDomain can't be flushed when target path does not exist" );

        }

        [Test]
        public void Snapshot_file_can_be_saved_and_restored_in_compressed_or_not_form()
        {
            NormalizedPath folder = TestHelper.TestProjectFolder.AppendPart( "TestTemp" ).AppendPart( nameof( Snapshot_file_can_be_saved_and_restored_in_compressed_or_not_form ) );
            TestHelper.CleanupFolder( folder );
            NormalizedPath file1 = folder.AppendPart( "f1.bin" );
            NormalizedPath file2 = folder.AppendPart( "f2.bin" );

            // File1: Create first uncompressed. 
            var c1 = new FileTransactionProviderClient( file1, 0 );
            c1.CompressionKind.Should().Be( CompressionKind.None );
            using var d1 = new ObservableDomain<TestObservableRootObject>(TestHelper.Monitor, nameof( Snapshot_file_can_be_saved_and_restored_in_compressed_or_not_form ), startTimer: true, client: c1 );
            d1.Modify( TestHelper.Monitor, () =>
            {
                TestHelper.Monitor.Info( "Modifying properties." );
                d1.Root.Prop1 = "Hello";
                d1.Root.Prop2 = "World";
            } );

            // Read File1 back.
            {
                var c2 = new FileTransactionProviderClient( file1, 0 );
                using var d2 = new ObservableDomain<TestObservableRootObject>(TestHelper.Monitor, nameof( Snapshot_file_can_be_saved_and_restored_in_compressed_or_not_form ), startTimer: true, client: c2 );
                using( d2.AcquireReadLock() )
                {
                    d2.Root.Prop1.Should().Be( "Hello" );
                    d2.Root.Prop2.Should().Be( "World" );
                }
            }
            File.Move( file1, file2 );
            c1.CompressionKind = CompressionKind.GZiped;
            d1.Modify( TestHelper.Monitor, () =>
            {
                d1.Root.Prop1 = "Hello (I'm compressed)";
                d1.Root.Prop2 = "World! (me too)";
            } );

            // Read File1 back: it is compressed.
            new FileInfo( file1 ).Length.Should().BeLessThan( new FileInfo( file2 ).Length, "Compressed file is smaller (even if the strings are bigger)." );

            var cC = new FileTransactionProviderClient( file1, 0 );
            using var dC = new ObservableDomain<TestObservableRootObject>(TestHelper.Monitor, nameof( Snapshot_file_can_be_saved_and_restored_in_compressed_or_not_form ), startTimer: true, client: cC );
            using( dC.AcquireReadLock() )
            {
                dC.Root.Prop1.Should().Be( "Hello (I'm compressed)" );
                dC.Root.Prop2.Should().Be( "World! (me too)" );
            }

        }

        FileTransactionProviderClient CreateClient( int timerPeriodMs = -1, string path = null )
        {
            if( path == null )
            {
                path = Path.GetTempFileName();
                File.Delete( path );
            }
            return new FileTransactionProviderClient( path, timerPeriodMs );
        }
    }
}
