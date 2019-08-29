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
    /// <see cref="ObservableDomain"/> with strongly typed <see cref="Root1"/>, <see cref="Root2"/>
    /// and <see cref="Root3"/> observable roots.
    /// </summary>
    /// <typeparam name="T1">Type of the first root object.</typeparam>
    /// <typeparam name="T2">Type of the second root object.</typeparam>
    /// <typeparam name="T3">Type of the third root object.</typeparam>
    public class ObservableDomain<T1,T2,T3> : ObservableDomain
        where T1 : ObservableRootObject
        where T2 : ObservableRootObject
        where T3 : ObservableRootObject
    {
        /// <summary>
        /// Initializes a new <see cref="ObservableDomain{T1,T2,T3}"/> with an
        /// automous <see cref="ObservableDomain.Monitor"/> and no <see cref="ObservableDomain.DomainClient"/>.
        /// The roots are initialized with new instances of their respective type (obtained by calling the constructor that accepts a ObservableDomain).
        /// </summary>
        public ObservableDomain()
            : this( null, null )
        {
        }

        /// <summary>
        /// Initializes a new <see cref="ObservableDomain{T1,T2,T3}"/> without any <see cref="ObservableDomain.DomainClient"/>.
        /// The roots are initialized with new instances of their respective type (obtained by calling the constructor that accepts a ObservableDomain).
        /// </summary>
        /// <param name="monitor">Monitor to use (when null, an automous monitor is automatically created).</param>
        public ObservableDomain( IActivityMonitor monitor )
            : this( null, monitor )
        {
        }

        /// <summary>
        /// Initializes a new <see cref="ObservableDomain{T1,T2,T3}"/> with an autonomous <see cref="ObservableDomain.Monitor"/>.
        /// The roots are initialized with new instances of their respective type (obtained by calling the constructor that accepts a ObservableDomain).
        /// </summary>
        /// <param name="tm">The transaction manager. Can be null.</param>
        public ObservableDomain( IObservableDomainClient tm )
            : this( tm, null )
        {
        }

        /// <summary>
        /// Initializes a new <see cref="ObservableDomain{T1,T2,T3}"/>.
        /// The roots are initialized with new instances of their respective type (obtained by calling the constructor that accepts a ObservableDomain).
        /// </summary>
        /// <param name="tm">The transaction manager. Can be null.</param>
        /// <param name="monitor">Monitor to use (when null, an automous monitor is automatically created).</param>
        public ObservableDomain( IObservableDomainClient tm, IActivityMonitor monitor )
            : base( tm, monitor )
        {
            if( AllRoots.Count != 0 )
            {
                CheckRoot();
                Root1 = (T1)AllRoots[0];
                Root2 = (T2)AllRoots[1];
                Root3 = (T3)AllRoots[2];
            }
            else using( var initialization = new InitializationTransaction( this ) )
            {
                Root1 = AddRoot<T1>( initialization );
                Root2 = AddRoot<T2>( initialization );
                Root3 = AddRoot<T3>( initialization );
            }
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
            CheckRoot();
            Root1 = (T1)AllRoots[0];
            Root2 = (T2)AllRoots[1];
            Root3 = (T3)AllRoots[2];
        }

        void CheckRoot()
        {
            if( AllRoots.Count != 3
                || !(AllRoots[0] is T1)
                || !(AllRoots[1] is T2)
                || !(AllRoots[2] is T3) )
            {
                throw new InvalidDataException( $"Incompatible stream. No root of type {typeof( T1 ).Name}, {typeof( T2 ).Name} and {typeof( T3 ).Name}. {AllRoots.Count} roots of type: {AllRoots.Select( t => t.GetType().Name ).Concatenate()}." );
            }
        }

        /// <summary>
        /// Gets the first typed root object.
        /// </summary>
        public T1 Root1 { get; }


        /// <summary>
        /// Gets the second typed root object.
        /// </summary>
        public T2 Root2 { get; }

        /// <summary>
        /// Gets the third typed root object.
        /// </summary>
        public T3 Root3 { get; }

    }
}
