using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    /// <summary>
    /// Stream store abstracts storages, exposes them as Streams and handles compression. 
    /// </summary>
    public interface IStreamStore
    {
        /// <summary>
        /// Gets whether an entry exists. 
        /// Recalls that names are case insensitive.
        /// </summary>
        /// <param name="fullName">The entry name.</param>
        /// <returns>True if the entry exists, false otherwise.</returns>
        Task<bool> ExistsAsync( string fullName );

        /// <summary>
        /// Tries to open a stream on an existing resource.
        /// <see cref="StoredStream.Stream"/> is null if the resource does not exist.
        /// </summary>
        /// <param name="fullName">The resource full name (case insensitive).</param>
        /// <returns>An opened readable stream along with its compression kind and last write time (or a null <see cref="StoredStream.Stream"/> if it does not exist).</returns>
        Task<TimedStoredStream> OpenReadAsync( string fullName );

        /// <summary>
        /// Creates a new entry with an initial content.
        /// </summary>
        /// <param name="fullName">The resource full name (case insensitive).</param>
        /// <param name="writer">Stream writer action.</param>
        /// <param name="storageKind">Specifies the content's stream storage compression. The <paramref name="writer"/> must write in the specified compression model.</param>
        /// <returns>The creation time in Utc. Can be used for optimistic concurrency check.</returns>
        Task<DateTime> CreateAsync( string fullName, Func<Stream,Task> writer, CompressionKind storageKind );

        /// <summary>
        /// Updates an entry, optionally allow creating it if it does not exists and optionally
        /// handles optimistic concurrency: the only case where this method must return false
        /// is when <paramref name="checkLastWriteTimeUtc"/> is provided and do not match the current
        /// last write time of the entry.
        /// </summary>
        /// <param name="fullName">The resource full name (case insensitive).</param>
        /// <param name="writeContent">Stream writer action.</param>
        /// <param name="storageKind">Specifies the content's stream storage compression. This updates any existing configuration.</param>
        /// <param name="allowCreate">True to automatically create the entry if it doesn't already exist.</param>
        /// <param name="checkLastWriteTimeUtc">Optional optimistic concurrency check.</param>
        /// <returns>The new last write time in Utc or <see cref="CK.Core.Util.UtcMaxValue"/> if optimistic concurrency check failed.</returns>
        Task<DateTime> UpdateAsync( string fullName, Func<Stream,Task> writeContent, CompressionKind storageKind, bool allowCreate = false, DateTime checkLastWriteTimeUtc = default );

        /// <summary>
        /// Deletes an entry. This is idempotent: no error if it does not exists.
        /// </summary>
        /// <param name="fullName">The full name of the resource to destroy (case insensitive).</param>
        /// <returns>The awaitable.</returns>
        Task DeleteAsync( string fullName );

        /// <summary>
        /// Deletes all files whose fullname matches a predicate.
        /// Recalls that names are case insensitive.
        /// </summary>
        /// <param name="predicate">The predicate.</param>
        /// <returns>The number of deleted entries.</returns>
        Task<int> DeleteAsync( Func<string, bool> predicate );

        /// <summary>
        /// Flushes any intermediate data.
        /// </summary>
        /// <returns>The awaitable.</returns>
        Task FlushAsync();
    }
}
