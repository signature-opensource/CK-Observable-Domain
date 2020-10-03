using CK.Core;
using System;
using System.Collections.Generic;
using System.IO;
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
        TimeSpan _keepDuration;
        int _keepLimit;

        /// <summary>
        /// Representation of a successful transaction.
        /// </summary>
        public readonly struct TransactionEvent
        {
            /// <summary>
            /// The transaction number.
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
        /// Initializes a new <see cref="JsonEventCollector"/>.
        /// </summary>
        /// <param name="next">The next manager (can be null).</param>
        public JsonEventCollector()
        {
            _events = new List<TransactionEvent>();
            _buffer = new StringWriter();
            _exporter = new ObjectExporter( new JSONExportTarget( _buffer ) );
            KeepDuration = TimeSpan.FromMinutes( 5 );
            KeepLimit = 2;
        }

        /// <summary>
        /// Gets the current transaction events.
        /// </summary>
        public IReadOnlyList<TransactionEvent> TransactionEvents => _events;

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
        /// Defaults to 2, the minimum is 1.
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
        /// Generates a JSON object that contains all the events from a specified transaction number.
        /// This may return the <c>{"N":-1,"E":null}</c> string if a full export of the domain is required.
        /// This may throw an <see cref="InvalidOperationException"/> if the <paramref name="transactionNumber"/>
        /// is greater than or equal to the currently known one.
        /// </summary>
        /// <param name="transactionNumber">The starting transaction number.</param>
        /// <returns>The JSON object.</returns>
        public string WriteJSONEventsFrom( int transactionNumber )
        {
            if( _events.Count == 0 || transactionNumber < _events[0].TransactionNumber - 1 )
            {
                // A full export is required.
                return "{\"N\":-1,\"E\":null}";
            }
            var last = _events[_events.Count - 1];
            if( transactionNumber >= last.TransactionNumber )
            {
                throw new InvalidOperationException( $"Transaction requested n°{transactionNumber}. Current is {last.TransactionNumber}." );
            }
            _buffer.GetStringBuilder().Clear();
            _exporter.Reset();
            var t = _exporter.Target;
            t.EmitStartObject( -1, ObjectExportedKind.Object );
            t.EmitPropertyName( "N" );
            t.EmitInt32( last.TransactionNumber );
            t.EmitPropertyName( "E" );
            t.EmitStartList();
            if( transactionNumber == last.TransactionNumber - 1 )
            {
                _buffer.Write( last.ExportedEvents );
            }
            else
            {
                bool atLeastOne = false;
                foreach( var e in _events )
                {
                    if( e.TransactionNumber <= transactionNumber ) continue;
                    if( atLeastOne ) _buffer.Write( "," );
                    else atLeastOne = true;
                    _buffer.Write( e.ExportedEvents );
                }
            }
            t.EmitEndList();
            t.EmitEndObject( -1, ObjectExportedKind.Object );
            return _buffer.GetStringBuilder().ToString();
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

        /// <summary>
        /// Event pattern adapter that calls <see cref="OnSuccessfulTransaction(in SuccessfulTransactionEventArgs)"/>.
        /// </summary>
        /// <param name="sender">The (ignored) sender domain.</param>
        /// <param name="c">The transaction event.</param>
        public void OnSuccessfulTransaction( object sender, SuccessfulTransactionEventArgs c ) => OnSuccessfulTransaction( c );

        /// <summary>
        /// Generates a new <see cref="TransactionEvent"/> based on <see cref="SuccessfulTransactionEventArgs.Events"/>.
        /// </summary>
        /// <param name="c">The transaction event.</param>
        public void OnSuccessfulTransaction( in SuccessfulTransactionEventArgs c )
        {
            _buffer.GetStringBuilder().Clear();
            _exporter.Reset();
            foreach( var e in c.Events ) e.Export( _exporter );
            _events.Add( new TransactionEvent( c.Domain.TransactionSerialNumber, c.CommitTimeUtc, _buffer.ToString() ) );
            ApplyKeepDuration();
        }

    }
}
