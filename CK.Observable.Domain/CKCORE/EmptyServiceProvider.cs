using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Core
{
    /// <summary>
    /// Simple empty implementation of a <see cref="IServiceProvider"/>.
    /// </summary>
    public class EmptyServiceProvider : IServiceProvider
    {
        /// <summary>
        /// Gets a singleton that should be used to avoid useless allocations.
        /// </summary>
        public static readonly IServiceProvider Default = new EmptyServiceProvider();

        object? IServiceProvider.GetService( Type serviceType ) => null;
    }
}
