using CK.BinarySerialization;
using CK.Core;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
            if( AllRoots.Count == 0 )
            {
                using( var initialization = new InitializationTransaction( monitor, this ) )
                {
                    Root1 = CreateAndAddRoot<T1>( initialization );
                    Root2 = CreateAndAddRoot<T2>( initialization );
                }
            }
            Debug.Assert( Root1 == AllRoots[0] && Root2 == AllRoots[1], "Binding has been done." );
            _initializingStatus = DomainInitializingStatus.None;
            monitor.Info( $"ObservableDomain<{typeof( T1 )}, {typeof( T2 )}> '{domainName}' created." );
        }

        /// <summary>
        /// Initializes a previously <see cref="ObservableDomain.Save"/>d domain.
        /// </summary>
        /// <param name="monitor">The monitor used to log the construction of this domain. Cannot be null.</param>
        /// <param name="domainName">Name of the domain. Must not be null but can be empty.</param>
        /// <param name="client">The observable client (head of the Chain of Responsibility) to use. Can be null.</param>
        /// <param name="stream">The input stream.</param>
        /// <param name="serviceProvider">The service providers that will be used to resolve the <see cref="ObservableDomainSidekick"/> objects.</param>
        /// <param name="startTimer">
        /// Ensures that the <see cref="ObservableDomain.TimeManager"/> is running or stopped.
        /// When null, it keeps its previous state (it is initially stopped at domain creation) and then its current state is persisted.
        /// </param>
        public ObservableDomain( IActivityMonitor monitor,
                                 string domainName,
                                 IObservableDomainClient client,
                                 RewindableStream stream,
                                 IServiceProvider? serviceProvider = null,
                                 bool? startTimer = null )
            : base( monitor, domainName, client, stream, serviceProvider, startTimer )
        {
            Debug.Assert( Root1 == AllRoots[0] && Root2 == AllRoots[1], "Binding has been done." );
            _initializingStatus = DomainInitializingStatus.None;
        }

        /// <summary>
        /// Gets the first typed root object.
        /// </summary>
        public T1 Root1 { get; private set; }


        /// <summary>
        /// Gets the second typed root object.
        /// </summary>
        public T2 Root2 { get; private set; }

        private protected override void BindRoots()
        {
            if( AllRoots.Count != 2
                || !(AllRoots[0] is T1)
                || !(AllRoots[1] is T2) )
            {
                Throw.InvalidDataException( $"Incompatible stream. No root of type {typeof( T1 ).Name} and {typeof( T2 ).Name}. {AllRoots.Count} roots of type: {AllRoots.Select( t => t.GetType().Name ).Concatenate()}." );
            }
            Root1 = (T1)AllRoots[0];
            Root2 = (T2)AllRoots[1];
        }
    }
}
