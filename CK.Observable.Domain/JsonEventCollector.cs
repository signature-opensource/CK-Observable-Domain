using CK.Core;
using CK.PerfectEvent;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CK.Observable
{
    /// <summary>
    /// Helpers that can be enlisted to the <see cref="ObservableDomain.TransactionDone"/> event that transforms
    /// <see cref="TransactionDoneEventArgs.Events"/> into <see cref="TransactionEvent"/> that captures, for each transaction,
    /// all the transaction events as JSON string that describes them.
    /// </summary>
    public sealed class JsonEventCollector
    {
        readonly List<TransactionEvent> _events;
        readonly StringWriter _buffer;
        readonly ObjectExporter _exporter;
        readonly Channel<TransactionEvent?> _channel;
        readonly PerfectEventSender<TransactionEvent> _lastEventChanged;

        ObservableDomain? _domain;
        int _lastTranNum;
        TimeSpan _keepDuration;
        int _keepLimit;

        /// <summary>
        /// Immutable representation of a successful transaction.
        /// </summary>
        public sealed class TransactionEvent
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
            /// The JSON description of the <see cref="TransactionDoneEventArgs.Events"/>.
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
            _channel = Channel.CreateUnbounded<TransactionEvent?>( new UnboundedChannelOptions { SingleReader = true } );
            _lastEventChanged = new PerfectEventSender<TransactionEvent>();

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
                Throw.CheckOutOfRangeArgument( value >= TimeSpan.Zero );
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
                Throw.CheckOutOfRangeArgument( value >= 1 );
                _keepLimit = value;
            }
        }

        /// <summary>
        /// Gets the transaction events if possible from a given transaction number.
        /// This returns null if an export is required (the <paramref name="transactionNumber"/> is too old),
        /// and an empty array if the transactionNumber is greater or equal to the current transaction number
        /// stored (this should not happen: clients should only have smaller transaction numbers).
        /// </summary>
        /// <param name="transactionNumber">The starting transaction number.</param>
        /// <returns>The current transaction number and the set of transaction events to apply or null if an export is required.</returns>
        public (int TransactionNumber, IReadOnlyList<TransactionEvent>? Events) GetTransactionEvents( int transactionNumber )
        {
            lock( _events )
            {
                if( transactionNumber <= 0 )
                {
                    return (_lastTranNum, null);
                }
                if( transactionNumber >= _lastTranNum )
                {
                    return (_lastTranNum, Array.Empty<TransactionEvent>());
                }
                int minTranNum = _lastTranNum - _events.Count;
                int idxStart = transactionNumber - minTranNum;
                if( idxStart < 0 )
                {
                    return (_lastTranNum, null);
                }
                var a = new TransactionEvent[_events.Count - idxStart];
                _events.CopyTo( idxStart, a, 0, a.Length );
                return (_lastTranNum,a);
            }
        }

        /// <summary>
        /// Called whenever a new transaction event is available.
        /// Note that the first transaction is visible: see <see cref="TransactionEvent.TransactionNumber"/>.
        /// </summary>
        public PerfectEvent<TransactionEvent> LastEventChanged => _lastEventChanged.PerfectEvent;

        /// <summary>
        /// Gets the last transaction event that has been seen (the first one can appear
        /// here - see <see cref="TransactionEvent.TransactionNumber"/>).
        /// </summary>
        public TransactionEvent? LastEvent { get; private set; }

        /// <summary>
        /// Associates this collector to a domain. There must not be any existing associated domain
        /// otherwise an <see cref="InvalidOperationException"/> is thrown.
        /// Use <see cref="Detach()"/> to stop collecting events from a domain.
        /// </summary>
        /// <param name="domain">The domain from which transaction events must be collected.</param>
        /// <param name="clearEvents">True to clear any existing transactions events.</param>
        public void CollectEvent( ObservableDomain domain, bool clearEvents )
        {
            Throw.CheckNotNullArgument( domain );
            lock( _events )
            {
                Throw.CheckState( "Event collector is already associated to a domain.", _domain == null );
                _domain = domain;
                // We don't need to wait for the end of the loop.
                // The null terminator is sent, the loop ends.
                // The channel is never completed (it's reused across Detach/CollectEvent).
                _ = Task.Run( RunLoopAsync );
                domain.TransactionDone += OnSuccessfulTransaction;
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
            if( _domain != null )
            {
                lock( _events )
                {
                    if( _domain != null )
                    {
                        // Sends the null terminator from the lock: no risk to push a transaction
                        // event after it.
                        _channel.Writer.TryWrite( null );
                        _domain.TransactionDone -= OnSuccessfulTransaction;
                        _domain = null!;
                    }
                }
            }
        }

        void OnSuccessfulTransaction( object? sender, TransactionDoneEventArgs c )
        {
            Debug.Assert( sender == _domain );
            lock( _events )
            {
                // It's useless to capture the initial transaction: the full export will be more efficient.
                int num = c.Domain.TransactionSerialNumber;
                if( num == 1 )
                {
                    LastEvent = new TransactionEvent( 1, c.CommitTimeUtc, String.Empty );
                }
                else
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
            _channel.Writer.TryWrite( LastEvent );
        }

        async Task RunLoopAsync()
        {
            Debug.Assert( _domain != null );
            var monitor = new ActivityMonitor( $"JsonEvent collector for '{_domain.DomainName}'." );
            bool mustExit = false;
            while( !mustExit )
            {
                var ev = await _channel.Reader.ReadAsync();
                if( ev == null ) break;
                await _lastEventChanged.SafeRaiseAsync( monitor, ev );
            }
            monitor.MonitorEnd();
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
