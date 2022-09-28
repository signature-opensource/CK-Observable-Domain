using CK.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CK.Observable.League
{
    /// <summary>
    /// Default league singleton service.
    /// </summary>
    public class DefaultObservableLeague : IObservableLeague, IHostedService, ISingletonAutoService
    {
        ObservableLeague? _default;
        readonly DirectoryStreamStore _store;
        readonly IDefaultObservableDomainInitializer? _initializer;
        readonly IServiceProvider _serviceProvider;
        readonly IActivityMonitor _monitor;
        readonly DefaultObservableLeagueOptions _options;

        public DefaultObservableLeague( IServiceProvider serviceProvider,
                                        IOptions<DefaultObservableLeagueOptions> options,
                                        IDefaultObservableDomainInitializer? initializer = null )
        {
            _initializer = initializer;
            _serviceProvider = serviceProvider;
            _options = options.Value;
            _monitor = new ActivityMonitor( nameof( DefaultObservableLeague ) );

            NormalizedPath p = AppContext.BaseDirectory;
            p = p.Combine( new NormalizedPath( options.Value.StorePath ?? string.Empty ) ).ResolveDots().AppendPart( nameof( DefaultObservableLeague ) );
            _monitor.Info( $"DirectoryStreamStore path is '{p}'." );
            _store = new DirectoryStreamStore( p );
        }

        /// <inheritdoc />
        public IObservableDomainLoader? this[string domainName] => _default!.Find( domainName );

        /// <inheritdoc />
        public IObservableDomainAccess<OCoordinatorRoot> Coordinator => _default!.Coordinator;

        /// <inheritdoc />
        public IObservableDomainLoader? Find( string domainName ) => _default!.Find( domainName );

        async Task IHostedService.StartAsync( CancellationToken cancellationToken )
        {
            using( _monitor.OpenInfo( "Loading DefaultObservableLeague." ) )
            {
                _default = await ObservableLeague.LoadAsync( _monitor, _store, _initializer, _serviceProvider );
                if( _default != null )
                {
                    await _default.ApplyEnsureDomainOptionsAsync( _monitor, _options.EnsureDomains );
                }
            }
        }

        async Task IHostedService.StopAsync( CancellationToken cancel )
        {
            await _default!.CloseAsync( _monitor );
            _monitor.MonitorEnd();
        }
    }
}
