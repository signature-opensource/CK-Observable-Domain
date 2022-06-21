using CK.BinarySerialization;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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

        public static void Skip( IBinaryDeserializer d )
        {
            d.Reader.ReadByte(); // Version
            int count = d.Reader.ReadNonNegativeSmallInt32();
            if( count > 0 )
            {
                // Skips the delegate signature.
                d.ReadTypeInfo();
                do
                {
                    // This is either the Target object instance or the Declaring type of the static method
                    // or null if the target was a SideKick.
                    object? o = d.ReadNullableObject<object>();
                    if( o != null )
                    {
                        d.Reader.ReadSharedString();
                        SkipArray( d );
                    }
                }
                while( --count > 0 );
                d.DebugCheckSentinel();
            }

            static void SkipArray( BinarySerialization.IBinaryDeserializer d )
            {
                int len = d.Reader.ReadNonNegativeSmallInt32();
                while( --len >= 0 ) d.ReadTypeInfo();
            }
        }

        /// <summary>
        /// Deserializes the delegate.
        /// </summary>
        /// <param name="d">The deserializer.</param>
        public ObservableDelegate( IBinaryDeserializer d )
        {
            static void ThrowError( string typeName, Type[] paramTypes, string methodName, bool isStatic )
            {
                var msg = $"Unable to find {(isStatic ? "static" : "")} method {methodName} on type {typeName} with parameters {paramTypes.Select( t => t.Name ).Concatenate()}.";
                msg += Environment.NewLine + "If the event has been suppressed, please use the static helper: ObservableEventHandler.Skip( IBinaryDeserializer d ).";
                throw new Exception( msg );
            }

            _d = null;
            d.Reader.ReadByte(); // Version
            int count = d.Reader.ReadNonNegativeSmallInt32();
            if( count > 0 )
            {
                Delegate? final = null;
                // Reads the type of the delegate itself that must exist (otherwise ResolveLocalType() throws).
                var tInfoD = d.ReadTypeInfo();
                Type tD = tInfoD.TargetType ?? tInfoD.ResolveLocalType();
                do
                {
                    object? o = d.ReadAnyNullable();
                    if( o != null )
                    {
                        string? methodName = d.Reader.ReadSharedString();
                        Debug.Assert( methodName != null );

                        // Use local DoReadArray (sharing this array makes no sense).
                        Type[] paramTypes = DoReadTypeArray( d );
                        if( o is Type t )
                        {
                            var m = t.GetMethod( methodName, BindingFlags.Static | BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.NonPublic, null, paramTypes, null );
                            if( m == null )
                            {
                                ThrowError( t.FullName!, paramTypes, methodName, true );
                                return; // Never.
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
                                ThrowError( o.GetType().FullName!, paramTypes, methodName, false );
                                return; // Never.
                            }
                            final = Delegate.Combine( final, Delegate.CreateDelegate( tD, o, m ) );
                        }
                    }
                }
                while( --count > 0 );
                _d = final;
                d.DebugCheckSentinel();
            }

            static Type[] DoReadTypeArray( IBinaryDeserializer r )
            {
                int len = r.Reader.ReadNonNegativeSmallInt32();
                if( len == 0 ) return Array.Empty<Type>();
                var result = new Type[len];
                for( int i = 0; i < len; ++i )
                {
                    var tInfo = r.ReadTypeInfo();
                    result[i] = tInfo.TargetType ?? tInfo.ResolveLocalType();
                }
                return result;
            }
        }

        /// <summary>
        /// Serializes this <see cref="ObservableDelegate"/>.
        /// </summary>
        /// <param name="s">The writer.</param>
        public void Write( BinarySerialization.IBinarySerializer s )
        {
            var list = Cleanup();
            s.Writer.Write( (byte)0 ); // Version
            s.Writer.WriteNonNegativeSmallInt32( list.Length );
            if( list.Length > 0 )
            {
                // Writes the Delegate's type (it's a sealed class).
                // Its signature contains the TEventArgs type (this is used only to implement ObservableEventHandler<TEventArgs>).
                s.WriteTypeInfo( list[0].GetType() );
                foreach( var d in list )
                {
                    // Don't serialize sidekick handlers.
                    if( d.Target is ObservableDomainSidekick )
                    {
                        s.WriteAnyNullable( null );
                    }
                    else
                    {
                        s.WriteObject( d.Target ?? d.Method.DeclaringType! );
                        s.Writer.WriteSharedString( d.Method.Name );
                        var paramInfos = d.Method.GetParameters();
                        // Writes the type array directly.
                        // We must write the actual parameter type because of contravariance:
                        // the method's parameters may be a specialization of the Delegate's ones.
                        // Option:
                        // This should be changed:
                        //  - Write should control that the method name is unique.
                        //  - But we must implement [PreviousNames(...)] attribute to allow existing ambiguous
                        //  methods to actually be renamed.
                        //  - Then, only the method name should be used.
                        // => The reason is that changing the types of the signature will be handled transparently: only the name
                        //    binds it, the method parameters are then free to evolve.
                        s.Writer.WriteNonNegativeSmallInt32( paramInfos.Length );
                        foreach( var p in paramInfos ) s.WriteTypeInfo( p.ParameterType );
                    }
                }
                s.DebugWriteSentinel();
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
        public void Add( Delegate d, string? eventName )
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
                Delegate? newOne = null;
                for( int i = 0; i < dList.Length; ++i )
                {
                    if( !cleanup[i] ) newOne = Delegate.Combine( newOne, dList[i] );
                }
                if( (_d = newOne) != null ) dList = newOne!.GetInvocationList();
                else dList = Array.Empty<Delegate>();
            }
            return dList;
        }

        static void CheckNonNullAndValidTarget( Delegate value, string? eventName )
        {
            if( value == null ) throw new ArgumentNullException( eventName );
            if( value.Target != null
                && !(value.Target is IDestroyable)
                && !(value.Target is ObservableDomainSidekick))
            {
                if( eventName == null ) eventName = "<missing event name>";
                throw new ArgumentException( $"Only static methods or Observable/InternalObject or Sidekick's instance methods can be registered on '{eventName}' event.", eventName );
            }
        }
    }
}
