using System;
using System.Diagnostics;

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

        bool ITypeSerializationDriver.IsFinalType => true;

        void ITypeSerializationDriver.WriteTypeInformation( BinarySerializer s ) => s.WriteSimpleType( Type, null );

        void ITypeSerializationDriver.WriteData( BinarySerializer w, object o ) => _underlyingType.WriteData( w, (TU)o );

        void ITypeSerializationDriver<T>.WriteData( BinarySerializer w, T o ) => _underlyingType.WriteData( w, o );
    }
}
