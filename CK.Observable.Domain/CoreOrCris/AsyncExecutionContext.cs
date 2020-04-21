using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable.Domain
{

    /// <summary>
    /// Closed implementation of the <see cref="AsyncExecutionContext{TThis}"/> base without
    /// any additional features.
    /// </summary>
    public sealed class AsyncExecutionContext : AsyncExecutionContext<AsyncExecutionContext>
    {
        /// <summary>
        /// Initializes an asynchronous execution context.
        /// </summary>
        /// <param name="monitor">The monitor that must be used.</param>
        public AsyncExecutionContext( IActivityMonitor monitor )
            : base( monitor )
        {
        }

        /// <summary>
        /// Initializes an execution context bound to an external memory.
        /// </summary>
        /// <param name="monitor">The monitor that must be used.</param>
        /// <param name="externalMemory">External memory.</param>
        /// <param name="callMemoryDisposable">
        /// True to call <see cref="IAsyncDisposable.DisposeAsync"/> or <see cref="IDisposable.Dispose"/> on
        /// all disposable <see cref="Memory"/>'s values.
        /// </param>
        public AsyncExecutionContext( IActivityMonitor monitor, IDictionary<object, object> externalMemory, bool callMemoryDisposable )
            : base( monitor )
        {
        }

    }
}
