using CK.Core;
using CK.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace CK.Observable
{
    /// <summary>
    /// Wraps a delegate to a static method or an instance method of a <see cref="IDestroyable"/>.
    /// This is an internal helper.
    /// </summary>
    struct ObservableDelegate
    {
        /// <summary>
        /// Wrapped delegate.
        /// </summary>
        Delegate? _d;

        public static void Skip( IBinaryDeserializer r )
        {
            r.DebugCheckSentinel();
            int count = r.ReadNonNegativeSmallInt32();
            if( count > 0 )
            {
                r.ReadType();
                object? o = r.ReadObject();
                if( o != null )
                {
                    do
                    {
                        r.ReadSharedString();
                        SkipArray( r );
                    }
                    while( --count > 0 );
                }
                r.DebugCheckSentinel();
            }

            static void SkipArray( IBinaryDeserializer r )
            {
                int len = r.ReadNonNegativeSmallInt32();
                while( --len >= 0 ) r.ReadType();
            }

        }

        /// <summary>
        /// Deserializes the delegate.
        /// </summary>
        /// <param name="r">The deserializer.</param>
        public ObservableDelegate( IBinaryDeserializer r )
        {
            static void ThrowError( string typeName, Type[] paramTypes, string methodName, bool isStatic )
            {
                var msg = $"Unable to find {(isStatic ? "static" : "")} method {methodName} on type {typeName} with parameters {paramTypes.Select( t => t.Name ).Concatenate()}.";
                msg += Environment.NewLine + "If the event has been suppressed, please use the static helper: ObservableEventHandler.Skip( IBinaryDeserializer r ).";
                throw new Exception( msg );
            }

            r.DebugCheckSentinel();
            _d = null;
            int count = r.ReadNonNegativeSmallInt32();
            if( count > 0 )
            {
                Delegate? final = null;
                Type tD = r.ReadType();
                do
                {
                    object? o = r.ReadObject();
                    if( o != null )
                    {
                        string methodName = r.ReadSharedString();
                        // Use local DoReadArray since ArrayDeserializer<Type>.ReadArray track the array (and ArraySerializer<Type>.WriteObjects don't):
                        // sharing this array makes no sense.
                        Type[] paramTypes = DoReadArray( r );
                        if( o is Type t )
                        {
                            var m = t.GetMethod( methodName, BindingFlags.Static | BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.NonPublic, null, paramTypes, null );
                            if( m == null )
                            {
                                ThrowError( t.FullName, paramTypes, methodName, true );
                            }
                            final = Delegate.Combine( final, Delegate.CreateDelegate( tD, m, true ) );
                        }
                        else
                        {
                            var oT = o.GetType();
                            var m = oT.GetMethod( methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, paramTypes, null );
                            while( m == null && (oT = oT.BaseType) != null )
                            {
                                m = oT.GetMethod( methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, paramTypes, null );
                            }
                            if( m == null )
                            {
                                ThrowError( o.GetType().FullName, paramTypes, methodName, false );
                            }
                            final = Delegate.Combine( final, Delegate.CreateDelegate( tD, o, m ) );
                        }
                    }
                }
                while( --count > 0 );
                _d = final;
                r.DebugCheckSentinel();
            }

            static Type[] DoReadArray( IBinaryDeserializer r )
            {
                int len = r.ReadNonNegativeSmallInt32();
                if( len == 0 ) return Array.Empty<Type>();
                var result = new Type[len];
                for( int i = 0; i < len; ++i ) result[i] = r.ReadType();
                return result;
            }
        }

        /// <summary>
        /// Serializes this <see cref="ObservableDelegate"/>.
        /// </summary>
        /// <param name="w">The writer.</param>
        public void Write( BinarySerializer w )
        {
            var list = Cleanup();
            w.DebugWriteSentinel();
            w.WriteNonNegativeSmallInt32( list.Length );
            if( list.Length > 0 )
            {
                w.Write( list[0].GetType() );
                foreach( var d in list )
                {
                    // Don't serialize sidekick handlers.
                    if( d.Target is ObservableDomainSidekick )
                    {
                        w.WriteObject( null );
                    }
                    else
                    {
                        w.WriteObject( d.Target ?? d.Method.DeclaringType );
                        w.WriteSharedString( d.Method.Name );
                        var paramInfos = d.Method.GetParameters();
                        w.WriteNonNegativeSmallInt32( paramInfos.Length );
                        foreach( var p in paramInfos ) w.Write( p.ParameterType );
                    }
                }
                w.DebugWriteSentinel();
            }
        }

        /// <summary>
        /// Gets whether at least one handler is registered.
        /// </summary>
        public bool HasHandlers => _d != null;

        /// <summary>
        /// Adds a delegate.
        /// </summary>
        /// <param name="d">The delegate must be non null and be a static method or a method on a <see cref="ObservableObject"/>.</param>
        /// <param name="eventName">The event name (used for error messages).</param>
        public void Add( Delegate d, string eventName )
        {
            CheckNonNullAndValidTarget( d, eventName );
            _d = Delegate.Combine( _d, d );
        }

        /// <summary>
        /// Removes a delegate and returns true if it has been removed.
        /// </summary>
        /// <param name="d">The delegate to remove. Can be null.</param>
        /// <returns>True if the delegate has been removed, false otherwise.</returns>
        public bool Remove( Delegate d )
        {
            var hBefore = _d;
            _d = Delegate.Remove( _d, d );
            return !ReferenceEquals( hBefore, _d );
        }

        /// <summary>
        /// Clears the delegate list.
        /// </summary>
        public void RemoveAll() => _d = null;

        /// <summary>
        /// Removes any delegates that reference disposed objects and returns the
        /// invocation list (that can be empty but never null).
        /// </summary>
        /// <returns>The atomic delegate list without any disposed ObservableObject in it. Can be empty.</returns>
        public Delegate[] Cleanup()
        {
            var h = _d;
            if( h == null ) return Array.Empty<Delegate>();
            var dList = h.GetInvocationList();
            bool needCleanup = false;
            Span<bool> cleanup = stackalloc bool[dList.Length];
            for( int i = 0; i < dList.Length; ++i )
            {
                var d = dList[i];
                if( d.Target is IDestroyable o && o.IsDestroyed )
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
                if( (_d = newOne) != null ) dList = newOne.GetInvocationList();
                else dList = Array.Empty<Delegate>();
            }
            return dList;
        }

        static void CheckNonNullAndValidTarget( Delegate value, string eventName )
        {
            if( value == null ) throw new ArgumentNullException( eventName );
            if( value.Target != null
                && !(value.Target is IDestroyable)
                && !(value.Target is ObservableDomainSidekick))
            {
                throw new ArgumentException( $"Only static methods or Observable/InternalObject or Sidekick's instance methods can be registered on {eventName} event.", eventName );
            }
        }
    }
}
