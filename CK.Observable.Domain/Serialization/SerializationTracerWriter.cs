using CK.Core;
using System;
using System.Collections.Generic;

namespace CK.Observable
{
    class SerializationTracer
    {
        readonly CKBinaryWriter _w;
        readonly Stack<string> _path;

        public SerializationTracer( CKBinaryWriter w )
        {
            _w = w;
            _path = new Stack<string>();
        }

        IDisposable OnWriteObject( object o )
        {
            string p = o.GetType().FullName;
            _w.Write( p );
            return Util.CreateDisposableAction( () =>
            {
                _path.Pop();
            } );
        }
    }
}
