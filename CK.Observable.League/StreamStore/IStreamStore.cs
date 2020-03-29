using CK.Core;
using System;
using System.IO;
using System.Threading.Tasks;

namespace CK.Observable
{
    /// <summary>
    /// Baic abstraction for storages. 
    /// </summary>
    public interface IStreamStore
    {
        /// <summary>
        /// Gets whether an entry exists. 
        /// Recalls that names are case insensitive.
        /// </summary>
        /// <param name="name">The entry name.</param>
        /// <returns>True if the entry exists, false otherwise.</returns>
        Task<bool> ExistsAsync( string name );

        /// <summary>
        /// Tries to open a stream on an existing resource. Null if the resource does not exist.
        /// </summary>
        /// <param name="name">The resource name (case insensitive).</param>
        /// <returns>An opened readable stream, null if it does not exist.</returns>
        Task<Stream?> OpenReadAsync( string name );

        /// <summary>
        /// Creates a new entry with an initial content.
        /// </summary>
        /// <param name="name">The resource full name (case insensitive).</param>
        /// <param name="writer">Stream writer action.</param>
        /// <returns>The creation time in Utc. Can be used for optimistic concurrency check.</returns>
        Task<DateTime> CreateAsync( string name, Func<Stream,Task> writer );

        /// <summary>
        /// Updates an entry, optionally allow creating it if it does not exists and optionally
        /// handles optimistic concurrency (<see cref="Util.UtcMaxValue"/> is returned if optimistic concurrency check failed).
        /// </summary>
        /// <param name="name">The resource name (case insensitive).</param>
        /// <param name="writeContent">Stream writer action.</param>
        /// <param name="allowCreate">True to automatically create the entry if it doesn't already exist.</param>
        /// <param name="checkLastWriteTimeUtc">Optional optimistic concurrency check.</param>
        /// <returns>The new last write time in Utc or <see cref="Util.UtcMaxValue"/> if optimistic concurrency check failed.</returns>
        Task<DateTime> UpdateAsync( string name, Func<Stream,Task> writeContent, bool allowCreate = false, DateTime checkLastWriteTimeUtc = default );

        /// <summary>
        /// Deletes an entry. This is idempotent: no error if it does not exists.
        /// </summary>
        /// <param name="name">The name of the resource to destroy (case insensitive).</param>
        /// <param name="archive">True to send the resource to the archives instead of deleting it.</param>
        /// <returns>The awaitable.</returns>
        Task DeleteAsync( string name, bool archive );

        /// <summary>
        /// Deletes all files whose name matches a predicate.
        /// Recalls that names are case insensitive.
        /// </summary>
        /// <param name="predicate">The predicate.</param>
        /// <param name="archive">True to send the resources to the archives instead of deleting them.</param>
        /// <returns>The number of deleted entries.</returns>
        Task<int> DeleteAsync( Func<string, bool> predicate, bool archive );

    }
}
