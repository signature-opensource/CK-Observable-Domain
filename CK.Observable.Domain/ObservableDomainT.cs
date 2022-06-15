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
    /// <see cref="ObservableDomain"/> with a strongly typed <see cref="Root"/>.
    /// </summary>
    /// <typeparam name="T">Type of the root object.</typeparam>
    public sealed class ObservableDomain<T> : ObservableDomain, IObservableDomain<T>
        where T : ObservableRootObject
    {
        /// <summary>
        /// Initializes a new <see cref="ObservableDomain{T}"/> without any <see cref="ObservableDomain.DomainClient"/>.
        /// The <see cref="Root"/> is a new <typeparamref name="T"/>.
        /// <para>
        /// Sidekicks are NOT instantiated by the constructors. If <see cref="HasWaitingSidekicks"/> is true, a null transaction
        /// can be done that will instantiate the required sidekicks (and initialize them with the <see cref="ISidekickClientObject{TSidekick}"/> objects
        /// if any).
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor used to log the construction of this domain. Cannot be null.</param>
        /// <param name="domainName">Name of the domain. Must not be null but can be empty.</param>
        /// <param name="startTimer">Whether to initially start the <see cref="TimeManager"/>.</param>
        /// <param name="serviceProvider">The service providers that will be used to resolve the <see cref="ObservableDomainSidekick"/> objects.</param>
        public ObservableDomain( IActivityMonitor monitor, string domainName, bool startTimer, IServiceProvider? serviceProvider = null )
            : this( monitor, domainName, startTimer, null, serviceProvider )
        {
        }

        /// <summary>
        /// Initializes a new <see cref="ObservableDomain{T}"/>.
        /// The <see cref="Root"/> is a new <typeparamref name="T"/>.
        /// <para>
        /// Sidekicks are NOT instantiated by the constructors. If <see cref="HasWaitingSidekicks"/> is true, a null transaction
        /// can be done that will instantiate the required sidekicks (and initialize them with the <see cref="ISidekickClientObject{TSidekick}"/> objects
        /// if any).
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor used to log the construction of this domain. Cannot be null.</param>
        /// <param name="domainName">Name of the domain. Must not be null but can be empty.</param>
        /// <param name="startTimer">Whether to initially start the <see cref="TimeManager"/>.</param>
        /// <param name="client">The observable client (head of the Chain of Responsibility) to use. Can be null.</param>
        /// <param name="serviceProvider">The service providers that will be used to resolve the <see cref="ObservableDomainSidekick"/> objects.</param>
        public ObservableDomain( IActivityMonitor monitor,
                                 string domainName,
                                 bool startTimer,
                                 IObservableDomainClient? client,
                                 IServiceProvider? serviceProvider = null )
            : base( monitor, domainName, startTimer, client, serviceProvider )
        {
            // If we have been deserialized by the client.OnDomainCreated, we have nothing to do.
            // Otherwise (no OnDomainCreated load) we must create and add our Root.
            //
            // This pattern is the same for the other ObservableDomain<T1, T2...> generics.
            //
            if( AllRoots.Count == 0 )
            {
                Debug.Assert( _transactionStatus == CurrentTransactionStatus.Instantiating );
                using( var initialization = new InitializationTransaction( monitor, this, true ) )
                {
                    Root = CreateAndAddRoot<T>( initialization );
                }
            }
            Debug.Assert( Root == AllRoots[0], "Binding has been done." );
            _transactionStatus = CurrentTransactionStatus.Regular;
            monitor.Info( $"ObservableDomain<{typeof(T)}> '{domainName}' created." );
        }

        /// <summary>
        /// Initializes a previously <see cref="ObservableDomain.Save"/>d domain.
        /// <para>
        /// Sidekicks are NOT instantiated by the constructors. If <see cref="HasWaitingSidekicks"/> is true, a null transaction
        /// can be done that will instantiate the required sidekicks (and initialize them with the <see cref="ISidekickClientObject{TSidekick}"/> objects
        /// if any).
        /// </para>
        /// </summary>
        /// <param name="monitor">The monitor used to log the construction of this domain.</param>
        /// <param name="domainName">Name of the domain. Must not be null but can be empty.</param>
        /// <param name="client">The observable client (head of the Chain of Responsibility) to use.</param>
        /// <param name="stream">The input stream.</param>
        /// <param name="serviceProvider">The service providers that will be used to resolve the <see cref="ObservableDomainSidekick"/> objects.</param>
        /// <param name="startTimer">
        /// Ensures that the <see cref="ObservableDomain.TimeManager"/> is running or stopped.
        /// When null, it keeps its restored state.
        /// </param>
        public ObservableDomain( IActivityMonitor monitor,
                                 string domainName,
                                 IObservableDomainClient? client,
                                 RewindableStream stream,
                                 IServiceProvider? serviceProvider = null,
                                 bool? startTimer = null )
            : base( monitor, domainName, client, stream, serviceProvider, startTimer )
        {
            Debug.Assert( _transactionStatus == CurrentTransactionStatus.Regular );
            Debug.Assert( Root == AllRoots[0], "Binding has been done." );
        }

        /// <summary>
        /// Gets the typed root object.
        /// </summary>
        public T Root { get; private set; }

        private protected override void BindRoots()
        {
            if( AllRoots.Count != 1 || !(AllRoots[0] is T) )
            {
                Throw.InvalidDataException( $"Incompatible stream. Expected single root of type {typeof( T )}. {AllRoots.Count} roots of type: {AllRoots.Select( t => t.GetType().Name ).Concatenate()}." );
            }
            Root = (T)AllRoots[0];
        }

    }
}
