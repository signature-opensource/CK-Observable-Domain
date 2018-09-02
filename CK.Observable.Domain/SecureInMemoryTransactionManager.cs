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
    public class SecureInMemoryTransactionManager : IObservableTransactionManager
    {
        readonly List<Snapshot> _snapshots;

        public class Snapshot
        {
            internal Snapshot( int serialNumber, MemoryStream c, IReadOnlyList<IObservableEvent> e )
            {
                Debug.Assert( c != null && c.Position == 0 && e != null );
                SerialNumber = serialNumber;
                Capture = c;
                Events = e;
                TimeUtc = DateTime.UtcNow;
            }

            public int SerialNumber { get; }

            public DateTime TimeUtc { get; }

            public MemoryStream Capture { get; }

            public IReadOnlyList<IObservableEvent> Events { get; }

            public override string ToString()
            {
                return $"Snapshot {SerialNumber} ({TimeUtc}) - {Events.Count} events.";
            }
        }

        public SecureInMemoryTransactionManager()
        {
            _snapshots = new List<Snapshot>();
        }

        /// <summary>
        /// Gets the snapshots.
        /// </summary>
        public IReadOnlyList<Snapshot> Snampshots => _snapshots;

        /// <summary>
        /// Default behavior is to create a snapshot (simply calls <see cref="CreateSnapshot"/> protected method).
        /// </summary>
        /// <param name="d">The associated domain.</param>
        /// <param name="events">The events.</param>
        public virtual void OnTransactionCommit( ObservableDomain d, IReadOnlyList<IObservableEvent> events )
        {
            CreateSnapshot( d, events );
        }

        /// <summary>
        /// Default behavior is to call <see cref="RestoreLastSnapshot"/> and throws an <see cref="Exception"/>
        /// if no snapshot was available.
        /// </summary>
        /// <param name="d">The associated domain.</param>
        public virtual void OnTransactionFailure( ObservableDomain d )
        {
            if( !RestoreLastSnapshot( d ) )
            {
                throw new Exception( "No snapshot available." );
            }
        }

        /// <summary>
        /// Does nothing.
        /// </summary>
        /// <param name="d">The associated domain.</param>
        public virtual void OnTransactionStart( ObservableDomain d )
        {
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
        /// Creates and add a snapshot to <see cref="Snapshots"/>.
        /// </summary>
        /// <param name="d">The associated domain.</param>
        /// <param name="events">The events.</param>
        protected void CreateSnapshot( ObservableDomain d, IReadOnlyList<IObservableEvent> events )
        {
            using( d.Monitor.OpenTrace( $"Creating snapshot." ) )
            {
                var capture = new MemoryStream();
                d.Save( capture, true );
                capture.Position = 0;
                var snapshot = new Snapshot( d.TransactionSerialNumber, capture, events );
                _snapshots.Add( snapshot );
                d.Monitor.CloseGroup( snapshot.ToString() );
            }
        }
    }
}
