using CK.Observable.League;
using CK.WCS;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CK.Observable.ServerSample.App
{
    public class StartupApp : StartupAppBase<Root>
    {
        public StartupApp( IConfiguration configuration, IHostEnvironment env )
            : base( configuration, env )
        {
        }

        public override void ConfigureServices( IServiceCollection services )
        {
            base.ConfigureServices( services );

            services.Configure<DefaultObservableLeagueOptions>( c =>
            {
                c.StorePath = Configuration[ "CK-ObservableLeague:StorePath" ];
                var domainOpts = new EnsureDomainOptions { DomainName = "Agent" };
                domainOpts.RootTypes.Add( typeof( OAgentRoot ).AssemblyQualifiedName! );
                c.EnsureDomains.Add( domainOpts );
            } );
        }
    }
}
