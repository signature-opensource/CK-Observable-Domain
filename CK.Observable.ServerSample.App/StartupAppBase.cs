using System.Reflection;
using CK.Core;
using CK.Observable;
using CK.Observable.League;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CK.WCS
{
    public class StartupAppBase<T> where T : ObservableRootObject
    {
        private readonly IConfiguration _configuration;
        private readonly IActivityMonitor _startupMonitor;

        public StartupAppBase( IConfiguration configuration, IHostEnvironment env )
        {
            _startupMonitor = new ActivityMonitor();
            _configuration = configuration;
        }

        public IConfiguration Configuration => _configuration;

        public virtual void ConfigureServices( IServiceCollection services )
        {
            // TODO: Apply the following lines when DefaultObservableLeagueOptions exposes Domains property.
            //services.Configure<DefaultObservableLeagueOptions>( "CK-ObservableLeague", o =>
            //{
            //    o.Domains.TryAdd( "Agent", typeof( T ).GetType() );
            //} );

            var startupServices = new SimpleServiceContainer();
            startupServices.Add( (IConfigurationRoot)_configuration );
            startupServices.Add( _configuration );

            services.AddSingleton( (IConfigurationRoot)_configuration )
                    .AddStObjMap( _startupMonitor, Assembly.GetEntryAssembly()!, startupServices );

        }
    }
}
