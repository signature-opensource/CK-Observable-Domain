using System;
using System.Collections;
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace CK.Observable
{
    public class ArraySerializer
    {
        /// <summary>
        /// Writes zero or more objects from any enumerable (that can be null).
        /// </summary>
        /// <param name="w">The binary serializer to use.</param>
        /// <param name="count">The number of objects to write. Must be zero or positive.</param>
        /// <param name="objects">
        /// The set of at least <paramref name="count"/> objects.
        /// Can be null (in such case count must be zero).
        /// </param>
        public static void WriteObjects( BinarySerializer w, int count, IEnumerable objects )
        {
            if( w == null ) throw new ArgumentNullException( nameof( w ) );
            if( count < 0 ) throw new ArgumentException( "Must be greater or equal to 0.", nameof( count ) );
            if( objects == null )
            {
                if( count != 0 ) throw new ArgumentNullException( nameof( objects ) );
                w.WriteSmallInt32( -1 );
            }
            else
            {
                w.WriteSmallInt32( count );
                if( count > 0 ) DoWriteObjects( w, count, objects );
            }
        }

        internal static void DoWriteObjects( BinarySerializer w, int count, IEnumerable objects )
        {
            foreach( var o in objects )
            {
                w.WriteObject( o );
                if( --count == 0 ) break;
            }
            if( count > 0 ) throw new ArgumentException( $"Not enough objects: missing {count} objects.", nameof( count ) );
        }


    }
}
