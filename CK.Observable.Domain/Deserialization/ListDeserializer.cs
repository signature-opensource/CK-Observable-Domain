using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    public class ListDeserializer<T> : IDeserializationDriver<List<T>>
    {
        readonly IDeserializationDriver<T> _item;

        public ListDeserializer( IDeserializationDriver<T> item )
        {
            Debug.Assert( item != null );
            _item = item;
        }

        public string AssemblyQualifiedName => typeof(List<T>).AssemblyQualifiedName;

        public List<T> ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo ) => ReadList( r, _item );

        object IDeserializationDriver.ReadInstance( IBinaryDeserializer r, TypeReadInfo readInfo  ) => ReadInstance( r, readInfo );

        /// <summary>
        /// Reads a list of objects that have been previously written
        /// by <see cref="ArraySerializer.WriteObjects(int, System.Collections.IEnumerable)"/>.
        /// </summary>
        /// <returns>The object list.</returns>
        public static List<T> ReadObjectList( IBinaryDeserializer r )
        {
            if( r == null ) throw new ArgumentNullException( nameof( r ) );
            int len = r.ReadSmallInt32();
            if( len == -1 ) return null;
            if( len == 0 ) return r.ImplementationServices.TrackObject( new List<T>() );
            var result = r.ImplementationServices.TrackObject( new List<T>( len ) );
            while( --len >= 0 ) result.Add( (T)r.ReadObject() );
            return result;
        }

        /// <summary>
        /// Reads a list of <typeparamref name="T"/> that have been previously written
        /// by <see cref="ArraySerializer{T}.WriteObjects(BinarySerializer, int, IEnumerable{T}, ITypeSerializationDriver{T})" />.
        /// </summary>
        /// <typeparam name="T">Type of the item.</typeparam>
        /// <param name="itemDeserialization">Item deserializer. Must not be null.</param>
        /// <returns>The list.</returns>
        public static List<T> ReadList( IBinaryDeserializer r, IDeserializationDriver<T> itemDeserialization )
        {
            if( r == null ) throw new ArgumentNullException( nameof( r ) );
            if( itemDeserialization == null ) throw new ArgumentNullException( nameof( itemDeserialization ) );
            int len = r.ReadSmallInt32();
            if( len == -1 ) return null;
            if( len == 0 ) return r.ImplementationServices.TrackObject( new List<T>() );
            var result = r.ImplementationServices.TrackObject( new List<T>( len ) );
            if( r.ReadBoolean() )
            {
                for( int i = 0; i < len; ++i ) result.Add( r.Read( itemDeserialization ) );
            }
            else
            {
                for( int i = 0; i < len; ++i ) result.Add( (T)r.ReadObject() );
            }
            return result;
        }
    }
}
