using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable
{
    /// <summary>
    /// Specify compression kind.
    /// </summary>
    public enum CompressionKind
    {
        /// <summary>
        /// The stream contains the actual, uncompressed, data.
        /// </summary>
        None,

        /// <summary>
        /// The stream is a gziped stream of the actual data.
        /// </summary>
        GZiped
    }
}
