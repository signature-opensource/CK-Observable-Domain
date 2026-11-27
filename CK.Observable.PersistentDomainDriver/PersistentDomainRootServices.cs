using System;
using CK.Core;

namespace CK.Observable;

/// <summary>
/// Gives the root service provider of the host to <see cref="PersistentDomainDriver{TSelf}"/>.
/// <para>
/// The generated host calls OnHostStartAsync with services from a scope, and it disposes this scope at the end
/// of the start. A domain keeps its service provider for its life: a rollback instantiates the sidekicks again
/// with it. So the domain must not get the scope. This class is a singleton: the container gives the root provider
/// to its constructor.
/// </para>
/// <para>
/// Because the domain uses the root provider, a service that a sidekick gets in its constructor must be a singleton.
/// A scoped service from the root provider is never disposed, and the container can refuse it.
/// </para>
/// </summary>
public sealed class PersistentDomainRootServices : ISingletonAutoService
{
    /// <summary>
    /// Initializes a new <see cref="PersistentDomainRootServices"/>.
    /// </summary>
    /// <param name="services">The root service provider of the host.</param>
    public PersistentDomainRootServices( IServiceProvider services )
    {
        Throw.CheckNotNullArgument( services );
        Services = services;
    }

    /// <summary>
    /// Gets the root service provider of the host.
    /// </summary>
    public IServiceProvider Services { get; }
}
