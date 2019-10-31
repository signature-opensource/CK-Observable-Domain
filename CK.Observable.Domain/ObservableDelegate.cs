using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Observable
{
    /// <summary>
    /// Wraps a delegate to a static method or an instance method of
    /// an <see cref="ObservableObject"/>.
    /// This is an internal helper.
    /// </summary>
    [SerializationVersion(0)]
    struct ObservableDelegate
    {
        /// <summary>
        /// Wrapped delegate.
        /// </summary>
        public Delegate D;

        /// <summary>
        /// Deserializes the delegate.
        /// </summary>
        /// <param name="c">The context.</param>
        public ObservableDelegate( IBinaryDeserializerContext c )
        {
            D = null;
            var r = c.StartReading();
            int count = r.ReadNonNegativeSmallInt32();
            if( count > 0 )
            {
                Delegate final = null;
                Type tD = (Type)r.ReadObject();
                do
                {
                    object o = r.ReadObject();
                    string n = r.ReadSharedString();
                    if( o is Type t )
                    {
                        final = Delegate.Combine( final, Delegate.CreateDelegate( tD, t, r.ReadSharedString() ) );                     
                    }
                    else
                    {
                        final = Delegate.Combine( final, Delegate.CreateDelegate( tD, o, r.ReadSharedString() ) );
                    }
                }
                while( --count > 0 );
                D = final;
            }
        }

        /// <summary>
        /// Serializes this <see cref="ObservableDelegate"/>.
        /// </summary>
        /// <param name="w">The writer.</param>
        public void Write( BinarySerializer w )
        {
            var list = Cleanup();
            w.WriteNonNegativeSmallInt32( list.Length );
            if( list.Length > 0 )
            {
                w.WriteObject( list[0].GetType() );
                foreach( var d in list )
                {
                    w.WriteObject( d.Target ?? d.Method.DeclaringType );
                    w.WriteSharedString( d.Method.Name );
                }
            }
        }

        /// <summary>
        /// Adds a delegate.
        /// </summary>
        /// <param name="d">The delegate must be non null and be a static method or a method on a <see cref="ObservableObject"/>.</param>
        /// <param name="eventName">The event name (used for error messages).</param>
        public void Add( Delegate d, string eventName )
        {
            CheckNonNullAndSerializableDelegate( d, eventName );
            D = Delegate.Combine( D, d );
        }

        /// <summary>
        /// Removes a delegate and returns true if it has been removed.
        /// </summary>
        /// <param name="d">The delegate to remove. Can be null.</param>
        /// <returns>True if the delegate has been removed, false otherwise.</returns>
        public bool Remove( Delegate d )
        {
            var hBefore = D;
            D = Delegate.Remove( D, d );
            return !ReferenceEquals( hBefore, D );
        }

        /// <summary>
        /// Clears the delegate list.
        /// </summary>
        public void RemoveAll() => D = null;

        /// <summary>
        /// Removes any delegates that reference disposed objects and returns the
        /// invocation list (that can be empty but never null).
        /// </summary>
        /// <returns>The atomic delegate list without any disposed ObservableObject in it. Can be empty.</returns>
        public Delegate[] Cleanup()
        {
            var h = D;
            if( h == null ) return Array.Empty<Delegate>();
            var dList = h.GetInvocationList();
            bool needCleanup = false;
            Span<bool> cleanup = stackalloc bool[dList.Length];
            for( int i = 0; i < dList.Length; ++i )
            {
                var d = dList[i];
                if( d.Target is ObservableObject o && o.IsDisposed )
                {
                    cleanup[i] = needCleanup = true;
                }
            }
            if( needCleanup )
            {
                Delegate newOne = null;
                for( int i = 0; i < dList.Length; ++i )
                {
                    if( !cleanup[i] ) newOne = Delegate.Combine( newOne, dList[i] );
                }
                D = newOne;
                dList = newOne.GetInvocationList();
            }
            return dList;
        }

        static void CheckNonNullAndSerializableDelegate( Delegate value, string eventName )
        {
            if( value == null ) throw new ArgumentNullException( eventName );
            if( value.Target != null && !(value.Target is ObservableObject) )
            {
                throw new ArgumentException( $"Only static methods or ObservableObject's instance methods can be registered on {eventName} event.", eventName );
            }
        }


    }
}
