using System;
using System.Diagnostics;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace CK.Observable
{
    public class ArrayDeserializer<T> : IDeserializationDriver<T[]>
    {
        readonly IDeserializationDriver<T> _item;

        public ArrayDeserializer( IDeserializationDriver<T> item )
        {
            Debug.Assert( item != null );
            _item = item;
        }

        public string AssemblyQualifiedName => typeof(T[]).AssemblyQualifiedName;

        /// <summary>
        /// Reads an array of <typeparamref name="T"/>.
        /// </summary>
        /// <param name="r">The deserializer.</param>
        /// <param name="readInfo">The read info (unused).</param>
        /// <returns>The array of items.</returns>
        public T[]? ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo, bool mustRead  ) => DoReadArray( r, _item, mustRead );

        public static T[]? ReadArray( IBinaryDeserializer r, IDeserializationDriver<T> itemDeserialization, bool mustRead )
        {
            if( r == null ) throw new ArgumentNullException( nameof( r ) );
            if( itemDeserialization == null ) throw new ArgumentNullException( nameof( itemDeserialization ) );
            return DoReadArray( r, itemDeserialization, mustRead );
        }

        private static T[]? DoReadArray( IBinaryDeserializer r, IDeserializationDriver<T> itemDeserialization, bool mustRead )
        {
            if( r.ImplementationServices.SerializationVersion >= 7 )
            {
                if( !mustRead && !r.ImplementationServices.ReadNewObject<T[]>( out var already ) ) return already;
                int len = r.ReadSmallInt32();
                if( len == 0 ) return r.ImplementationServices.TrackObject( Array.Empty<T>() );

                var result = r.ImplementationServices.TrackObject( new T[len] );
                if( r.ReadBoolean() )
                {
                    for( int i = 0; i < len; ++i ) result[i] = itemDeserialization.ReadInstance( r, null, false );
                }
                else
                {
                    for( int i = 0; i < len; ++i ) result[i] = (T)r.ReadObject();
                }
                return result;
            }
            else
            {
                int len = r.ReadSmallInt32();
                if( len == -1 ) return null;
                if( len == 0 ) return r.ImplementationServices.TrackObject( Array.Empty<T>() );

                var result = r.ImplementationServices.TrackObject( new T[len] );
                if( r.ReadBoolean() )
                {
                    for( int i = 0; i < len; ++i ) result[i] = itemDeserialization.ReadInstance( r, null, false );
                }
                else
                {
                    for( int i = 0; i < len; ++i ) result[i] = (T)r.ReadObject();
                }
                return result;
            }
        }

        object IDeserializationDriver.ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo, bool mustRead ) => ReadInstance( r, readInfo, mustRead );
    }
}
