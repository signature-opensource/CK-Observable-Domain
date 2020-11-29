using CK.Core;
using CK.Text;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace CK.Observable
{
    /// <summary>
    /// <see cref="ObservableDomain"/> with strongly typed <see cref="Root1"/> and <see cref="Root2"/>
    /// observable roots.
    /// </summary>
    /// <typeparam name="T1">Type of the first root object.</typeparam>
    /// <typeparam name="T2">Type of the second root object.</typeparam>
    public sealed class ObservableDomain<T1, T2> : ObservableDomain, IObservableDomain<T1, T2>
        where T1 : ObservableRootObject
        where T2 : ObservableRootObject
    {
        /// <summary>
        /// Initializes a new <see cref="ObservableDomain{T1,T2}"/> without any <see cref="ObservableDomain.DomainClient"/>.
        /// The <see cref="Root1"/> is a new <typeparamref name="T1"/> and the <see cref="Root2"/> is a new <typeparamref name="T2"/>
        /// (obtained by calling the constructor that accepts a ObservableDomain).
        /// </summary>
        /// <param name="monitor">The monitor used to log the construction of this domain. Cannot be null.</param>
        /// <param name="domainName">Name of the domain. Must not be null but can be empty.</param>
        /// <param name="startTimer">Whether to initially start the <see cref="TimeManager"/>.</param>
        /// <param name="serviceProvider">The service providers that will be used to resolve the <see cref="ObservableDomainSidekick"/> objects.</param>
        public ObservableDomain( IActivityMonitor monitor, string domainName, bool startTimer, IServiceProvider? serviceProvider = null )
            : this(monitor, domainName, startTimer, null, serviceProvider)
        {
        }

        /// <summary>
        /// Initializes a new <see cref="ObservableDomain{T1,T2}"/>.
        /// The <see cref="Root1"/> is a new <typeparamref name="T1"/> and the <see cref="Root2"/> is a new <typeparamref name="T2"/>
        /// (obtained by calling the constructor that accepts a ObservableDomain).
        /// </summary>
        /// <param name="monitor">The monitor used to log the construction of this domain. Cannot be null.</param>
        /// <param name="domainName">Name of the domain. Must not be null but can be empty.</param>
        /// <param name="startTimer">Whether to initially start the <see cref="TimeManager"/>.</param>
        /// <param name="client">The observable client (head of the Chain of Responsibility) to use. Can be null.</param>
        /// <param name="serviceProvider">The service providers that will be used to resolve the <see cref="ObservableDomainSidekick"/> objects.</param>
        public ObservableDomain( IActivityMonitor monitor, string domainName,
                                 bool startTimer,
                                 IObservableDomainClient? client,
                                 IServiceProvider? serviceProvider = null )
            : base( monitor, domainName, startTimer, client, serviceProvider )
        {
            if( AllRoots.Count != 0 ) BindRoots();
            else using( var initialization = new InitializationTransaction( monitor, this ) )
                {
                    Root1 = AddRoot<T1>( initialization );
                    Root2 = AddRoot<T2>( initialization );
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
        /// <param name="startTimer">
        /// Ensures that the <see cref="ObservableDomain.TimeManager"/> is running or stopped.
        /// When null, it keeps its previous state (it is initially stopped at domain creation) and then its current state is persisted.
        /// </param>
        public ObservableDomain( IActivityMonitor monitor,
                                 string domainName,
                                 IObservableDomainClient client,
                                 Stream stream,
                                 bool leaveOpen = false,
                                 Encoding encoding = null,
                                 IServiceProvider? serviceProvider = null,
                                 bool? startTimer = null )
            : base( monitor, domainName, client, stream, leaveOpen, encoding, serviceProvider, startTimer )
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
        /// Overridden to bind our typed roots.
        /// </summary>
        protected internal override void OnLoaded() => BindRoots();

        void BindRoots()
        {
            if( AllRoots.Count != 2
                || !(AllRoots[0] is T1)
                || !(AllRoots[1] is T2) )
            {
                throw new InvalidDataException( $"Incompatible stream. No root of type {typeof( T1 ).Name} and {typeof( T2 ).Name}. {AllRoots.Count} roots of type: {AllRoots.Select( t => t.GetType().Name ).Concatenate()}." );
            }
            Root1 = (T1)AllRoots[0];
            Root2 = (T2)AllRoots[1];
        }
    }
}
