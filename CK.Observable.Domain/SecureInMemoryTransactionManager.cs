using CK.Core;
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
    /// This is more a demo than an actual useful participant since it keeps everything
    /// in memory. However it is open to extensions and can be used as a base class.
    /// It implements the chain of responsibility design pattern.
    /// </summary>
    public class SecureInMemoryTransactionManager : IObservableTransactionManager
    {
        readonly IObservableTransactionManager _next;
        readonly List<Snapshot> _snapshots;

        /// <summary>
        /// Captures the whole domain by serializing it into a <see cref="MemoryStream"/>
        /// after a successful transaction.
        /// </summary>
        public struct Snapshot
        {
            internal Snapshot( int serialNumber, MemoryStream c, DateTime timeUtc )
            {
                Debug.Assert( c != null && c.Position == 0 );
                SerialNumber = serialNumber;
                TimeUtc = timeUtc;
                Capture = c;
            }

            /// <summary>
            /// Gets the transaction serial number.
            /// </summary>
            public int SerialNumber { get; }

            /// <summary>
            /// Gets the date and time of the transaction.
            /// </summary>
            public DateTime TimeUtc { get; }

            /// <summary>
            /// Gets the serialized domain.
            /// </summary>
            public MemoryStream Capture { get; }

            /// <summary>
            /// Overridden to return the <see cref="SerialNumber"/> and <see cref="TimeUtc"/>.
            /// </summary>
            /// <returns>A readable string.</returns>
            public override string ToString()
            {
                return $"Snapshot {SerialNumber} - {TimeUtc}.";
            }
        }

        /// <summary>
        /// Initializes a new <see cref="SecureInMemoryTransactionManager"/>.
        /// </summary>
        /// <param name="next">The next manager (can be null).</param>
        public SecureInMemoryTransactionManager( IObservableTransactionManager next = null )
        {
            _next = next;
            _snapshots = new List<Snapshot>();
        }

        /// <summary>
        /// Gets the snapshots.
        /// </summary>
        public IReadOnlyList<Snapshot> Snapshots => _snapshots;

        /// <summary>
        /// Gets the mutable list of snapshots.
        /// </summary>
        protected List<Snapshot> ActualSnapshots;

        /// <summary>
        /// Default behavior is to create a snapshot (simply calls <see cref="CreateSnapshot"/> protected method).
        /// </summary>
        /// <param name="d">The associated domain.</param>
        /// <param name="timeUtc">The date time utc of the commit.</param>
        /// <param name="events">The events.</param>
        public virtual void OnTransactionCommit( ObservableDomain d, DateTime timeUtc, IReadOnlyList<ObservableEvent> events )
        {
            _next?.OnTransactionCommit( d, timeUtc, events );
            CreateSnapshot( d, timeUtc );
        }

        /// <summary>
        /// Default behavior is to call <see cref="RestoreLastSnapshot"/> and throws an <see cref="Exception"/>
        /// if no snapshot was available.
        /// </summary>
        /// <param name="d">The associated domain.</param>
        /// <param name="errors">A necessarily non null list of errors with at least one error.</param>
        public virtual void OnTransactionFailure( ObservableDomain d, IReadOnlyList<CKExceptionData> errors )
        {
            _next?.OnTransactionFailure( d, errors );
            if( !RestoreLastSnapshot( d ) )
            {
                throw new Exception( "No snapshot available." );
            }
        }

        /// <summary>
        /// Does nothing except calling the next <see cref="IObservableTransactionManager"/> in the chain of responsibility
        /// except for the very first transaction where a snapshot is created.
        /// </summary>
        /// <param name="d">The associated domain.</param>
        public virtual void OnTransactionStart( ObservableDomain d, DateTime timeUtc )
        {
            _next?.OnTransactionStart( d, timeUtc );
            if( d.TransactionSerialNumber == 0 ) CreateSnapshot( d, timeUtc );
        }

        /// <summary>
        /// Restores the last snapshot. Returns false if there is no snapshot or if an error occurred.
        /// </summary>
        /// <param name="d">The associated domain.</param>
        /// <returns>False if no snapshot is available or if the restoration failed. True otherwise.</returns>
        protected bool RestoreLastSnapshot( ObservableDomain d )
        {
            using( d.Monitor.OpenInfo( $"Restoring last snapshot." ) )
            {
                try
                {
                    int idx = _snapshots.Count - 1;
                    if( idx >= 0 )
                    {
                        d.Monitor.Info( _snapshots[idx].ToString() );
                        var s = _snapshots[idx].Capture;
                        d.Load( s, true );
                        s.Position = 0;
                        _snapshots.RemoveAt( idx );
                        d.Monitor.CloseGroup( "Success." );
                        return true;
                    }
                    d.Monitor.CloseGroup( "No Snapshot to restore." );
                    return false;
                }
                catch( Exception ex )
                {
                    d.Monitor.Error( ex );
                    return false;
                }
            }
        }

        /// <summary>
        /// Creates and adds a snapshot to <see cref="Snapshots"/>.
        /// </summary>
        /// <param name="d">The associated domain.</param>
        /// <param name="timeUtc">Time of the operation.</param>
        protected void CreateSnapshot( ObservableDomain d, DateTime timeUtc )
        {
            using( d.Monitor.OpenTrace( $"Creating snapshot." ) )
            {
                var capture = new MemoryStream();
                d.Save( capture, true );
                capture.Position = 0;
                var snapshot = new Snapshot( d.TransactionSerialNumber, capture, timeUtc );
                _snapshots.Add( snapshot );
                d.Monitor.CloseGroup( snapshot.ToString() );
            }
        }
    }
}
