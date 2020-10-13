using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CK.Observable
{
    /// <summary>
    /// Helpers that can be enlisted to the <see cref="ObservableDomain.OnSuccessfulTransaction"/> event that transforms
    /// <see cref="SuccessfulTransactionEventArgs.Events"/> into <see cref="TransactionEvent"/> that captures, for each transaction,
    /// all the transaction events as JSON string that describes them.
    /// </summary>
    public class JsonEventCollector
    {
        readonly List<TransactionEvent> _events;
        readonly StringWriter _buffer;
        readonly ObjectExporter _exporter;
        ObservableDomain _domain;
        int _lastTranNum;
        TimeSpan _keepDuration;
        int _keepLimit;

        /// <summary>
        /// Representation of a successful transaction.
        /// </summary>
        public class TransactionEvent
        {
            /// <summary>
            /// The transaction number.
            /// The very first transaction is number 1.
            /// It's <see cref="ExportedEvents"/> is empty since a full export will be more efficient.
            /// This initial transaction will be raised by <see cref="LastEventChanged"/> but will never be returned
            /// by <see cref="GetTransactionEvents(int)"/>: this avoids a race condition when a domain has been exported
            /// (empty) before the very first transaction.
            /// </summary>
            public readonly int TransactionNumber;

            /// <summary>
            /// The date and time of the transaction.
            /// </summary>
            public readonly DateTime TimeUtc;

            /// <summary>
            /// The JSON description of the <see cref="SuccessfulTransactionEventArgs.Events"/>.
            /// </summary>
            public readonly string ExportedEvents;

            internal TransactionEvent( int t, DateTime timeUtc, string exported )
            {
                TransactionNumber = t;
                TimeUtc = timeUtc;
                ExportedEvents = exported;
            }
        }

        /// <summary>
        /// Initializes a new <see cref="JsonEventCollector"/>, optionally calling <see cref="CollectEvent(ObservableDomain, bool)"/>
        /// immediately.
        /// </summary>
        /// <param name="domain">Optional domain for which events must be collected.</param>
        public JsonEventCollector( ObservableDomain? domain = null )
        {
            _events = new List<TransactionEvent>();
            _buffer = new StringWriter();
            _exporter = new ObjectExporter( new JSONExportTarget( _buffer ) );
            KeepDuration = TimeSpan.FromMinutes( 5 );
            KeepLimit = 10;
            if( domain != null ) CollectEvent( domain, false );
        }

        /// <summary>
        /// Gets or sets the maximum time during which events are kept, regardless of <see cref="KeepLimit"/>.
        /// Defaults to 5 minutes. Must be <see cref="TimeSpan.Zero"/> or positive.
        /// </summary>
        public TimeSpan KeepDuration
        {
            get => _keepDuration;
            set
            {
                if( value < TimeSpan.Zero ) throw new ArgumentOutOfRangeException();
                _keepDuration = value;
            }
        }

        /// <summary>
        /// Gets or sets the minimum number of transaction events that are kept, regardless of <see cref="KeepDuration"/>.
        /// Defaults to 10, the minimum is 1.
        /// </summary>
        public int KeepLimit
        {
            get => _keepLimit;
            set
            {
                if( value < 1 ) throw new ArgumentOutOfRangeException();
                _keepLimit = value;
            }
        }

        /// <summary>
        /// Gets the transaction events if possible from a given transaction number.
        /// This returns null if an export is required (the <paramref name="transactionNumber"/> is too old),
        /// and an empty array if the transactionNumber is greater or equal to the current transaction number
        /// stored (this should not happen: clients should only have smaller transaction number).
        /// </summary>
        /// <param name="transactionNumber">The starting transaction number.</param>
        /// <returns>The set of transaction events to apply or null if an export is required.</returns>
        public IReadOnlyList<TransactionEvent>? GetTransactionEvents( int transactionNumber )
        {
            lock( _events )
            {
                if( transactionNumber >= _lastTranNum )
                {
                    return Array.Empty<TransactionEvent>();
                }
                int minTranNum = _lastTranNum - _events.Count;
                int idxStart = transactionNumber - minTranNum;
                if( idxStart < 0 )
                {
                    return null;
                }
                var a = new TransactionEvent[_events.Count - idxStart];
                _events.CopyTo( idxStart, a, 0, a.Length );
                return a;
            }
        }

        /// <summary>
        /// Called whenever a new transaction event is available.
        /// Note that the first transaction is visible: see <see cref="TransactionEvent.TransactionNumber"/>.
        /// </summary>
        public event Action<IActivityMonitor, TransactionEvent> LastEventChanged;

        /// <summary>
        /// Gets the last transaction event that has been seen (the first one can appear
        /// here - see <see cref="TransactionEvent.TransactionNumber"/>).
        /// </summary>
        public TransactionEvent? LastEvent { get; private set; }

        /// <summary>
        /// Associates this collector to a domain. There must not be any existing associated domain
        /// otherwise an <see cref="InvalidOperationException"/> is thrown.
        /// </summary>
        /// <param name="domain">The domain from which transaction events must be collected.</param>
        /// <param name="clearEvents">True to clear any existing transactions events.</param>
        public void CollectEvent( ObservableDomain domain, bool clearEvents )
        {
            if( domain == null ) throw new ArgumentNullException( nameof( domain ) );
            lock( _events )
            {
                if( _domain != null ) throw new InvalidOperationException( "Event collector is already associated to a domain." );
                _domain = domain;
                domain.OnSuccessfulTransaction += OnSuccessfulTransaction;
                if( clearEvents )
                {
                    _events.Clear();
                    _lastTranNum = 0;
                }
            }
        }

        /// <summary>
        /// Dissociates this collector from the current domain.
        /// </summary>
        public void Detach()
        {
            lock( _events )
            {
                _domain.OnSuccessfulTransaction -= OnSuccessfulTransaction;
                _domain = null;
            }
        }

        void OnSuccessfulTransaction( object sender, SuccessfulTransactionEventArgs c ) 
        {
            Debug.Assert( sender == _domain );
            // It's useless to capture the initial transaction: the full export will be more efficient.
            int num = c.Domain.TransactionSerialNumber;
            if( num == 1 )
            {
                LastEvent = new TransactionEvent( 1, c.CommitTimeUtc, String.Empty );
            }
            else
            {
                lock( _events )
                {
                    if( _lastTranNum != 0 && _lastTranNum != num - 1 )
                    {
                        c.Monitor.Warn( $"Missed transaction. Current is n°{num}, last stored was {_lastTranNum}. Clearing transaction events cache." );
                        _events.Clear();
                    }
                    _lastTranNum = num;
                    _buffer.GetStringBuilder().Clear();
                    _exporter.Reset();
                    foreach( var e in c.Events ) e.Export( _exporter );
                    _events.Add( LastEvent = new TransactionEvent( num, c.CommitTimeUtc, _buffer.ToString() ) );
                    ApplyKeepDuration();
                }
            }
            LastEventChanged?.Invoke( c.Monitor, LastEvent );
        }

        void ApplyKeepDuration()
        {
            int removableMaxIndex = _events.Count - KeepLimit;
            if( removableMaxIndex > 0 )
            {
                var timeLimit = DateTime.UtcNow.Subtract( KeepDuration );
                int i = 0;
                for( ; i < removableMaxIndex; ++i )
                {
                    if( _events[i].TimeUtc >= timeLimit ) break;
                }
                if( i > 0 ) _events.RemoveRange( 0, i );
            }
        }

    }
}
