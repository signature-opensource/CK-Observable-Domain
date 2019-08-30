using CK.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using CK.Core;

namespace CK.Observable
{

    /// <summary>
    /// Persistent implementation of <see cref="MemoryTransactionProviderClient"/>.
    /// Loads the <see cref="ObservableDomain"/> from the given file path,
    /// and stores it at regular intervals - or when calling <see cref="Flush"/>.
    /// </summary>
    public class FileTransactionProviderClient : MemoryTransactionProviderClient
    {
        readonly NormalizedPath _path;
        readonly int _minimumDueTimeMs;
        readonly TimeSpan _minimumDueTimeSpan;
        readonly object _fileLock;
        int _fileTransactionNumber;
        DateTime _nextDueTimeUtc;

        /// <summary>
        /// Initializes a new <see cref="MemoryTransactionProviderClient"/>.
        /// </summary>
        /// <param name="path">
        /// The path to the persistent file to load the domain from.
        /// This file may not exist.
        /// </param>
        /// <param name="minimumDueTimeMs">
        /// Minimum number of milliseconds between each file save, checked on every <see cref="OnTransactionCommit"/>.
        /// If -1: The file will not be saved automatically. <see cref="Flush"/> must be manually called.
        /// If 0: Every transaction will write the file.
        /// </param>
        /// <param name="next">The next manager (can be null).</param>
        public FileTransactionProviderClient( NormalizedPath path, int minimumDueTimeMs, IObservableDomainClient next = null )
            : base( next )
        {
            if( minimumDueTimeMs < -1 ) throw new ArgumentException( $"{minimumDueTimeMs} is not a valid value. Valid values are -1, 0, or above.", nameof( minimumDueTimeMs ) );
            _path = path;
            _minimumDueTimeMs = minimumDueTimeMs;
            _fileLock = new object();
            _nextDueTimeUtc = DateTime.UtcNow; // This is re-scheduled in OnDomainCreated
            if( minimumDueTimeMs > 0 )
            {
                _minimumDueTimeSpan = TimeSpan.FromMilliseconds( minimumDueTimeMs );
            }
            else
            {
                _minimumDueTimeSpan = TimeSpan.Zero;
            }
        }

        /// <summary>
        /// The file path provided to this instance.
        /// </summary>
        public NormalizedPath Path => _path;

        /// <summary>
        /// Loads the file if it exists (calls <see cref="MemoryTransactionProviderClient.LoadAndInitializeSnapshot(ObservableDomain, DateTime, Stream)"/>)).
        /// </summary>
        /// <param name="d">The newly created domain.</param>
        /// <param name="timeUtc">The date time utc of the creation.</param>
        public override void OnDomainCreated( ObservableDomain d, DateTime timeUtc )
        {
            lock( _fileLock )
            {
                if( File.Exists( _path ) )
                {
                    using( var f = new FileStream( _path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096,
                        FileOptions.SequentialScan ) )
                    {
                        LoadAndInitializeSnapshot( d, timeUtc, f );
                        _fileTransactionNumber = CurrentSerialNumber;
                    }
                }
            }
            RescheduleDueTime( timeUtc );

            base.OnDomainCreated( d, timeUtc );
        }

        /// <inheritdoc />
        public override void OnTransactionCommit( ObservableDomain d, DateTime timeUtc, IReadOnlyList<ObservableEvent> events, IReadOnlyList<ObservableCommand> commands )
        {
            base.OnTransactionCommit( d, timeUtc, events, commands );

            if( _minimumDueTimeMs == 0 )
            {
                // Write every snapshot
                DoWriteFile();
            }
            else if( _minimumDueTimeMs > 0 && timeUtc > _nextDueTimeUtc )
            {
                // Write snapshot if due, then reschedule it.
                DoWriteFile();
                RescheduleDueTime( timeUtc );
            }
        }


        /// <summary>
        /// Writes any pending snapshot to the disk,
        /// without waiting for the next timer tick.
        /// </summary>
        /// <param name="m">The monitor to use</param>
        /// <returns>
        /// True when the file was written, or when nothing has to be written.
        /// False when an error occured while writing. This error was logged to <paramref name="m"/>.
        /// </returns>
        public bool Flush( IActivityMonitor m )
        {
            try
            {
                DoWriteFile();
                if( _minimumDueTimeMs > 0 )
                {
                    RescheduleDueTime( DateTime.UtcNow );
                }
                return true;
            }
            catch( Exception e )
            {
                m.Error( "Caught when writing ObservableDomain file", e );
                return false;
            }
        }

        private void RescheduleDueTime( DateTime relativeTimeUtc )
        {
            _nextDueTimeUtc = relativeTimeUtc + _minimumDueTimeSpan;
        }

        private void DoWriteFile()
        {
            lock( _fileLock )
            {
                if( _fileTransactionNumber != CurrentSerialNumber )
                {
                    using( var f = new FileStream( _path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.SequentialScan ) )
                    {
                        WriteSnapshotTo( f );
                    }

                    _fileTransactionNumber = CurrentSerialNumber;
                }
            }
        }
    }
}
