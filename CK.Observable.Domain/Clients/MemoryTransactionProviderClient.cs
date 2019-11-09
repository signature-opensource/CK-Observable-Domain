using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace CK.Observable
{

    /// <summary>
    /// This is a simple, yet useful, participant that implements transaction in memory.
    /// It is open to extensions and can be used as a base class: the <see cref="FileTransactionProviderClient"/> extends this.
    /// The protected <see cref="LoadAndInitializeSnapshot"/> and <see cref="WriteSnapshotTo"/> methods offers access to
    /// the internal memory.
    /// </summary>
    public class MemoryTransactionProviderClient : IObservableDomainClient
    {
        readonly IObservableDomainClient _next;
        readonly MemoryStream _memory;
        int _snapshotSerialNumber;
        DateTime _snapshotTimeUtc;

        /// <summary>
        /// Initializes a new <see cref="MemoryTransactionProviderClient"/>.
        /// </summary>
        /// <param name="next">The next manager (can be null).</param>
        public MemoryTransactionProviderClient( IObservableDomainClient next = null )
        {
            _next = next;
            _memory = new MemoryStream( 16 * 1024 );
            _snapshotSerialNumber = -1;
            _snapshotTimeUtc = Util.UtcMinValue;
        }

        /// <summary>
        /// Gets the current, snapshot, serial number.
        /// Defaults to -1 when there is no snapshot.
        /// </summary>
        public int CurrentSerialNumber => _snapshotSerialNumber;

        /// <summary>
        /// Gets the time of the current snapshot.
        /// Defaults to <see cref="Util.UtcMinValue"/> when there is no snapshot.
        /// </summary>
        public DateTime CurrentTimeUtc => _snapshotTimeUtc;

        /// <summary>
        /// Gets whether a snapshot is available.
        /// </summary>
        public bool HasSnapshot => _snapshotSerialNumber >= 0;

        /// <summary>
        /// Called when the domain instance is created.
        /// By default, does nothing.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="timeUtc">The date time utc of the creation.</param>
        /// <param name="d">The newly created domain.</param>
        public virtual void OnDomainCreated( IActivityMonitor monitor, ObservableDomain d, DateTime timeUtc )
        {
            _next?.OnDomainCreated( monitor, d, timeUtc );
        }

        /// <summary>
        /// Default behavior is to create a snapshot (simply calls <see cref="CreateSnapshot"/> protected method).
        /// </summary>
        /// <param name="c">The transaction context.</param>
        public virtual void OnTransactionCommit( in SuccessfulTransactionContext c )
        {
            _next?.OnTransactionCommit( c );
            CreateSnapshot( c.Monitor, c.Domain, c.CommitTimeUtc );
        }

        /// <summary>
        /// Default behavior is to call <see cref="RestoreSnapshot"/> and throws an <see cref="Exception"/>
        /// if no snapshot was available or if an error occured.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="d">The associated domain.</param>
        /// <param name="errors">A necessarily non null list of errors with at least one error.</param>
        public virtual void OnTransactionFailure( IActivityMonitor monitor, ObservableDomain d, IReadOnlyList<CKExceptionData> errors )
        {
            _next?.OnTransactionFailure( monitor, d, errors );
            if( !RestoreSnapshot( monitor, d ) )
            {
                throw new Exception( "No snapshot available or error while restoring the last snapshot." );
            }
        }

        /// <summary>
        /// Simply calls the next <see cref="IObservableDomainClient"/> in the chain of responsibility
        /// and if there is no snapshot an initial snapshot is created.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="d">The associated domain.</param>
        /// <param name="timeUtc">The Utc time associated to the transaction.</param>
        public virtual void OnTransactionStart( IActivityMonitor monitor, ObservableDomain d, DateTime timeUtc )
        {
            _next?.OnTransactionStart( monitor, d, timeUtc );
            if( !HasSnapshot ) CreateSnapshot( monitor, d, timeUtc );
        }

        /// <summary>
        /// Loads the domain and initializes the current snapshot.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="d">The domain.</param>
        /// <param name="timeUtc">A utc time (typically the creation time).</param>
        /// <param name="s">The readable stream (will be copied into the memory).</param>
        protected void LoadAndInitializeSnapshot( IActivityMonitor monitor, ObservableDomain d, DateTime timeUtc, Stream s )
        {
            _memory.Position = 0;
            s.CopyTo( _memory );
            DoLoadMemory( monitor, d );
            if( _snapshotSerialNumber == -1 )
            {
                _snapshotSerialNumber = Int32.MaxValue;
                _snapshotTimeUtc = timeUtc;
            }
        }

        /// <summary>
        /// Writes the current snapshot to the provided stream.
        /// </summary>
        /// <param name="s">The target stream.</param>
        protected void WriteSnapshotTo( Stream s )
        {
            s.Write( _memory.GetBuffer(), 0, (int)_memory.Position );
        }


        /// <summary>
        /// Restores the last snapshot. Returns false if there is no snapshot or if an error occurred.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="d">The associated domain.</param>
        /// <returns>False if no snapshot is available or if the restoration failed. True otherwise.</returns>
        protected bool RestoreSnapshot( IActivityMonitor monitor, ObservableDomain d )
        {
            if( !HasSnapshot )
            {
                Debug.Assert( ToString() == "No snapshot." );
                monitor.Warn( "No snapshot." );
                return false;
            }
            using( monitor.OpenInfo( $"Restoring snapshot: {ToString()}" ) )
            {
                try
                {
                    DoLoadMemory( monitor, d );
                    monitor.CloseGroup( "Success." );
                    return true;
                }
                catch( Exception ex )
                {
                    monitor.Error( ex );
                    return false;
                }
            }
        }

        void DoLoadMemory( IActivityMonitor monitor, ObservableDomain d )
        {
            long p = _memory.Position;
            _memory.Position = 0;
            d.Load( monitor, _memory, leaveOpen: true );
            if( _memory.Position != p ) throw new Exception( $"Internal error: stream position should be {p} but was {_memory.Position} after reload." );
        }

        /// <summary>
        /// Creates a snapshot.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="d">The associated domain.</param>
        /// <param name="timeUtc">Time of the operation.</param>
        protected void CreateSnapshot( IActivityMonitor monitor, ObservableDomain d, DateTime timeUtc )
        {
            using( monitor.OpenTrace( $"Creating snapshot." ) )
            {
                _memory.Position = 0;
                d.Save( monitor, _memory, true );
                _snapshotSerialNumber = d.TransactionSerialNumber;
                _snapshotTimeUtc = timeUtc;
                monitor.CloseGroup( ToString() );
            }
        }

        /// <summary>
        /// Returns the number of bytes, the transaction number and the time or "No snapshot.".
        /// </summary>
        /// <returns>A readable string.</returns>
        public override string ToString() => HasSnapshot ? $"Snapshot {_memory.Position} bytes, n°{_snapshotSerialNumber} - {_snapshotTimeUtc}." : "No snapshot.";

    }
}
