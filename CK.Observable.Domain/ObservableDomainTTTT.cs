using CK.BinarySerialization;
using CK.Core;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;

namespace CK.Observable;

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
    /// The roots are initialized with new instances of their respective type.
    /// <para>
    /// Sidekicks are NOT instantiated by the constructors. If <see cref="ObservableDomain.HasWaitingSidekicks"/> is true, a null transaction
    /// can be done that will instantiate the required sidekicks (and initialize them with the <see cref="ISidekickClientObject{TSidekick}"/> objects
    /// if any).
    /// </para>
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
    /// Initializes a new <see cref="ObservableDomain{T1,T2,T3,T4}"/>.
    /// The roots are initialized with new instances of their respective type.
    /// <para>
    /// Sidekicks are NOT instantiated by the constructors. If <see cref="ObservableDomain.HasWaitingSidekicks"/> is true, a null transaction
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
        : base(monitor, domainName, startTimer, client, serviceProvider )
    {
        if( AllRoots.Count == 0 )
        {
            using( var initialization = new InitializationTransaction( monitor, this, true ) )
            {
                Root1 = CreateAndAddRoot<T1>( initialization );
                Root2 = CreateAndAddRoot<T2>( initialization );
                Root3 = CreateAndAddRoot<T3>( initialization );
                Root4 = CreateAndAddRoot<T4>( initialization );
            }
        }
        Debug.Assert( Root1 == AllRoots[0] && Root2 == AllRoots[1] && Root3 == AllRoots[2] && Root4 == AllRoots[3], "Binding has been done." );
        _transactionStatus = CurrentTransactionStatus.Regular;
        monitor.Info( $"ObservableDomain<{typeof( T1 )}, {typeof( T2 )}, {typeof( T3 )}, {typeof( T4 )}> '{domainName}' created." );
    }

    /// <summary>
    /// Initializes a previously <see cref="ObservableDomain.Save"/>d domain.
    /// <para>
    /// Sidekicks are NOT instantiated by the constructors. If <see cref="ObservableDomain.HasWaitingSidekicks"/> is true, a null transaction
    /// can be done that will instantiate the required sidekicks (and initialize them with the <see cref="ISidekickClientObject{TSidekick}"/> objects
    /// if any).
    /// </para>
    /// </summary>
    /// <param name="monitor">The monitor used to log the construction of this domain.</param>
    /// <param name="domainName">Name of the domain. Must not be null but can be empty.</param>
    /// <param name="client">The observable client (head of the Chain of Responsibility) to use.</param>
    /// <param name="s">The input stream.</param>
    /// <param name="serviceProvider">The service providers that will be used to resolve the <see cref="ObservableDomainSidekick"/> objects.</param>
    /// <param name="startTimer">
    /// Ensures that the <see cref="ObservableDomain.TimeManager"/> is running or stopped.
    /// When null, it keeps its restored state.
    /// </param>
    public ObservableDomain( IActivityMonitor monitor,
                             string domainName,
                             IObservableDomainClient client,
                             RewindableStream s,
                             IServiceProvider? serviceProvider = null,
                             bool? startTimer = null )
        : base( monitor, domainName, client, s, serviceProvider, startTimer )
    {
        Debug.Assert( _transactionStatus == CurrentTransactionStatus.Regular );
        Debug.Assert( Root1 == AllRoots[0] && Root2 == AllRoots[1] && Root3 == AllRoots[2] && Root4 == AllRoots[3], "Binding has been done." );
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

    private protected override void BindRoots()
    {
        if( AllRoots.Count != 4
            || !(AllRoots[0] is T1)
            || !(AllRoots[1] is T2)
            || !(AllRoots[2] is T3)
            || !(AllRoots[3] is T4) )
        {
            Throw.InvalidDataException( $"Incompatible stream. No root of type {typeof( T1 ).Name}, {typeof( T2 ).Name} , {typeof( T3 ).Name} and {typeof( T4 ).Name}. {AllRoots.Count} roots of type: {AllRoots.Select( t => t.GetType().Name ).Concatenate()}." );
        }
        Root1 = (T1)AllRoots[0];
        Root2 = (T2)AllRoots[1];
        Root3 = (T3)AllRoots[2];
        Root4 = (T4)AllRoots[3];
    }

}
