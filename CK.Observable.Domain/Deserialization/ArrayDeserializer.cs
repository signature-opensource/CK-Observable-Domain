using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        public T[] ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  ) => DoReadArray( r, _item );

        /// <summary>
        /// Reads an array of objects that have been previously written
        /// by <see cref="ArraySerializer.WriteObjects(int, System.Collections.IEnumerable)"/>.
        /// </summary>
        /// <returns>The object array.</returns>
        public static T[] ReadObjectArray( IBinaryDeserializer r )
        {
            if( r == null ) throw new ArgumentNullException( nameof( r ) );
            int len = r.ReadSmallInt32();
            if( len == -1 ) return null;
            if( len == 0 ) return Array.Empty<T>();
            var result = r.ImplementationServices.TrackObject( new T[len] );
            for( int i = 0; i < len; ++i ) result[i] = (T)r.ReadObject();
            return result;
        }

        /// <summary>
        /// Reads an array of <typeparamref name="T"/> that have been previously written
        /// by <see cref="ArraySerializer.WriteObjects{T}(int, IEnumerable{T}, ITypeSerializationDriver{T}).
        /// </summary>
        /// <typeparam name="T">Type of the item.</typeparam>
        /// <param name="itemDeserialization">Item deserializer. Must not be null.</param>
        /// <returns>The object array.</returns>
        public static T[] ReadArray( IBinaryDeserializer r, IDeserializationDriver<T> itemDeserialization )
        {
            if( r == null ) throw new ArgumentNullException( nameof( r ) );
            if( itemDeserialization == null ) throw new ArgumentNullException( nameof( itemDeserialization ) );
            return DoReadArray( r, itemDeserialization );
        }

        private static T[] DoReadArray( IBinaryDeserializer r, IDeserializationDriver<T> itemDeserialization )
        {
            int len = r.ReadSmallInt32();
            if( len == -1 ) return null;
            if( len == 0 ) return r.ImplementationServices.TrackObject( Array.Empty<T>() );

            var result = r.ImplementationServices.TrackObject( new T[len] );
            if( r.ReadBoolean() )
            {
                for( int i = 0; i < len; ++i ) result[i] = r.Read( itemDeserialization );
            }
            else
            {
                for( int i = 0; i < len; ++i ) result[i] = (T)r.ReadObject();
            }
            return result;
        }

        object IDeserializationDriver.ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  ) => ReadInstance( r, readInfo );
    }
}
