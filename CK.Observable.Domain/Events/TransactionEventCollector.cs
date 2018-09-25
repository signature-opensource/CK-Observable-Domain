using CK.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    public class TransactionEventCollector : IObservableTransactionManager
    {
        readonly IObservableTransactionManager _next;
        readonly List<Event> _events;
        readonly StringWriter _buffer;
        readonly ObjectExporter _exporter;

        public struct Event
        {
            public readonly int TransactionNumber;
            public readonly IReadOnlyList<ObservableEvent> Events;
            public readonly DateTime TimeUtc;
            public readonly string ExportedEvents;

            internal Event( int t, DateTime timeUtc, IReadOnlyList<ObservableEvent> e, string exported )
            {
                TransactionNumber = t;
                Events = e;
                TimeUtc = timeUtc;
                ExportedEvents = exported;
            }
        }

        public TransactionEventCollector( IObservableTransactionManager next = null )
        {
            _next = next;
            _events = new List<Event>();
            _buffer = new StringWriter();
            _exporter = new ObjectExporter( new JSONExportTarget( _buffer ) );
            KeepDuration = TimeSpan.FromHours( 1 );
            KeepLimit = 100;
        }

        /// <summary>
        /// Gets the current transaction events.
        /// </summary>
        public IReadOnlyList<Event> TransactionEvents => _events;

        /// <summary>
        /// Gets or sets the maximum time during which events are kept.
        /// Defaults to one hour.
        /// </summary>
        public TimeSpan KeepDuration { get; set; }

        /// <summary>
        /// Gets or sets the minimum number of transaction events that are kept, regardless of <see cref="KeepDuration"/>.
        /// Default to 100.
        /// </summary>
        public int KeepLimit { get; set; }

        public string WriteEventsFrom( int transactionNumber )
        {
            if( _events.Count == 0 ) throw new InvalidOperationException( "OnTransactionCommit has not been called yet." );
            var last = _events[_events.Count - 1];
            if( transactionNumber >= last.TransactionNumber )
            {
                throw new InvalidOperationException( $"Transaction requested n°{transactionNumber}. Current is {last.TransactionNumber}." );
            }
            _buffer.GetStringBuilder().Clear();
            _exporter.Reset();
            _exporter.Target.EmitStartObject( -1, ObjectExportedKind.Object );
            _exporter.Target.EmitPropertyName( "N" );
            _exporter.Target.EmitInt32( last.TransactionNumber );
            _exporter.Target.EmitPropertyName( "E" );
            _buffer.Write( "[" );
            if( last.TransactionNumber == transactionNumber )
            {
                _buffer.Write( last.ExportedEvents );
            }
            else
            {
                foreach( var e in _events )
                {
                    if( e.TransactionNumber <= transactionNumber ) continue;
                    foreach( var ev in e.Events ) ev.Export( _exporter );
                }
            }
            _buffer.Write( "]" );
            _exporter.Target.EmitEndObject( -1, ObjectExportedKind.Object );
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

        void IObservableTransactionManager.OnTransactionCommit( ObservableDomain d, DateTime timeUtc, IReadOnlyList<ObservableEvent> events )
        {
            _buffer.GetStringBuilder().Clear();
            _exporter.Reset();
            foreach( var e in events ) e.Export( _exporter );
            _events.Add( new Event( d.TransactionSerialNumber, timeUtc, events, _buffer.ToString() ) );
            ApplyKeepDuration();
            _next?.OnTransactionCommit( d, timeUtc, events );
        }

        void IObservableTransactionManager.OnTransactionFailure( ObservableDomain d, IReadOnlyList<CKExceptionData> errors )
        {
            _next?.OnTransactionFailure( d, errors );
        }

        void IObservableTransactionManager.OnTransactionStart( ObservableDomain d, DateTime timeUtc )
        {
            _next?.OnTransactionStart( d, timeUtc );
        }
    }
}
