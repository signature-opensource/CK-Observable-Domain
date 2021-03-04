using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace CK.Observable
{
    public partial class BinaryDeserializer
    {
        readonly Stack<string> _debugContext = new Stack<string>();
        int _debugModeCounter;
        int _debugSentinel;
        string? _lastWriteSentinel;
        string? _lastReadSentinel;

        public const string ExceptionPrefixContext = "[WithContext]";

        /// <summary>
        /// Gets whether this deserializer is currently in debug mode.
        /// </summary>
        public bool IsDebugMode => _debugModeCounter > 0;

        /// <summary>
        /// Updates the current debug mode that must have been written by <see cref="BinarySerializer.DebugWriteMode(bool?)"/>.
        /// </summary>
        /// <returns>Whether the debug mode is currently active or not.</returns>
        public bool DebugReadMode()
        {
            switch( ReadByte() )
            {
                case 182: ++_debugModeCounter; break;
                case 181: --_debugModeCounter; break;
                case 180: break;
                default: ThrowInvalidDataException( $"Expected DebugMode byte marker." ); break;
            }
            return IsDebugMode;
        }

        /// <summary>
        /// Checks the existence of a sentinel written by <see cref="BinarySerializer.DebugWriteSentinel"/>.
        /// An <see cref="InvalidDataException"/> is thrown if <see cref="IsDebugMode"/> is true and the sentinel cannot be read.
        /// </summary>
        /// <param name="fileName">Current file name used to build the <see cref="InvalidDataException"/> message if sentinel cannot be read back.</param>
        /// <param name="line">Current line number used to build the <see cref="InvalidDataException"/> message if sentinel cannot be read back.</param>
        public void DebugCheckSentinel( [CallerFilePath] string? fileName = null, [CallerLineNumber] int line = 0 )
        {
            if( !IsDebugMode ) return;
            bool success = false;
            Exception? e = null;
            try
            {
                success = ReadInt32() == 987654321
                          && ReadInt32() == _debugSentinel
                          && (_lastWriteSentinel = ReadString()) != null;
            }
            catch( Exception ex )
            {
                e = ex;
            }
            if( !success )
            {
                var msg = $"Sentinel check failure: expected reading sentinel n°{_debugSentinel} at {fileName}({line}). Last successful sentinel was written at {_lastWriteSentinel} and read at {_lastReadSentinel}.";
                ThrowInvalidDataException( msg, e );
            }
            ++_debugSentinel;
            _lastReadSentinel = fileName + '(' + line.ToString() + ')';
        }

        void OpenDebugPushContext( string ctx )
        {
            Debug.Assert( IsDebugMode );
            _debugContext.Push( ctx );
        }

        void CloseDebugPushContext( string ctx )
        {
            _debugContext.Pop();
        }

        void ThrowInvalidDataException( string message, Exception? inner = null )
        {
            StringBuilder b = new StringBuilder();
            b.Append( ExceptionPrefixContext ).Append( message )
             .Append( " => Objects read: " )
             .Append( _objects.Count );
            foreach( var m in _debugContext )
            {
                b.Append( " > " ).Append( m );
            }
            throw new InvalidDataException( b.ToString(), inner );
        }
    }
}
