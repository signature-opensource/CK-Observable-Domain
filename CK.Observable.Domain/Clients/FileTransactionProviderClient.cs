using CK.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace CK.Observable
{

    /// <summary>
    /// Persistent implementation of <see cref="MemoryTransactionProviderClient"/>.
    /// Loads the <see cref="ObservableDomain"/> from the given file path,
    /// and stores it at regular intervals - or when calling <see cref="Flush"/>.
    /// </summary>
    public class FileTransactionProviderClient : MemoryTransactionProviderClient, IDisposable
    {
        readonly NormalizedPath _path;
        readonly int _timerPeriodMs;
        readonly object _fileLock;
        int _fileTransactionNumber;
        Timer _timer;

        /// <summary>
        /// Initializes a new <see cref="MemoryTransactionProviderClient"/>.
        /// </summary>
        /// <param name="path">
        /// The path to the persistent file to load the domain from.
        /// This file may not exist.
        /// </param>
        /// <param name="timerPeriodMs">
        /// Number of milliseconds between each file save.
        /// If -1: The file will not be saved automatically. <see cref="Flush"/> must be manually called.
        /// If 0: Every transaction will write the file.
        /// </param>
        /// <param name="next">The next manager (can be null).</param>
        public FileTransactionProviderClient( NormalizedPath path, int timerPeriodMs, IObservableDomainClient next = null )
            : base( next )
        {
            if( timerPeriodMs < -1 ) throw new ArgumentException( $"{timerPeriodMs} is not a valid value. Valid values are -1, 0, or above.", nameof( timerPeriodMs ) );
            _path = path;
            _timerPeriodMs = timerPeriodMs;
            _fileLock = new object();
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

            base.OnDomainCreated( d, timeUtc );

            _timer?.Dispose();
            _timer = new Timer( TimerCallback, null, _timerPeriodMs, _timerPeriodMs );
        }

        /// <inheritdoc />
        public override void OnTransactionCommit( ObservableDomain d, DateTime timeUtc, IReadOnlyList<ObservableEvent> events, IReadOnlyList<ObservableCommand> commands )
        {
            base.OnTransactionCommit( d, timeUtc, events, commands );

            if( _timerPeriodMs == 0 )
            {
                DoWriteFile();
            }
        }


        /// <summary>
        /// Writes any pending snapshot to the disk,
        /// without waiting for the next timer tick.
        /// </summary>
        public void Flush()
        {
            DoWriteFile();
        }

        private void TimerCallback( object state )
        {
            DoWriteFile();
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

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
