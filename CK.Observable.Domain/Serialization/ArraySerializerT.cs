using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CK.Observable
{
    class ArraySerializer<T> : ArraySerializer, ITypeSerializationDriver<T[]>
    {
        readonly ITypeSerializationDriver<T> _itemSerializer;

        public ArraySerializer( ITypeSerializationDriver<T> itemSerializer )
        {
            Debug.Assert( itemSerializer != null );
            _itemSerializer = itemSerializer;
        }

        public Type Type => typeof(T[]);

        void ITypeSerializationDriver<T[]>.WriteData( BinarySerializer w, T[] o ) => DoWriteData( w, o );

        void ITypeSerializationDriver.WriteData( BinarySerializer w, object o ) => DoWriteData( w, (T[])o );

        void DoWriteData( BinarySerializer w, T[] o ) => WriteObjects( w, o?.Length ?? 0, o, _itemSerializer );

        public void WriteTypeInformation( BinarySerializer s ) => s.WriteSimpleType( Type, null );

        /// <summary>
        /// Writes any list content.
        /// </summary>
        /// <typeparam name="T">The item type.</typeparam>
        /// <param name="w">The binary serializer to use. Must not be null.</param>
        /// <param name="count">The number of items. Must be 0 zero or positive.</param>
        /// <param name="items">The items. Can be null (in such case, <paramref name="count"/> must be zero).</param>
        /// <param name="itemSerializer">Available item serializer if known.</param>
        public static void WriteObjects( BinarySerializer w, int count, IEnumerable<T> items, ITypeSerializationDriver<T> itemSerializer = null )
        {
            if( w == null ) throw new ArgumentNullException( nameof( w ) );
            if( count < 0 ) throw new ArgumentException( "Must be greater or equal to 0.", nameof( count ) );
            if( items == null )
            {
                if( count != 0 ) throw new ArgumentNullException( nameof( items ) );
                w.WriteSmallInt32( -1 );
                return;
            }
            w.WriteSmallInt32( count );
            if( count > 0 )
            {
                var tI = itemSerializer.Type;
                bool monoType = tI.IsSealed || tI.IsValueType;
                w.Write( monoType );
                if( monoType )
                {
                    if( itemSerializer == null ) itemSerializer = w.Drivers.FindDriver<T>();
                    foreach( var i in items )
                    {
                        w.Write( i, itemSerializer );
                        if( --count == 0 ) break;
                    }
                    if( count > 0 ) throw new ArgumentException( $"Not enough items: missing {count} items.", nameof( count ) );
                }
                else
                {
                    DoWriteObjects( w, count, items );
                }
            }
        }

    }
}
