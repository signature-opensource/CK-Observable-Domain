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
        /// <summary>
        /// Tries to read the domain by protecting the <paramref name="reader"/> function in read lock.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="reader">The reader function.</param>
        /// <param name="millisecondsTimeout">
        /// The maximum number of milliseconds to wait for a read access before returning false.
        /// </param>
        /// <returns>True if the read has been done, false on timeout.</returns>
        public bool TryRead( IActivityMonitor monitor, Action reader, int millisecondsTimeout )
        {
            var g = monitor.OpenDebug( $"Trying to read domain '{DomainName}' in less than {millisecondsTimeout} ms." );
            if( !_lock.TryEnterReadLock( millisecondsTimeout ) )
            {
                monitor.Warn( $"Failed to obtain the read lock on '{DomainName}' in less than {millisecondsTimeout} ms." );
                g.Dispose();
                return false;
            }
            try
            {
                reader();
                return true;
            }
            finally
            {
                _lock.ExitReadLock();
                g.Dispose();
            }
        }

        /// <summary>
        /// Tries to read the domain by protecting the <paramref name="reader"/> function in read lock.
        /// </summary>
        /// <typeparam name="T">The type of the information to read.</typeparam>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="reader">The reader function that projects read information into a <typeparamref name="T"/>.</param>
        /// <param name="result">The value returned by the reader function.</param>
        /// <param name="millisecondsTimeout">
        /// The maximum number of milliseconds to wait for a read access before returning false.
        /// </param>
        /// <returns>True if the read has been done, false on timeout.</returns>
        public bool TryRead<T>( IActivityMonitor monitor, Func<T> reader, [MaybeNullWhen( false )] out T result, int millisecondsTimeout )
        {
            var g = monitor.OpenDebug( $"Trying to read domain '{DomainName}' in less than {millisecondsTimeout} ms." );
            if( !_lock.TryEnterReadLock( millisecondsTimeout ) )
            {
                result = default;
                monitor.Warn( $"Failed to obtain the read lock on '{DomainName}' in less than {millisecondsTimeout} ms." );
                g.Dispose();
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
                g.Dispose();
            }
        }

        /// <summary>
        /// Read the domain by protecting the <paramref name="reader"/> function in read lock.
        /// </summary>
        /// <typeparam name="T">The type of the information to read.</typeparam>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="reader">The reader function that projects read information into a <typeparamref name="T"/>.</param>
        /// <returns>The read value.</returns>
        public T Read<T>( IActivityMonitor monitor, Func<T> reader )
        {
            var g = monitor.OpenDebug( $"Reading domain '{DomainName}'." );
            _lock.EnterReadLock();
            try
            {
                return reader();
            }
            finally
            {
                _lock.ExitReadLock();
                g.Dispose();
            }
        }

        /// <summary>
        /// Read the domain by protecting the <paramref name="reader"/> action in read lock.
        /// </summary>
        /// <typeparam name="T">The type of the information to read.</typeparam>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="reader">The reader action.</param>
        public void Read( IActivityMonitor monitor, Action reader )
        {
            var g = monitor.OpenDebug( $"Reading domain '{DomainName}'." );
            _lock.EnterReadLock();
            try
            {
                reader();
            }
            finally
            {
                _lock.ExitReadLock();
                g.Dispose();
            }
        }
    }
}
