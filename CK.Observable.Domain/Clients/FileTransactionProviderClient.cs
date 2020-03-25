using CK.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using CK.Core;
using System.Threading.Tasks;

namespace CK.Observable
{

    /// <summary>
    /// Persistent implementation of <see cref="MemoryTransactionProviderClient"/>.
    /// Loads the <see cref="ObservableDomain"/> from the given file path,
    /// and stores it at regular intervals - or when calling <see cref="Flush"/>.
    /// </summary>
    public class FileTransactionProviderClient : MemoryTransactionProviderClient
    {
        readonly NormalizedPath _filePath;
        readonly NormalizedPath _tmpFilePath;
        readonly NormalizedPath _bakFilePath;
        readonly int _minimumDueTimeMs;
        readonly TimeSpan _minimumDueTimeSpan;
        readonly object _fileLock;
        int _fileTransactionNumber;
        DateTime _nextDueTimeUtc;

        /// <summary>
        /// Initializes a new <see cref="MemoryTransactionProviderClient"/>.
        /// </summary>
        /// <param name="filePath">
        /// The path to the persistent file to load the domain from.
        /// This file may not exist.
        /// </param>
        /// <param name="minimumDueTimeMs">
        /// Minimum number of milliseconds between each file save, checked on every <see cref="OnTransactionCommit"/>.
        /// If -1: The file will not be saved automatically. <see cref="Flush"/> must be manually called.
        /// If 0: Every transaction will write the file.
        /// </param>
        /// <param name="next">The next manager (can be null).</param>
        public FileTransactionProviderClient( NormalizedPath filePath, int minimumDueTimeMs, IObservableDomainClient next = null )
            : base( next )
        {
            if( minimumDueTimeMs < -1 ) throw new ArgumentException( $"{minimumDueTimeMs} is not a valid value. Valid values are -1, 0, or above.", nameof( minimumDueTimeMs ) );
            _filePath = filePath;
            _tmpFilePath = _filePath + ".tmp";
            _bakFilePath = _filePath + ".bak";

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
        public NormalizedPath FilePath => _filePath;

        /// <summary>
        /// Loads the file if it exists, calling base method
        /// <see cref="MemoryTransactionProviderClient.LoadAndInitializeSnapshot(IActivityMonitor, ObservableDomain, DateTime, Stream)"/>)).
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="d">The newly created domain.</param>
        public override void OnDomainCreated( IActivityMonitor monitor, ObservableDomain d )
        {
            lock( _fileLock )
            {
                if( File.Exists( _filePath ) )
                {
                    using( var f = new FileStream( _filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan ) )
                    {
                        LoadAndInitializeSnapshot( monitor, d, f );
                        _fileTransactionNumber = CurrentSerialNumber;
                    }
                }
            }
            RescheduleDueTime( DateTime.UtcNow );

            base.OnDomainCreated( monitor, d );
        }

        /// <inheritdoc />
        public override void OnTransactionCommit( in SuccessfulTransactionContext c )
        {
            base.OnTransactionCommit( c );
            if( _minimumDueTimeMs == 0 )
            {
                // Write every snapshot
                WriteFileIfNeeded();
            }
            else if( _minimumDueTimeMs > 0 && c.CommitTimeUtc > _nextDueTimeUtc )
            {
                // Write snapshot if due, then reschedule it.
                WriteFileIfNeeded();
                RescheduleDueTime( c.CommitTimeUtc );
            }
        }

        /// <summary>
        /// Writes any pending snapshot to the disk if something changed.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <returns>
        /// True when the file was written, or when nothing has to be written.
        /// False when an error occured while writing. This error was logged to the <paramref name="monitor"/>.
        /// </returns>
        public bool Flush( IActivityMonitor monitor )
        {
            try
            {
                WriteFileIfNeeded();
                if( _minimumDueTimeMs > 0 )
                {
                    RescheduleDueTime( DateTime.UtcNow );
                }
                return true;
            }
            catch( Exception e )
            {
                monitor.Error( "Caught when writing ObservableDomain file", e );
                return false;
            }
        }

        private void RescheduleDueTime( DateTime relativeTimeUtc )
        {
            _nextDueTimeUtc = relativeTimeUtc + _minimumDueTimeSpan;
        }

        void WriteFileIfNeeded()
        {
            if( _fileTransactionNumber != CurrentSerialNumber )
            {
                lock( _fileLock )
                {
                    if( _fileTransactionNumber != CurrentSerialNumber )
                    {
                        using( var f = new FileStream( _tmpFilePath, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.SequentialScan ) )
                        {
                            WriteSnapshot( f );
                        }

                        if( File.Exists( _filePath ) )
                        {
                            File.Replace(
                                _tmpFilePath,
                                _filePath,
                                _bakFilePath,
                                true
                            );
                        }
                        else
                        {
                            File.Move(
                                _tmpFilePath,
                                _filePath
                            );
                        }

                        _fileTransactionNumber = CurrentSerialNumber;
                    }
                }
            }
        }
    }
}
