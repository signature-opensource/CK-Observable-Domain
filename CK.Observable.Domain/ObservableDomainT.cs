using CK.Core;
using CK.Text;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace CK.Observable
{
    /// <summary>
    /// <see cref="ObservableDomain"/> with a strongly typed <see cref="Root"/>.
    /// </summary>
    /// <typeparam name="T">Type of the root object.</typeparam>
    public sealed class ObservableDomain<T> : ObservableDomain, IObservableDomain<T>
        where T : ObservableRootObject
    {
        /// <summary>
        /// Initializes a new <see cref="ObservableDomain{T}"/> without any <see cref="ObservableDomain.DomainClient"/>.
        /// The <see cref="Root"/> is a new <typeparamref name="T"/> (obtained by calling the constructor
        /// that accepts a ObservableDomain).
        /// </summary>
        /// <param name="monitor">The monitor used to log the construction of this domain. Cannot be null.</param>
        /// <param name="domainName">Name of the domain. Must not be null but can be empty.</param>
        /// <param name="serviceProvider">The service providers that will be used to resolve the <see cref="ObservableDomainSidekick"/> objects.</param>
        public ObservableDomain( IActivityMonitor monitor, string domainName, IServiceProvider? serviceProvider = null )
            : this( monitor, domainName, null, serviceProvider )
        {
        }

        /// <summary>
        /// Initializes a new <see cref="ObservableDomain{T}"/>.
        /// The <see cref="Root"/> is a new <typeparamref name="T"/> (obtained by calling the constructor
        /// that accepts a ObservableDomain).
        /// </summary>
        /// <param name="monitor">The monitor used to log the construction of this domain. Cannot be null.</param>
        /// <param name="domainName">Name of the domain. Must not be null but can be empty.</param>
        /// <param name="client">The observable client (head of the Chain of Responsibility) to use. Can be null.</param>
        /// <param name="serviceProvider">The service providers that will be used to resolve the <see cref="ObservableDomainSidekick"/> objects.</param>
        public ObservableDomain( IActivityMonitor monitor, string domainName, IObservableDomainClient? client, IServiceProvider? serviceProvider = null )
            : base( monitor, domainName, client, serviceProvider )
        {
            if( AllRoots.Count != 0 ) BindRoots();
            else using( var initialization = new InitializationTransaction( monitor, this ) )
                {
                    Root = AddRoot<T>( initialization );
                }
        }

        /// <summary>
        /// Initializes a previously <see cref="ObservableDomain.Save"/>d domain.
        /// </summary>
        /// <param name="monitor">The monitor used to log the construction of this domain. Cannot be null.</param>
        /// <param name="domainName">Name of the domain. Must not be null but can be empty.</param>
        /// <param name="client">The observable client (head of the Chain of Responsibility) to use. Can be null.</param>
        /// <param name="stream">The input stream.</param>
        /// <param name="leaveOpen">True to leave the stream opened.</param>
        /// <param name="encoding">Optional encoding for characters. Defaults to UTF-8.</param>
        /// <param name="serviceProvider">The service providers that will be used to resolve the <see cref="ObservableDomainSidekick"/> objects.</param>
        /// <param name="loadHook">The load hook to apply. See loadHook parameter of <see cref="ObservableDomain.Load(IActivityMonitor, Stream, bool, Encoding?, int, Func{ObservableDomain, bool}?)"/>.</param>
        public ObservableDomain(
            IActivityMonitor monitor,
            string domainName,
            IObservableDomainClient client,
            Stream stream,
            bool leaveOpen = false,
            Encoding encoding = null,
            IServiceProvider? serviceProvider = null,
            Func<ObservableDomain, bool>? loadHook = null )
            : base( monitor, domainName, client, stream, leaveOpen, encoding, serviceProvider, loadHook )
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
