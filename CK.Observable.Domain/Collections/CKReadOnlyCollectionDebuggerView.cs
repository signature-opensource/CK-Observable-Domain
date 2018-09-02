using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace CK.Observable.Impl
{
    /// <summary>
    /// Debugger object for <see cref="IReadOnlyCollection{T}"/>.
    /// </summary>
    /// <typeparam name="T">Type of elements in the collection.</typeparam>

    [ExcludeFromCodeCoverage]
    sealed class CKReadOnlyCollectionDebuggerView<T>
    {
        readonly IReadOnlyCollection<T> _collection;
        
        /// <summary>
        /// Called by the debugger when needed.
        /// </summary>
        /// <param name="collection">The collection to debug.</param>
        public CKReadOnlyCollectionDebuggerView( IReadOnlyCollection<T> collection )
        {
            if( collection == null ) throw new ArgumentNullException( "collection" );
            _collection = collection;
        }

        /// <summary>
        /// Gets the items as a flattened array view.
        /// </summary>
        [DebuggerBrowsable( DebuggerBrowsableState.RootHidden )]
        public T[] Items
        {
            get
            {
                T[] a = new T[_collection.Count];
                int i = 0; 
                foreach( var e in _collection ) a[i++] = e;
                return a;
            }
        }
    }

    /// <summary>
    /// Debugger for adapters with two types (an exposed type and an inner type).
    /// </summary>
    /// <typeparam name="T">Type of the exposed element.</typeparam>
    /// <typeparam name="TInner">Type of the inner element.</typeparam>

    [ExcludeFromCodeCoverage]
    sealed class ReadOnlyCollectionDebuggerView<T, TInner>
    {
        readonly IReadOnlyCollection<T> _collection;

        /// <summary>
        /// Called by the debugger when needed.
        /// </summary>
        /// <param name="collection">The collection to debug.</param>
        public ReadOnlyCollectionDebuggerView( IReadOnlyCollection<T> collection )
        {
            if( collection == null ) throw new ArgumentNullException( "collection" );
            _collection = collection;
        }

        /// <summary>
        /// Gets the items as a flattened array view.
        /// </summary>
        [DebuggerBrowsable( DebuggerBrowsableState.RootHidden )]
        public T[] Items
        {
            get
            {
                T[] a = new T[_collection.Count];
                int i = 0;
                foreach( var e in _collection ) a[i++] = e;
                return a;
            }
        }

    }

}
