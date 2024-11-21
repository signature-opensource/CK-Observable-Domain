using CK.AppIdentity;
using CK.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Observable.League;

/// <summary>
/// Default league singleton service.
/// </summary>
public class DefaultObservableLeague : IObservableLeague, IHostedService, ISingletonAutoService
{
    ObservableLeague? _default;
    readonly DirectoryStreamStore _store;
    readonly IDefaultObservableDomainInitializer? _initializer;
    readonly IServiceProvider _serviceProvider;
    readonly ApplicationIdentityService? _identityService;
    readonly IActivityMonitor _monitor;
    readonly DefaultObservableLeagueOptions _options;

    public DefaultObservableLeague( IServiceProvider serviceProvider,
                                    IOptions<DefaultObservableLeagueOptions> options,
                                    ApplicationIdentityService? identityService = null,
                                    IDefaultObservableDomainInitializer? initializer = null )
    {
        _initializer = initializer;
        _serviceProvider = serviceProvider;
        _identityService = identityService;
        _options = options.Value;
        _monitor = new ActivityMonitor( nameof( DefaultObservableLeague ) );

        NormalizedPath p = AppContext.BaseDirectory;
        p = p.Combine( new NormalizedPath( options.Value.StorePath ?? string.Empty ) ).ResolveDots().AppendPart( nameof( DefaultObservableLeague ) );
        _monitor.Info( $"DirectoryStreamStore path is '{p}'." );
        _store = new DirectoryStreamStore( p );
    }

    /// <inheritdoc />
    public IObservableDomainLoader? this[string domainName]
    {
        get
        {
            CheckLoadedDomain();
            return _default.Find( domainName );
        }
    }

    /// <inheritdoc />
    public IObservableDomainAccess<OCoordinatorRoot> Coordinator
    {
        get
        {
            CheckLoadedDomain();
            return _default.Coordinator;
        }
    }

    /// <inheritdoc />
    public IObservableDomainLoader? Find( string domainName )
    {
        CheckLoadedDomain();
        return _default.Find( domainName );
    }

    async Task IHostedService.StartAsync( CancellationToken cancellationToken )
    {
        using( _monitor.OpenInfo( "Loading DefaultObservableLeague." ) )
        {
            if( _identityService == null || _identityService.InitializationTask.IsCompleted )
            {
                await CreateDefaultAsync( _monitor ).ConfigureAwait( false );
            }
            else if( _identityService != null )
            {
                _monitor.Warn( "Waiting for AppIdentityService initialization to initialize the League." );
            }
        }
    }

    async Task CreateDefaultAsync( IActivityMonitor monitor )
    {
        var d = await ObservableLeague.LoadAsync( monitor, _store, _initializer, _serviceProvider ).ConfigureAwait( false );
        if( d != null )
        {
            await d.ApplyEnsureDomainOptionsAsync( monitor, _options.EnsureDomains ).ConfigureAwait( false );
        }
        Volatile.Write( ref _default, d );
    }

    async Task IHostedService.StopAsync( CancellationToken cancel )
    {
        if( _default != null )
        {
            await _default.CloseAsync( _monitor ).ConfigureAwait( false );
        }
        _monitor.MonitorEnd();
    }

    [MemberNotNull( nameof( _default ) )]
    void CheckLoadedDomain()
    {
        if( _default == null )
        {
            if( _identityService == null )
            {
                throw new InvalidOperationException( "The DefaultObservableLeague is not loaded. " +
                                                     "Did the DefaultObservableLeague service start?" );
            }
            // Highly questionnable part (to say the least)...
            // ...until the league is managed by a AppIdentity feature!.
            _identityService.Heartbeat.Async += Heartbeat_Async;
            try
            {
                int totalMS = 0;
                while( Volatile.Read( ref _default ) == null )
                {
                    Thread.Sleep( 100 );
                    totalMS += 100;
                    if( totalMS > 1000000 )
                    {
                        throw new InvalidOperationException( "The ApplicationIdentityService is not started." );
                    }
                }
            }
            finally
            {
                _identityService.Heartbeat.Async -= Heartbeat_Async;
            }
        }
    }

    Task Heartbeat_Async( IActivityMonitor monitor, int e, CancellationToken cancel )
    {
        if( Volatile.Read( ref _default ) != null )
        {
            _identityService!.Heartbeat.Async -= Heartbeat_Async;
        }
        else
        {
            if( _identityService!.InitializationTask.IsCompleted )
            {
                monitor.Info( $"AppIdentity available at heartbeat n°{e}." );
                return CreateDefaultAsync( monitor );
            }
            monitor.Info( $"AppIdentity not yet available (heartbeat n°{e})." );
        }
        return Task.CompletedTask;
    }
}
