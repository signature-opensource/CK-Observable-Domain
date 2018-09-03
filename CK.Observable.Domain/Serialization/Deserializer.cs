using CK.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    public class Deserializer : IDisposable
    {
        readonly ObservableDomain _domain;
        readonly ObjectStreamReader _reader;
        readonly Stack<State> _stack;

        class State
        {
            public readonly ObjectStreamReader.TypeReadInfo ReadInfo;
            public int CurrentIndex;

            public State( ObjectStreamReader.TypeReadInfo readInfo )
            {
                CurrentIndex = -1;
                ReadInfo = readInfo;
            }
        }

        internal Deserializer( ObservableDomain d, Stream stream, bool leaveOpen, Encoding encoding = null )
        {
            _domain = d;
            _reader = new ObjectStreamReader( this, stream, leaveOpen, encoding ?? Encoding.UTF8 );
            _stack = new Stack<State>();
        }

        internal ObservableDomain Domain => _domain;

        internal ObjectStreamReader Reader => _reader;

        public ObjectStreamReader StartReading()
        {
            var head = _stack.Peek();
            if( head != null ) _reader.CurrentReadInfo = head.ReadInfo.TypePath[++head.CurrentIndex];
            else _reader.CurrentReadInfo = null;
            return _reader;
        }

        internal void PushCtorContext( ObjectStreamReader.TypeReadInfo readInfo )
        {
            _stack.Push( readInfo != null ? new State( readInfo ) : null );
        }

        internal void PopCtorContext() => _stack.Pop();

        void IDisposable.Dispose()
        {
            _reader.Dispose();
        }
    }
}
