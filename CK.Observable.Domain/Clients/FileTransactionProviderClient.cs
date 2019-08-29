using CK.Core;
using CK.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{

    /// <summary>
    /// Persistent implementation of <see cref="MemoryTransactionProviderClient"/>.
    /// </summary>
    public class FileTransactionProviderClient : MemoryTransactionProviderClient
    {
        readonly NormalizedPath _path;

        /// <summary>
        /// Initializes a new <see cref="MemoryTransactionProviderClient"/>.
        /// </summary>
        /// <param name="path">The path of the persistent file. The file may not exist.</param>
        /// <param name="next">The next manager (can be null).</param>
        public FileTransactionProviderClient( NormalizedPath path, IObservableDomainClient next = null )
            : base( next )
        {
            _path = path;
        }

        /// <summary>
        /// Loads the file if it exists (calls <see cref="MemoryTransactionProviderClient.LoadAndInitializeSnapshot(ObservableDomain, DateTime, Stream)"/>)).
        /// </summary>
        /// <param name="d">The newly created domain.</param>
        /// <param name="timeUtc">The date time utc of the creation.</param>
        public override void OnDomainCreated( ObservableDomain d, DateTime timeUtc )
        {
            if( File.Exists( _path ) )
            {
                using( var f = new FileStream( _path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan ) )
                {
                    LoadAndInitializeSnapshot( d, timeUtc, f );
                }
            }
            base.OnDomainCreated( d, timeUtc );
        }

        /// <summary>
        /// Default behavior is to create a snapshot (simply calls <see cref="CreateSnapshot"/> protected method).
        /// </summary>
        /// <param name="d">The associated domain.</param>
        /// <param name="timeUtc">The date time utc of the commit.</param>
        /// <param name="events">The events.</param>
        /// <param name="commands">The commands emitted by the transaction and that should be handled. Can be empty.</param>
        public override void OnTransactionCommit( ObservableDomain d, DateTime timeUtc, IReadOnlyList<ObservableEvent> events, IReadOnlyList<ObservableCommand> commands )
        {
            base.OnTransactionCommit( d, timeUtc, events, commands );
            using( var f = new FileStream( _path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.SequentialScan ) )
            {
                WriteSnapshotTo( f );
            }
        }

    }
}
