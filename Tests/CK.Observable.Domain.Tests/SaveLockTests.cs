using CK.Core;
using CK.Observable.Domain.Tests.Clients;
using NUnit.Framework;
using Shouldly;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static CK.Testing.MonitorTestHelper;

namespace CK.Observable.Domain.Tests;

[TestFixture]
public class SaveLockTests
{
    [Test]
    public async Task external_Save_does_not_deadlock_with_a_commit_snapshot_Async()
    {
        var d = new ObservableDomain<TestObservableRootObject>( TestHelper.Monitor,
                                                                nameof( external_Save_does_not_deadlock_with_a_commit_snapshot_Async ),
                                                                startTimer: false,
                                                                client: new ConcreteMemoryTransactionProviderClient() );
        var saveLock = d.GetSaveLock();
        Task<bool>? save = null;
        // The commit calls MemoryTransactionProviderClient.CreateSnapshot that calls d.Save under the write lock.
        var modify = Task.Run( () => d.TryModifyAsync( TestHelper.Monitor, () =>
        {
            d.Root.Prop1 = "Hello";
            save = Task.Run( () => d.Save( new ActivityMonitor(), new MemoryStream() ) );
            // Waits (at most 1 second) for the external Save to acquire the _saveLock.
            // When the lock order is right, the external Save waits for the domain lock
            // before the _saveLock and this loop times out.
            var limit = DateTime.UtcNow.AddSeconds( 1 );
            while( DateTime.UtcNow < limit && Monitor.TryEnter( saveLock ) )
            {
                Monitor.Exit( saveLock );
                Thread.Sleep( 5 );
            }
        } ) );
        var completed = await Task.WhenAny( modify, Task.Delay( 10_000 ) );
        ReferenceEquals( completed, modify ).ShouldBeTrue( "The commit must not deadlock with the external Save." );
        var tr = await modify;
        tr.Success.ShouldBeTrue( $"{tr} {tr.ClientError?.Message} {string.Join( ", ", tr.Errors.Select( e => e.Message ) )}" );
        (save != null).ShouldBeTrue();
        (await save!.WaitAsync( TimeSpan.FromSeconds( 10 ) )).ShouldBeTrue();
        d.Dispose();
    }

    [Test]
    public async Task concurrent_external_Saves_all_succeed_Async()
    {
        // No using: when a lock leaks, Dispose waits forever.
        var d = new ObservableDomain<TestObservableRootObject>( TestHelper.Monitor,
                                                                nameof( concurrent_external_Saves_all_succeed_Async ),
                                                                startTimer: false );
        await d.ModifyThrowAsync( TestHelper.Monitor, () => d.Root.Prop1 = new string( 'x', 100_000 ) );
        // The _saveLock must be released only once the serializer has released the shared serializer context:
        // a Save that acquires the _saveLock in between fails to create its serializer.
        for( int round = 0; round < 50; ++round )
        {
            var saves = Enumerable.Range( 0, 8 )
                                  .Select( _ => Task.Run( () => d.Save( new ActivityMonitor(), new MemoryStream() ) ) )
                                  .ToArray();
            var results = await Task.WhenAll( saves ).WaitAsync( TimeSpan.FromSeconds( 10 ) );
            results.ShouldAllBe( r => r );
        }
        d.Dispose();
    }

    [Test]
    public async Task Save_timeout_on_the_domain_lock_returns_false_and_takes_no_lock_Async()
    {
        using var d = new ObservableDomain<TestObservableRootObject>( TestHelper.Monitor,
                                                                      nameof( Save_timeout_on_the_domain_lock_returns_false_and_takes_no_lock_Async ),
                                                                      startTimer: false );
        var saveLock = d.GetSaveLock();
        await d.ModifyThrowAsync( TestHelper.Monitor, () =>
        {
            var saveResult = Task.Run( () => d.Save( new ActivityMonitor(), new MemoryStream(), millisecondsTimeout: 50 ) ).Result;
            saveResult.ShouldBeFalse( "The write lock is held by this transaction." );
            Monitor.TryEnter( saveLock ).ShouldBeTrue( "The _saveLock is free." );
            Monitor.Exit( saveLock );
        } );
        d.Save( TestHelper.Monitor, new MemoryStream(), millisecondsTimeout: 1000 ).ShouldBeTrue();
    }

    [Test]
    public async Task Save_timeout_on_the_save_lock_returns_false_and_releases_the_domain_lock_Async()
    {
        using var d = new ObservableDomain<TestObservableRootObject>( TestHelper.Monitor,
                                                                      nameof( Save_timeout_on_the_save_lock_returns_false_and_releases_the_domain_lock_Async ),
                                                                      startTimer: false );
        var saveLock = d.GetSaveLock();
        using( var holderReady = new ManualResetEventSlim() )
        using( var releaseHolder = new ManualResetEventSlim() )
        {
            var holder = Task.Run( () =>
            {
                lock( saveLock )
                {
                    holderReady.Set();
                    releaseHolder.Wait();
                }
            } );
            holderReady.Wait();
            d.Save( TestHelper.Monitor, new MemoryStream(), millisecondsTimeout: 50 ).ShouldBeFalse( "The _saveLock is held by another thread." );
            releaseHolder.Set();
            await holder;
        }
        // If the read lock leaked, this transaction cannot start (no recursion, or no write lock).
        var tr = await d.TryModifyAsync( TestHelper.Monitor, () => d.Root.Prop1 = "Hello", millisecondsTimeout: 1000 );
        tr.Success.ShouldBeTrue();
    }
}
