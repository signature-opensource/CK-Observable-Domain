using System;
using System.Diagnostics;
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace CK.Observable
{
    class EnumTypeDeserializer<T, TU> : IDeserializationDriver<T>
        where T : Enum
    {
        readonly IDeserializationDriver<TU> _underlyingType;

        public EnumTypeDeserializer( IDeserializationDriver<TU> underlyingType )
        {
            Debug.Assert( underlyingType != null );
            _underlyingType = underlyingType;
        }

        public object ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo, bool mustRead ) => _underlyingType.ReadInstance( r, readInfo, mustRead );

        T IDeserializationDriver<T>.ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo, bool mustRead ) => (T)ReadInstance( r, readInfo, mustRead );
    }
}
