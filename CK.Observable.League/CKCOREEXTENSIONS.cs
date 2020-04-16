using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CK.Core
{

    public static class ValueTaskExtensions
    {
        /// <summary>
        /// Transforms a <see cref="ValueTask{TResult}"/> into a non generic <see cref="ValueTask"/>.
        /// Adapted from https://github.com/dotnet/runtime/issues/31503#issuecomment-554415966
        /// </summary>
        /// <typeparam name="T">The result type.</typeparam>
        /// <param name="valueTask">This value task.</param>
        /// <returns>The non generic value task.</returns>
        public static ValueTask AsNonGenericValueTask<T>( in this ValueTask<T> valueTask )
        {
            if( valueTask.IsCompletedSuccessfully )
            {
                // The Resut must be obtained since if the backup is IValueTaskSource
                // it needs this "ack" to be freed.
                T fake = valueTask.Result;
                return default;
            }
            return new ValueTask( valueTask.AsTask() );
        }
    }
}
