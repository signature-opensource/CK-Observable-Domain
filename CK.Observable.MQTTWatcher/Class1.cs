using CK.Core;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable.MQTTWatcher
{
    public sealed class PipeReaderLine
    {
        readonly PipeReader _reader;
        readonly Encoding _encoding;
        readonly byte[] _newLine;
        readonly Queue<string?> _lines;
        bool _end;
        public PipeReaderLine( PipeReader reader, Encoding encoding, string newLine = "\r\n" )
        {
            Throw.CheckNotNullArgument( reader );
            Throw.CheckNotNullArgument( encoding );
            Throw.CheckNotNullOrEmptyArgument( newLine );
            _reader = reader;
            _encoding = encoding;
            _newLine = encoding.GetBytes( newLine );
            _lines = new Queue<string?>();
        }

        /// <summary>
        /// Waits for a line of text. Returns null when the pipe reader is no more available.
        /// </summary>
        /// <returns>The string or null is the pipe reader is no more available.</returns>
        public async ValueTask<string?> ReadLineAsync()
        {
            if( _lines.Count > 0 ) return _lines.Dequeue();
            if( _end ) return null;
            while( _lines.Count == 0 )
            {
                ReadResult result = await _reader.ReadAsync();
                ReadOnlySequence<byte> buffer = result.Buffer;
                // The try...finally here only secures the reader.AdvanceTo.
                try
                {
                    while( TryReadLine( ref buffer, _encoding, _newLine, out var oneLine ) )
                    {
                        _lines.Enqueue( oneLine );
                    }
                    if( result.IsCanceled || result.IsCompleted )
                    {
                        _lines.Enqueue( null );
                        _end = true;
                    }
                }
                finally
                {
                    _reader.AdvanceTo( buffer.Start, buffer.End );
                }
            }
            return _lines.Dequeue();
        }
        public static bool TryReadLine( ref ReadOnlySequence<byte> buffer,
                                        Encoding encoding,
                                        ReadOnlySpan<byte> newLine,
                                        [NotNullWhen( true )] out string? line )
        {
            Throw.CheckNotNullArgument( encoding );
            if( buffer.IsSingleSegment )
            {
                var span = buffer.FirstSpan;
                if( span.Length > 0 )
                {
                    var idx = span.IndexOf( newLine );
                    if( idx >= 0 )
                    {
                        if( idx == 0 )
                        {
                            line = String.Empty;
                            buffer = buffer.Slice( newLine.Length );
                            return true;
                        }
                        line = encoding.GetString( span.Slice( 0, idx ) );
                        buffer = buffer.Slice( idx + newLine.Length );
                        return true;
                    }
                }
            }
            else
            {
                var sR = new SequenceReader<byte>( buffer );
                ReadOnlySequence<byte> rLine;
                if( sR.TryReadTo( out rLine, newLine ) )
                {
                    line = encoding.GetString( rLine );
                    buffer = buffer.Slice( sR.Position );
                    return true;
                }
            }
            line = null;
            return false;
        }
    }
}
