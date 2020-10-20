using CK.Core;
using CK.Text;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace CK.Observable
{
    /// <summary>
    /// <see cref="ObservableDomain"/> with 4 strongly typed roots.
    /// </summary>
    /// <typeparam name="T1">Type of the first root object.</typeparam>
    /// <typeparam name="T2">Type of the second root object.</typeparam>
    /// <typeparam name="T3">Type of the third root object.</typeparam>
    /// <typeparam name="T4">Type of the fourth root object.</typeparam>
    public sealed class ObservableDomain<T1, T2, T3, T4> : ObservableDomain, IObservableDomain<T1, T2, T3, T4>
        where T1 : ObservableRootObject
        where T2 : ObservableRootObject
        where T3 : ObservableRootObject
        where T4 : ObservableRootObject
    {
        /// <summary>
        /// Initializes a new <see cref="ObservableDomain{T1,T2,T3,T4}"/> without any <see cref="ObservableDomain.DomainClient"/>.
        /// The roots are initialized with new instances of their respective type (obtained by calling the constructor that accepts a ObservableDomain).
        /// </summary>
        /// <param name="monitor">The monitor used to log the construction of this domain. Cannot be null.</param>
        /// <param name="domainName">Name of the domain. Must not be null but can be empty.</param>
        /// <param name="serviceProvider">The service providers that will be used to resolve the <see cref="ObservableDomainSidekick"/> objects.</param>
        public ObservableDomain( IActivityMonitor monitor, string domainName, IServiceProvider? serviceProvider = null )
            : this( monitor, domainName, null, serviceProvider )
        {
        }

        /// <summary>
        /// Initializes a new <see cref="ObservableDomain{T1,T2,T3,T4}"/>.
        /// The roots are initialized with new instances of their respective type (obtained by calling the constructor that accepts a ObservableDomain).
        /// </summary>
        /// <param name="monitor">The monitor used to log the construction of this domain. Cannot be null.</param>
        /// <param name="domainName">Name of the domain. Must not be null but can be empty.</param>
        /// <param name="client">The observable client (head of the Chain of Responsibility) to use. Can be null.</param>
        /// <param name="serviceProvider">The service providers that will be used to resolve the <see cref="ObservableDomainSidekick"/> objects.</param>
        /// <param name="postActionsMarshaller">Optional marshaller for post actions execution.</param>
        public ObservableDomain( IActivityMonitor monitor,
                                 string domainName,
                                 IObservableDomainClient? client,
                                 IServiceProvider? serviceProvider = null,
                                 IPostActionContextMarshaller? postActionsMarshaller = null )
            : base( monitor, domainName, client, serviceProvider, postActionsMarshaller )
        {
            if( AllRoots.Count != 0 ) BindRoots();
            else using( var initialization = new InitializationTransaction( monitor, this ) )
                {
                    Root1 = AddRoot<T1>( initialization );
                    Root2 = AddRoot<T2>( initialization );
                    Root3 = AddRoot<T3>( initialization );
                    Root4 = AddRoot<T4>( initialization );
                }
        }

        /// <summary>
        /// Initializes a previously <see cref="ObservableDomain.Save"/>d domain.
        /// </summary>
        /// <param name="monitor">The monitor used to log the construction of this domain. Cannot be null.</param>
        /// <param name="domainName">Name of the domain. Must not be null but can be empty.</param>
        /// <param name="client">The observable client (head of the Chain of Responsibility) to use. Can be null.</param>
        /// <param name="s">The input stream.</param>
        /// <param name="leaveOpen">True to leave the stream opened.</param>
        /// <param name="encoding">Optional encoding for characters. Defaults to UTF-8.</param>
        /// <param name="serviceProvider">The service providers that will be used to resolve the <see cref="ObservableDomainSidekick"/> objects.</param>
        /// <param name="loadHook">The load hook to apply. See loadHook parameter of <see cref="ObservableDomain.Load(IActivityMonitor, Stream, bool, Encoding?, int, Func{ObservableDomain, bool}?)"/>.</param>
        /// <param name="postActionsMarshaller">Optional marshaller for post actions execution.</param>
        public ObservableDomain( IActivityMonitor monitor,
                                 string domainName,
                                 IObservableDomainClient client,
                                 Stream s,
                                 bool leaveOpen = false,
                                 Encoding encoding = null,
                                 IServiceProvider? serviceProvider = null,
                                 Func<ObservableDomain, bool>? loadHook = null,
                                 IPostActionContextMarshaller? postActionsMarshaller = null )
            : base( monitor, domainName, client, s, leaveOpen, encoding, serviceProvider, loadHook, postActionsMarshaller )
        {
            BindRoots();
        }

        /// <summary>
        /// Gets the first typed root object.
        /// </summary>
        public T1 Root1 { get; private set; }

        /// <summary>
        /// Gets the second typed root object.
        /// </summary>
        public T2 Root2 { get; private set; }

        /// <summary>
        /// Gets the third typed root object.
        /// </summary>
        public T3 Root3 { get; private set; }

        /// <summary>
        /// Gets the fourth typed root object.
        /// </summary>
        public T4 Root4 { get; private set; }

        /// <summary>
        /// Overridden to bind our typed roots.
        /// </summary>
        protected internal override void OnLoaded() => BindRoots();

        void BindRoots()
        {
            if( AllRoots.Count != 4
                || !(AllRoots[0] is T1)
                || !(AllRoots[1] is T2)
                || !(AllRoots[2] is T3)
                || !(AllRoots[3] is T4) )
            {
                throw new InvalidDataException( $"Incompatible stream. No root of type {typeof( T1 ).Name}, {typeof( T2 ).Name} , {typeof( T3 ).Name} and {typeof( T4 ).Name}. {AllRoots.Count} roots of type: {AllRoots.Select( t => t.GetType().Name ).Concatenate()}." );
            }
            Root1 = (T1)AllRoots[0];
            Root2 = (T2)AllRoots[1];
            Root3 = (T3)AllRoots[2];
            Root4 = (T4)AllRoots[3];
        }

    }
}
