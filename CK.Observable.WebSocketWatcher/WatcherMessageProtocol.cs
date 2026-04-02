using System;
using System.Buffers;
using System.Text;
using SimpleR.Protocol;

namespace CK.Observable.WebSocketWatcher;

/// <summary>
/// SimpleR protocol that writes outgoing WebSocket messages as raw bytes.
/// Incoming messages are represented as UTF-8 strings but are not used today.
/// </summary>
public sealed class WatcherMessageProtocol : IDelimitedMessageProtocol<string, ReadOnlyMemory<byte>>
{
    /// <summary>
    /// Parses a message from the input.
    /// Unused today.
    /// </summary>
    /// <param name="input">The input sequence to parse the message from.</param>
    /// <returns>The parsed message.</returns>
    public string ParseMessage( ref ReadOnlySequence<byte> input )
    {
        return Encoding.UTF8.GetString( input.ToArray() );
    }

    /// <summary>
    /// Writes a message to the output.
    /// </summary>
    /// <param name="message">The message to write.</param>
    /// <param name="output">The output buffer writer.</param>
    public void WriteMessage( ReadOnlyMemory<byte> message, IBufferWriter<byte> output )
    {
        output.Write( message.Span );
    }
}
