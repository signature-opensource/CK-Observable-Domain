using CK.Core;
using CK.Text;
using System.IO;
using System.Linq;
using System.Text;

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
        /// autonomous <see cref="ObservableDomain.Monitor"/> and no <see cref="ObservableDomain.DomainClient"/>.
        /// The <see cref="Root"/> is a new <typeparamref name="T"/> (obtained by calling a constructor
        /// that accepts a ObservableDomain).
        /// </summary>
        /// <param name="domainName">Name of the domain. Must not be null but can be empty.</param>
        public ObservableDomain( string domainName )
            : this( domainName, null, null )
        {
        }

        /// <summary>
        /// Initializes a new <see cref="ObservableDomain{T}"/> without any <see cref="ObservableDomain.DomainClient"/>.
        /// The <see cref="Root"/> is a new <typeparamref name="T"/> (obtained by calling the constructor
        /// that accepts a ObservableDomain).
        /// </summary>
        /// <param name="domainName">Name of the domain. Must not be null but can be empty.</param>
        /// <param name="monitor">
        /// The monitor that will become the <see cref="ObservableDomain.Monitor"/> of this domain.
        /// Can be null: a new, dedicated, ActivityMonitor will be created.
        /// </param>
        public ObservableDomain( string domainName, IActivityMonitor monitor )
            : this( domainName, null, monitor )
        {
        }

        /// <summary>
        /// Initializes a new <see cref="ObservableDomain{T}"/> with an autonomous <see cref="ObservableDomain.Monitor"/>.
        /// The <see cref="Root"/> is a new <typeparamref name="T"/> (obtained by calling the constructor
        /// that accepts a ObservableDomain).
        /// </summary>
        /// <param name="domainName">Name of the domain. Must not be null but can be empty.</param>
        /// <param name="client">The transaction manager. Can be null.</param>
        public ObservableDomain( string domainName, IObservableDomainClient client )
            : this( domainName, client, null )
        {
        }

        /// <summary>
        /// Initializes a new <see cref="ObservableDomain{T}"/>.
        /// The <see cref="Root"/> is a new <typeparamref name="T"/> (obtained by calling the constructor
        /// that accepts a ObservableDomain).
        /// </summary>
        /// <param name="domainName">Name of the domain. Must not be null but can be empty.</param>
        /// <param name="client">The transaction manager. Can be null.</param>
        /// <param name="monitor">
        /// The monitor that will become the <see cref="ObservableDomain.Monitor"/> of this domain.
        /// Can be null: a new, dedicated, ActivityMonitor will be created.
        /// </param>
        public ObservableDomain( string domainName, IObservableDomainClient client, IActivityMonitor monitor )
            : base( domainName, client, monitor )
        {
            if( AllRoots.Count != 0 ) BindRoots();
            else using( var initialization = new InitializationTransaction( this ) )
                {
                    Root = AddRoot<T>( initialization );
                }
        }

        /// <summary>
        /// Initializes a previously <see cref="ObservableDomain.Save"/>d domain.
        /// </summary>
        /// <param name="domainName">Name of the domain. Must not be null but can be empty.</param>
        /// <param name="client">The transaction manager to use. Can be null.</param>
        /// <param name="monitor">
        /// The monitor that will become the <see cref="ObservableDomain.Monitor"/> of this domain.
        /// Can be null: a new, dedicated, ActivityMonitor will be created.
        /// </param>
        /// <param name="s">The input stream.</param>
        /// <param name="leaveOpen">True to leave the stream opened.</param>
        /// <param name="encoding">Optional encoding for characters. Defaults to UTF-8.</param>
        public ObservableDomain(
            string domainName,
            IObservableDomainClient client,
            IActivityMonitor monitor,
            Stream s,
            bool leaveOpen = false,
            Encoding encoding = null )
            : base( domainName, client, monitor, s, leaveOpen, encoding )
        {
            BindRoots();
        }

        /// <summary>
        /// Gets the typed root object.
        /// </summary>
        public T Root { get; private set; }

        /// <summary>
        /// Overridden to bind our typed root.
        /// </summary>
        protected internal override void OnLoaded() => BindRoots();

        void BindRoots()
        {
            if( AllRoots.Count != 1 || !(AllRoots[0] is T) )
            {
                throw new InvalidDataException( $"Incompatible stream. No root of type {typeof( T ).FullName}. {AllRoots.Count} roots of type: {AllRoots.Select( t => t.GetType().Name ).Concatenate()}." );
            }
            Root = (T)AllRoots[0];
        }

    }
}
