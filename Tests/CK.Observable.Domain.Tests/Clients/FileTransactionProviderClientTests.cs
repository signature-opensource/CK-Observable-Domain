using System;
using System.IO;
using System.Threading;
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
            var client = CreateClient( -1 );
            FileInfo fi = new FileInfo( client.Path );
            var d = new ObservableDomain<TestObservableRootObject>( client, TestHelper.Monitor );

            d.Modify( () =>
            {
                d.Root.Prop1 = "Hello";
                d.Root.Prop2 = "World";
            } );
            fi.Refresh();
            fi.Exists.Should().BeFalse( "File is flushed manually, and should not exist yet" );

            client.Flush( TestHelper.Monitor );
            fi.Refresh();
            fi.Exists.Should().BeTrue( "File was flushed manually, and should now exist" );

            fi.Delete();
        }

        [Test]
        public void File_is_written_on_every_snapshot_with_minimumDueTime_0()
        {
            var client = CreateClient( 0 );
            FileInfo fi = new FileInfo( client.Path );
            var d = new ObservableDomain<TestObservableRootObject>( client, TestHelper.Monitor );

            d.Modify( () =>
            {
                d.Root.Prop1 = "Hello";
                d.Root.Prop2 = "World";
            } );
            fi.Refresh();
            fi.Exists.Should().BeTrue( "File is flushed on every snapshot, and should exist" );

            fi.Delete();
        }

        [Test]
        public void File_is_written_after_minimumDueTime_with_positive_minimumDueTime()
        {
            int dueTimeMs = 300;
            var client = CreateClient( dueTimeMs );
            FileInfo fi = new FileInfo( client.Path );
            var d = new ObservableDomain<TestObservableRootObject>( client, TestHelper.Monitor );

            // Call once - doesn't trigger the DoWrite yet
            d.Modify( () =>
            {
                d.Root.Prop1 = "Hello";
                d.Root.Prop2 = "World";
            } );
            fi.Refresh();
            fi.Exists.Should().BeFalse( "File is flushed before dueTime, and should not exist yet" );

            Thread.Sleep( 2 * dueTimeMs );
            // Call again - triggers the DoWrite
            d.Modify( () =>
            {
            } );
            fi.Refresh();
            fi.Exists.Should().BeTrue( "File was flushed after dueTime, and should exist" );


            fi.Delete();
        }

        [Test]
        public void File_due_time_is_properly_rescheduled()
        {
            int dueTimeMs = 300;
            int waitTimeMs = dueTimeMs + 150;

            var client1 = CreateClient( dueTimeMs );
            FileInfo fi = new FileInfo( client1.Path );
            var d1 = new ObservableDomain<TestObservableRootObject>( client1, TestHelper.Monitor );

            // Call once - doesn't trigger the DoWrite yet
            d1.Modify( () =>
            {
                d1.Root.Prop1 = "Hello";
                d1.Root.Prop2 = "World";
            } );
            fi.Refresh();
            fi.Exists.Should().BeFalse( "File is flushed before dueTime, and should not exist yet" );

            Thread.Sleep( waitTimeMs );

            // Call again - triggers the DoWrite
            d1.Modify( () =>
            {
            } );
            fi.Refresh();
            fi.Exists.Should().BeTrue( "File was flushed after dueTime, and should exist" );

            Thread.Sleep( waitTimeMs );

            fi.Refresh();

            // Call again - change properties and trigger DoWrite
            d1.Modify( () =>
            {
                d1.Root.Prop1 = "This should";
                d1.Root.Prop2 = "Have been saved to file";
            } );

            // Load file with another client and domain to ensure it has the new values

            var client2 = CreateClient( -1, client1.Path );
            var d2 = new ObservableDomain<TestObservableRootObject>( client2, TestHelper.Monitor );
            using( d2.AcquireReadLock() )
            {
                d2.Root.Prop1.Should().Be( "This should" );
                d2.Root.Prop2.Should().Be( "Have been saved to file" );
            }

            fi.Delete();
        }

        [Test]
        public void Load_throws_on_invalid_file()
        {
            var client = CreateClient( 0 );
            File.WriteAllText( client.Path, "(INVALID FILE CONTENTS)" );
            FileInfo fi = new FileInfo( client.Path );

            Action act = () =>
            {
                var d = new ObservableDomain<TestObservableRootObject>( client, TestHelper.Monitor );
            };

            // EndOfStreamException is an IOException.
            act.Should().Throw<IOException>( "ObservableDomain can't be created when given an invalid file" );


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


            var d = new ObservableDomain<TestObservableRootObject>( client, TestHelper.Monitor );
            d.Modify( () =>
            {
                d.Root.Prop1 = "Hello";
                d.Root.Prop2 = "World";
            } );
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
            var d = new ObservableDomain<TestObservableRootObject>( client, TestHelper.Monitor );

            var transactionResult = d.Modify( () =>
            {
                d.Root.Prop1 = "Hello";
                d.Root.Prop2 = "World";
            } );

            transactionResult.ClientError.Should().NotBeNull( "ObservableDomain can't be flushed when target path does not exist" );

        }

        [Test]
        public void Snapshot_file_can_be_saved_and_restored()
        {
            NormalizedPath path;

            // Create test file
            {
                var client = CreateClient( 0 );
                path = client.Path;
                var d1 = new ObservableDomain<TestObservableRootObject>( client, TestHelper.Monitor );
                d1.Modify( () =>
                {
                    d1.Root.Prop1 = "Hello";
                    d1.Root.Prop2 = "World";
                } );
            }


            // Read test file
            {
                var client = CreateClient( 0, path );

                var d2 = new ObservableDomain<TestObservableRootObject>( client, TestHelper.Monitor );

                using( d2.AcquireReadLock() )
                {
                    d2.Root.Prop1.Should().Be( "Hello" );
                    d2.Root.Prop2.Should().Be( "World" );
                }
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
