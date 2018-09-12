using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    class ArrayTypeSerializer<T> : ITypeSerializationDriver<T[]>
    {
        readonly ITypeSerializationDriver<T> _itemSerializer;

        public ArrayTypeSerializer( ITypeSerializationDriver<T> itemSerializer )
        {
            Debug.Assert( itemSerializer != null );
            _itemSerializer = itemSerializer;
        }

        public Type Type => typeof(T[]);

        public void WriteData( BinarySerializer w, T[] o )
        {
            var tI = _itemSerializer.Type;
            bool monoType = tI.IsSealed || tI.IsValueType;
            w.Write( monoType );
            if( monoType )
            {
                if( o == null ) w.WriteSmallInt32( -1 );
                else
                {
                    w.WriteSmallInt32( o.Length );
                    foreach( var i in o )
                    {
                        _itemSerializer.WriteData( w, i );
                    }
                }
            }
            else
            {
                w.WriteObjects( o.Length, o );
            }
        }

        public void WriteData( BinarySerializer w, object o ) => WriteData( w, (T[])o );

        public void WriteTypeInformation( BinarySerializer s )
        {
            s.WriteSimpleType( Type );
        }
    }
}
