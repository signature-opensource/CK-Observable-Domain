using System;
using System.Diagnostics;

namespace CK.Observable
{
    class EnumTypeDeserializer<T, TU> : IDeserializationDriver<T>
        where T : Enum
    {
        readonly IDeserializationDriver<TU> _underlyingType;

        public EnumTypeDeserializer( IDeserializationDriver<TU> underlyingType )
        {
            Debug.Assert( underlyingType != null );
            Debug.Assert( Enum.GetUnderlyingType( typeof( T ) ).AssemblyQualifiedName == underlyingType.AssemblyQualifiedName );
            _underlyingType = underlyingType;
        }

        public object ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo ) => _underlyingType.ReadInstance( r, readInfo );

        T IDeserializationDriver<T>.ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo ) => (T)ReadInstance( r, readInfo );

        string IDeserializationDriver.AssemblyQualifiedName => typeof( T ).AssemblyQualifiedName;

    }
}
