using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    /// <summary>
    /// Captures a <see cref="Stream"/>, its <see cref="CompressionKind"/>
    /// and its <see cref="LastWriteTimeUtc"/>.
    /// </summary>
    public readonly struct TimedStoredStream
    {
        /// <summary>
        /// The kind of compression of the <see cref="Stream"/>.
        /// </summary>
        public readonly StoredStream StoredStream;

        /// <summary>
        /// The kind of compression of the <see cref="Stream"/>.
        /// </summary>
        public CompressionKind Kind => StoredStream.Kind;

        /// <summary>
        /// The actual stream.
        /// Null if the stream does not exist.
        /// </summary>
        public Stream Stream => StoredStream.Stream;

        /// <summary>
        /// The last write time. Used for optimistic concurrency.
        /// </summary>
        public readonly DateTime LastWriteTimeUtc;

        /// <summary>
        /// Initializes a new <see cref="LocalStoredStream"/> descriptor.
        /// </summary>
        /// <param name="k">The compression kind.</param>
        /// <param name="s">The stream itself.</param>
        /// <param name="lastWriteTimeUtc">The last write time.</param>
        public TimedStoredStream( CompressionKind k, Stream s, DateTime lastWriteTimeUtc )
            : this( new StoredStream( k, s ), lastWriteTimeUtc )
        {
        }

        /// <summary>
        /// Initializes a new <see cref="LocalStoredStream"/> descriptor.
        /// </summary>
        /// <param name="s">The stored stream.</param>
        /// <param name="lastWriteTimeUtc">The last write time.</param>
        public TimedStoredStream( in StoredStream s, DateTime lastWriteTimeUtc )
        {
            if( lastWriteTimeUtc.Kind != DateTimeKind.Utc ) throw new ArgumentException( "Must be Utc.", nameof( lastWriteTimeUtc ) );
            StoredStream = s;
            LastWriteTimeUtc = lastWriteTimeUtc;
        }

    }
}
