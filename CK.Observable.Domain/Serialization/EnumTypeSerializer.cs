using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    class EnumTypeSerializer<T,TU> : ITypeSerializationDriver<T>
        where T : Enum
    {
        readonly ITypeSerializationDriver<TU> _underlyingType;

        public EnumTypeSerializer( ITypeSerializationDriver<TU> underlyingType )
        {
            Debug.Assert( underlyingType != null );
            Debug.Assert( Enum.GetUnderlyingType( typeof( T ) ) == underlyingType.Type );
            _underlyingType = underlyingType;
        }

        public Type Type => typeof( T );

        void ITypeSerializationDriver.WriteTypeInformation( BinarySerializer s ) => s.WriteSimpleType( Type, null );

        public void WriteData( BinarySerializer w, object o ) => _underlyingType.WriteData( w, (TU)o );

        void ITypeSerializationDriver<T>.WriteData( BinarySerializer w, T o ) => WriteData( w, o );
    }
}
