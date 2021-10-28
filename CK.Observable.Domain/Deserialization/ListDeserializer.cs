using System;
using System.Collections.Generic;
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace CK.Observable
{
    public class ListDeserializer<T> : IDeserializationDriver<List<T>>
    {
        readonly IDeserializationDriver<T> _item;

        public ListDeserializer( IDeserializationDriver<T> item )
        {
            if( item == null ) throw new ArgumentException( $"Type {typeof( T )} seems to be not serializable." );
            _item = item;
        }

        public string AssemblyQualifiedName => typeof( List<T> ).AssemblyQualifiedName;

        public List<T> ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo, bool mustRead ) => ReadList( r, _item, mustRead );

        object IDeserializationDriver.ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo, bool mustRead ) => ReadInstance( r, readInfo, mustRead );

        public static List<T> ReadList( IBinaryDeserializer r, IDeserializationDriver<T> itemDeserialization, bool mustRead )
        {
            if( r.ImplementationServices.SerializationVersion >= 7 )
            {
                if( !mustRead && !r.ImplementationServices.ReadNewObject<List<T>>( out var already ) ) return already;
                int len = r.ReadSmallInt32();
                if( len == 0 ) return r.ImplementationServices.TrackObject( new List<T>() );

                var result = r.ImplementationServices.TrackObject( new List<T>() );
                if( r.ReadBoolean() )
                {
                    for( int i = 0; i < len; ++i ) result.Add( itemDeserialization.ReadInstance( r, null, false ) );
                }
                else
                {
                    for( int i = 0; i < len; ++i ) result.Add( (T)r.ReadObject() );
                }
                return result;
            }
            else
            {
                if( r == null ) throw new ArgumentNullException( nameof( r ) );
                if( itemDeserialization == null ) throw new ArgumentNullException( nameof( itemDeserialization ) );
                int len = r.ReadSmallInt32();
                if( len == -1 ) return null;
                if( len == 0 ) return r.ImplementationServices.TrackObject( new List<T>() );
                var result = r.ImplementationServices.TrackObject( new List<T>( len ) );
                if( r.ReadBoolean() )
                {
                    for( int i = 0; i < len; ++i ) result.Add( itemDeserialization.ReadInstance( r, null, false ) );
                }
                else
                {
                    for( int i = 0; i < len; ++i ) result.Add( (T)r.ReadObject() );
                }
                return result;
            }
        }
    }
}
