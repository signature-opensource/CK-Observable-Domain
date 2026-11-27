using System.IO;
using NUnit.Framework;
using Shouldly;

namespace CK.Observable.PersistentDomainDriverTests;

[TestFixture]
public class FolderLockTests
{
    [Test]
    public void second_acquire_on_the_same_file_returns_null()
    {
        var path = Path.Combine( TestStore.NewDirectory(), "D.lock" );
        using var first = FolderLock.TryAcquire( path );
        first.ShouldNotBeNull();
        FolderLock.TryAcquire( path ).ShouldBeNull();
    }

    [Test]
    public void released_lock_can_be_taken_again_and_the_file_stays()
    {
        var path = Path.Combine( TestStore.NewDirectory(), "D.lock" );
        var first = FolderLock.TryAcquire( path );
        first.ShouldNotBeNull();
        first.Dispose();
        File.Exists( path ).ShouldBeTrue();
        using var second = FolderLock.TryAcquire( path );
        second.ShouldNotBeNull();
    }
}
