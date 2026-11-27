using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CK.BinarySerialization;
using CK.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shouldly;

namespace CK.Observable.PersistentDomainDriverTests;

/// <summary>A service that a sidekick needs. The domain gets it from its service provider.</summary>
public sealed class SidekickDependency
{
}

/// <summary>A sidekick with a service in its constructor: the domain resolves it at each instantiation.</summary>
public sealed class ServiceSidekick : ObservableDomainSidekick
{
    public ServiceSidekick( IActivityMonitor monitor, IObservableDomainSidekickManager manager, SidekickDependency dependency )
        : base( manager )
    {
        Throw.CheckNotNullArgument( dependency );
    }

    protected override void RegisterClientObject( IActivityMonitor monitor, IDestroyable o )
    {
    }

    protected override bool ExecuteCommand( IActivityMonitor monitor, in SidekickCommand command ) => false;

    protected override void OnUnload( IActivityMonitor monitor )
    {
    }
}

/// <summary>An object that uses <see cref="ServiceSidekick"/>.</summary>
[SerializationVersion( 0 )]
[UseSidekick( typeof( ServiceSidekick ) )]
public sealed class OSidekickUser : ObservableObject
{
    public OSidekickUser()
    {
    }

    OSidekickUser( IBinaryDeserializer d, ITypeReadInfo info )
        : base( Sliced.Instance )
    {
    }

    public static void Write( IBinarySerializer s, in OSidekickUser o )
    {
    }
}

/// <summary>Creates one <see cref="OSidekickUser"/> in the initial transaction: its sidekick exists before the start scope ends.</summary>
public sealed class SidekickUserConfigurator : IObservableDomainConfigurator<TestDriver>
{
    public bool ConfigureDomain( IActivityMonitor monitor, ObservableDomain observableDomain )
    {
        if( !observableDomain.AllObjects.Items.OfType<OSidekickUser>().Any() ) new OSidekickUser();
        return true;
    }
}

[TestFixture]
public class RootServiceProviderTests
{
    // R1: the generated host trigger resolves the parameters of OnHostStartAsync from a scope, and disposes the scope
    // at the end of the start. The domain must not keep this scope: a rollback instantiates the sidekicks again.
    [Test]
    public async Task after_the_start_scope_is_disposed_a_rollback_still_resolves_the_sidekick_services()
    {
        var monitor = new ActivityMonitor();
        var store = TestStore.NewDirectory();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection( new Dictionary<string, string?>
        {
            ["CK-ObservableDomain:Domains:TestDomain:StorePath"] = store,
        } ).Build();
        var start = typeof( PersistentDomainDriver<TestDriver> ).GetMethod( "OnHostStartAsync", BindingFlags.Instance | BindingFlags.NonPublic );
        start.ShouldNotBeNull();
        var parameters = start.GetParameters();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>( configuration );
        services.AddSingleton<SidekickDependency>();
        services.AddSingleton<IObservableDomainConfigurator<TestDriver>, SidekickUserConfigurator>();
        // The engine registers the singleton auto services. This does the same for the parameters of OnHostStartAsync.
        foreach( var p in parameters )
        {
            if( p.ParameterType.IsClass && typeof( ISingletonAutoService ).IsAssignableFrom( p.ParameterType ) )
            {
                services.AddSingleton( p.ParameterType );
            }
        }
        using var root = services.BuildServiceProvider();

        var driver = new TestDriver();
        // The same steps as the generated HostedServiceLifetimeTrigger.StartAsync: resolve each parameter from a scope,
        // call the method, then dispose the scope.
        using( var scope = root.CreateScope() )
        {
            var args = parameters.Select( p => p.ParameterType == typeof( IActivityMonitor )
                                                ? monitor
                                                : scope.ServiceProvider.GetRequiredService( p.ParameterType ) ).ToArray();
            await (Task)start.Invoke( driver, args )!;
        }
        try
        {
            driver.Domain.Read( monitor, () => driver.Domain.AllObjects.Items.OfType<OSidekickUser>().Count() ).ShouldBe( 1 );

            // The transaction fails: the client restores the snapshot, and the domain instantiates the sidekick again.
            var failed = await driver.Domain.ModifyAsync( monitor, () => throw new InvalidOperationException( "The transaction fails (test)." ),
                                                          throwException: false,
                                                          millisecondsTimeout: -1,
                                                          considerRolledbackAsFailure: false,
                                                          parallelDomainPostActions: true,
                                                          waitForDomainPostActionsCompletion: true );
            failed.ClientError.ShouldBeNull( "The rollback must instantiate the sidekick with the services of the host." );

            // A next transaction works and the state is the restored one.
            await driver.Domain.ModifyThrowAsync( monitor, () => new OCounter { Count = 1 }, waitForDomainPostActionsCompletion: true );
            driver.Domain.Read( monitor, () => driver.Domain.AllObjects.Items.OfType<OSidekickUser>().Count() ).ShouldBe( 1 );
        }
        finally
        {
            await driver.DoHostStopAsync( monitor );
            driver.Domain.Dispose();
        }
    }
}
