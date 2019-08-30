using CK.Core;
using CK.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    /// <summary>
    /// <see cref="ObservableDomain"/> with a strongly typed <see cref="Root"/>.
    /// </summary>
    /// <typeparam name="T">Type of the root object.</typeparam>
    public class ObservableDomain<T> : ObservableDomain where T : ObservableRootObject
    {
        /// <summary>
        /// Initializes a new <see cref="ObservableDomain{T}"/> with an
        /// automous <see cref="ObservableDomain.Monitor"/> and no <see cref="ObservableDomain.DomainClient"/>.
        /// The <see cref="Root"/> is a new <typeparamref name="T"/> (obtained by calling a constructor
        /// that accepts a ObservableDomain).
        /// </summary>
        public ObservableDomain()
            : this( null, null )
        {
        }

        /// <summary>
        /// Initializes a new <see cref="ObservableDomain{T}"/> without any <see cref="ObservableDomain.DomainClient"/>.
        /// The <see cref="Root"/> is a new <typeparamref name="T"/> (obtained by calling the constructor
        /// that accepts a ObservableDomain).
        /// </summary>
        /// <param name="monitor">Monitor to use (when null, an automous monitor is automatically created).</param>
        public ObservableDomain( IActivityMonitor monitor )
            : this( null, monitor )
        {
        }

        /// <summary>
        /// Initializes a new <see cref="ObservableDomain{T}"/> with an autonomous <see cref="ObservableDomain.Monitor"/>.
        /// The <see cref="Root"/> is a new <typeparamref name="T"/> (obtained by calling the constructor
        /// that accepts a ObservableDomain).
        /// </summary>
        /// <param name="tm">The transaction manager. Can be null.</param>
        public ObservableDomain( IObservableDomainClient tm )
            : this( tm, null )
        {
        }

        /// <summary>
        /// Initializes a new <see cref="ObservableDomain{T}"/>.
        /// The <see cref="Root"/> is a new <typeparamref name="T"/> (obtained by calling the constructor
        /// that accepts a ObservableDomain).
        /// </summary>
        /// <param name="tm">The transaction manager. Can be null.</param>
        /// <param name="monitor">Monitor to use (when null, an automous monitor is automatically created).</param>
        public ObservableDomain( IObservableDomainClient tm, IActivityMonitor monitor )
            : base( tm, monitor )
        {
            if( AllRoots.Count != 0 ) BindRoots();
            else using( var initialization = new InitializationTransaction( this ) )
            {
                Root = AddRoot<T>( initialization );
            }
        }

        /// <summary>
        /// Overridden to bind our typed root.
        /// </summary>
        internal protected override void OnLoaded() => BindRoots();

        void BindRoots()
        {
            if( AllRoots.Count != 1 || !(AllRoots[0] is T) )
            {
                throw new InvalidDataException( $"Incompatible stream. No root of type {typeof( T ).FullName}. {AllRoots.Count} roots of type: {AllRoots.Select( t => t.GetType().Name ).Concatenate()}." );
            }
            Root = (T)AllRoots[0];
        }

        /// <summary>
        /// Initializes a previously <see cref="ObservableDomain.Save"/>d domain.
        /// </summary>
        /// <param name="tm">The transaction manager to use. Can be null.</param>
        /// <param name="monitor">The monitor associated to the domain. Can be null (a dedicated one will be created).</param>
        /// <param name="s">The input stream.</param>
        /// <param name="leaveOpen">True to leave the stream opened.</param>
        /// <param name="encoding">Optional encoding for characters. Defaults to UTF-8.</param>
        public ObservableDomain(
            IObservableDomainClient tm,
            IActivityMonitor monitor,
            Stream s,
            bool leaveOpen = false,
            Encoding encoding = null )
            : base( tm, monitor, s, leaveOpen, encoding )
        {
            BindRoots();
        }

        /// <summary>
        /// Gets the typed root object.
        /// </summary>
        public T Root { get; private set; }

    }
}
