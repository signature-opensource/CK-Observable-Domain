using CK.Core;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Observable.Domain.Tests
{
    class LockAndAsyncTests
    {
        class Locker : IDisposable
        {
            readonly ReaderWriterLockSlim _mrsw;
            readonly object _lock;

            public Locker( bool useMRSW )
            {
                if( useMRSW ) _mrsw = new ReaderWriterLockSlim( LockRecursionPolicy.NoRecursion );
                else _lock = new object();
            }

            public void Dispose()
            {
                IsLockHeld.Should().BeFalse();
                _mrsw?.Dispose();
            }

            public IDisposable AcquireLock()
            {
                if( _mrsw != null )
                {
                    _mrsw.EnterReadLock();
                    return Util.CreateDisposableAction( () => _mrsw.ExitReadLock() );
                }
                else
                {
                    Monitor.Enter( _lock );
                    return Util.CreateDisposableAction( () => Monitor.Exit( _lock ) );
                }
            }

            public void ExplicitRelease()
            {
                if( _mrsw != null ) _mrsw.ExitReadLock();
                else Monitor.Exit( _lock );
            }

            public bool IsLockHeld => _mrsw?.IsReadLockHeld ?? Monitor.IsEntered( _lock );

        }

        [TestCase( "UseMRSW" )]
        [TestCase( "UseMonitor" )]
        public void showing_how_Locks_and_RAII_work_fine_together_in_synchronous_mode( string mode )
        {
            bool useMRSW = mode == "UseMRSW";
            using( var locker = new Locker( useMRSW ) )
            {
                using( locker.AcquireLock() )
                {
                    locker.IsLockHeld.Should().BeTrue();
                }
                locker.IsLockHeld.Should().BeFalse();
            }
        }


        [TestCase( "UseMRSW" )]
        [TestCase( "UseMonitor" )]
        public void showing_how_Locks_and_async_can_be_dangerous( string mode )
        {
            bool useMRSW = mode == "UseMRSW";
            using( var locker = new Locker( useMRSW ) )
            {
                // Using a Wait() here removes any risk of thread changes here:
                // the "bug" occurs in the TestAsync method and here we can
                // see the result (the exception raised by the lock acquisition)
                // from the exact same thread that acquired the lock.
                var ex = TestAsync( locker ).GetAwaiter().GetResult();
                if( useMRSW )
                {
                    ex.Message.Should().Be( "The read lock is being released without being held." );
                }
                else
                {
                    ex.Message.Should().Be( "Object synchronization method was called from an unsynchronized block of code." );
                }

                locker.IsLockHeld.Should().BeTrue( "The lock IS NOT actually released... This thread still holds it." );

                // So we release it (otherwise, disposing the ReadWriterLockSlim while it is held throws).
                locker.ExplicitRelease();
            }
        }

        static async Task<Exception> TestAsync( Locker locker )
        {
            try
            {
                using( locker.AcquireLock() )
                {
                    locker.IsLockHeld.Should().BeTrue();
                    // Yield() is the clean way to force a real "async jump".
                    int initialThreadId = Thread.CurrentThread.ManagedThreadId;
                    await Task.Yield();

                    // We are NOT on the same thread!
                    // (And if we are, the whole test becomes useless: we use the NUnit Assume here.)
                    Assume.That( initialThreadId != Thread.CurrentThread.ManagedThreadId );
                    locker.IsLockHeld.Should().BeFalse( "The lock IS not held... but for THIS tread (from the thread pool)." );
                }
            }
            catch( Exception ex )
            {
                // The using finally tried to release the lock from a different thread that holds it.
                return ex;
            }
            Assert.Fail( "Can never be here!" );
            return null;
        }

    }
}