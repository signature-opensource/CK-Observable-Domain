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
        public void File_can_be_written_manually_with_timerMs_minus_1()
        {
            using( var client = CreateClient( -1 ) )
            {
                FileInfo fi = new FileInfo( client.Path );
                var d = new ObservableDomain<TestObservableRootObject>( client, TestHelper.Monitor );

                d.Modify( () =>
                {
                    d.Root.Prop1 = "Hello";
                    d.Root.Prop2 = "World";
                } );
                fi.Refresh();
                fi.Exists.Should().BeFalse( "File is flushed manually, and should not exist yet" );

                client.Flush();
                fi.Refresh();
                fi.Exists.Should().BeTrue( "File was flushed manually, and should now exist" );

                fi.Delete();
            }
        }

        [Test]
        public void File_is_written_on_every_snapshot_with_timerMs_0()
        {
            using( var client = CreateClient( 0 ) )
            {
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
        }

        [Test]
        public void File_is_written_automatically_with_positive_timerMs()
        {
            int timerMs = 300;
            using( var client = CreateClient( timerMs ) )
            {
                FileInfo fi = new FileInfo( client.Path );
                var d = new ObservableDomain<TestObservableRootObject>( client, TestHelper.Monitor );

                d.Modify( () =>
                {
                    d.Root.Prop1 = "Hello";
                    d.Root.Prop2 = "World";
                } );
                fi.Refresh();
                fi.Exists.Should().BeFalse( "File is flushed on timer tick, and should not exist yet" );

                Thread.Sleep( 2 * timerMs );
                fi.Refresh();
                fi.Exists.Should().BeTrue( "File was flushed on timer tick, and should exist" );


                fi.Delete();
            }
        }

        [Test]
        public void Load_throws_on_invalid_file()
        {
            using( var client = CreateClient( 0 ) )
            {
                File.WriteAllText( client.Path, "(INVALID FILE CONTENTS)" );
                FileInfo fi = new FileInfo( client.Path );

                Action act = () =>
                {
                    var d = new ObservableDomain<TestObservableRootObject>( client, TestHelper.Monitor );
                };

                // EndOfStreamException is an IOException.
                act.Should().Throw<IOException>( "ObservableDomain can't be created when given an invalid file" );

            }
        }

        [Test]
        public void Flush_throws_when_file_directory_does_not_exist()
        {
            NormalizedPath path = Path.Combine(
                Path.GetTempPath(),
                "this-directory-definitely-shouldnt-exist",
                "ObservableDomain.bin"
            );
            using( var client = CreateClient( -1, path ) )
            {
                var d = new ObservableDomain<TestObservableRootObject>( client, TestHelper.Monitor );
                d.Modify( () =>
                {
                    d.Root.Prop1 = "Hello";
                    d.Root.Prop2 = "World"
                        ;
                } );
                Action act = () =>
                {
                    client.Flush();
                };

                act.Should().Throw<IOException>( "ObservableDomain can't be flushed when target path does not exist" );
            }
        }

        [Test]
        public void TransactionResult_has_ClientError_when_file_directory_does_not_exist()
        {
            NormalizedPath path = Path.Combine(
                Path.GetTempPath(),
                "this-directory-definitely-shouldnt-exist",
                "ObservableDomain.bin"
            );
            using( var client = CreateClient( 0, path ) )
            {
                var d = new ObservableDomain<TestObservableRootObject>( client, TestHelper.Monitor );

                var transactionResult = d.Modify( () =>
                {
                    d.Root.Prop1 = "Hello";
                    d.Root.Prop2 = "World";
                } );

                transactionResult.ClientError.Should().NotBeNull( "ObservableDomain can't be flushed when target path does not exist" );
            }
        }

        [Test]
        public void Snapshot_file_can_be_saved_and_restored()
        {
            NormalizedPath path;

            // Create test file
            using( var client = CreateClient( 0 ) )
            {
                path = client.Path;
                var d1 = new ObservableDomain<TestObservableRootObject>( client, TestHelper.Monitor );
                d1.Modify( () =>
                {
                    d1.Root.Prop1 = "Hello";
                    d1.Root.Prop2 = "World";
                } );
            }

            // Read test file
            using( var client = CreateClient( 0, path ) )
            {
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
