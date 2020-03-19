using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    /// <summary>
    /// Captures a <see cref="Stream"/> and its <see cref="CompressionKind"/>.
    /// </summary>
    public readonly struct StoredStream
    {
        /// <summary>
        /// The kind of compression of the <see cref="Stream"/>.
        /// </summary>
        public readonly CompressionKind Kind;

        /// <summary>
        /// The actual stream.
        /// Null if the stream does not exist.
        /// </summary>
        public readonly Stream Stream;

        /// <summary>
        /// Initializes a new <see cref="StoredStream"/> descriptor.
        /// </summary>
        /// <param name="k">The compression kind.</param>
        /// <param name="s">The stream itself.</param>
        /// <param name="lastWriteTimeUtc">The last write time.</param>
        public StoredStream( CompressionKind k, Stream s )
        {
            Kind = k;
            Stream = s;
        }
    }
}
