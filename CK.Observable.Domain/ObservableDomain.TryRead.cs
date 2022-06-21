using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Observable
{
    public partial class ObservableDomain
    {


        public readonly struct DisposableReadLock : IDisposable
        {
            readonly ReaderWriterLockSlim _l;
            public DisposableReadLock( ReaderWriterLockSlim l ) => _l = l;
            public void Dispose() => _l.ExitReadLock();
        }

        /// <summary>
        /// Acquires a single-threaded read lock on this <see cref="ObservableDomain"/>:
        /// until the returned disposable is disposed, objects can safely be read, and any attempt
        /// to call one of the ModifyAsync methods from other threads will be blocked.
        /// <para>
        /// Changing threads (typically by awaiting tasks) before the returned disposable is disposed
        /// will throw a <see cref="SynchronizationLockException"/>.
        /// </para>
        /// <para>
        /// Any attempt to call one of the ModifyAsync methods from this thread will throw a <see cref="LockRecursionException"/>.
        /// </para>
        /// </summary>
        /// <param name="millisecondsTimeout">
        /// The maximum number of milliseconds to wait for a read access before throwing.
        /// Wait indefinitely by default.
        /// </param>
        /// <returns>A disposable that releases the read lock when disposed.</returns>
        public DisposableReadLock AcquireReadLock( int millisecondsTimeout = -1 )
        {
            if( !_lock.TryEnterReadLock( millisecondsTimeout ) ) Throw.Exception( $"Unable to acquire read lock on domain '{DomainName}' in less than {millisecondsTimeout} ms." );
            CheckDisposed();
            return new DisposableReadLock( _lock );
        }

        /// <summary>
        /// Tries to read the domain by protecting the <paramref name="reader"/> function in a <see cref="ObservableDomain.AcquireReadLock(int)"/>.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="reader">The reader function.</param>
        /// <param name="millisecondsTimeout">
        /// The maximum number of milliseconds to wait for a read access before returning false.
        /// Wait indefinitely by default.
        /// </param>
        /// <returns>True if the read has been done, false on timeout.</returns>
        public bool TryRead( IActivityMonitor monitor, Action reader, int millisecondsTimeout = -1 )
        {
            if( !_lock.TryEnterReadLock( millisecondsTimeout ) ) return false;
            try
            {
                reader();
                return true;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Tries to read the domain by protecting the <paramref name="reader"/> function in a <see cref="ObservableDomain.AcquireReadLock(int)"/>.
        /// </summary>
        /// <typeparam name="T">The type of the information to read.</typeparam>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="reader">The reader function that projects read information into a <typeparamref name="T"/>.</param>
        /// <param name="millisecondsTimeout">
        /// The maximum number of milliseconds to wait for a read access before returning the <paramref name="defaultValue"/>.
        /// Wait indefinitely by default.
        /// </param>
        /// <returns>True if the read has been done, false on timeout.</returns>
        public bool TryRead<T>( IActivityMonitor monitor, Func<T> reader, [MaybeNullWhen( false )] out T result, int millisecondsTimeout )
        {
            if( !_lock.TryEnterReadLock( millisecondsTimeout ) )
            {
                result = default;
                return false;
            }
            try
            {
                result = reader();
                return true;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }
}
