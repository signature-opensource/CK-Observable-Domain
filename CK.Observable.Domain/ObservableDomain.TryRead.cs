using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    public partial class ObservableDomain
    {
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
        public bool TryRead( IActivityMonitor monitor, Action reader, int millisecondsTimeout )
        {
            var l = AcquireReadLock( millisecondsTimeout );
            if( l == null ) return false;
            try
            {
                reader();
                return true;
            }
            finally
            {
                l.Dispose();
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
            var l = AcquireReadLock( millisecondsTimeout );
            if( l == null )
            {
                result = default( T );
                return false;
            }
            try
            {
                result = reader();
                return true;
            }
            finally
            {
                l.Dispose();
            }
        }
    }
}
