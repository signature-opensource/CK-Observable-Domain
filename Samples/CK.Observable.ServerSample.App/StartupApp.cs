using CK.Observable.League;
using CK.WCS;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;

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
                var domainOpts = new EnsureDomainOptions { DomainName = "Test-Domain" };
                domainOpts.CreateSnapshotSaveDelay = TimeSpan.FromSeconds( 10 );
                domainOpts.RootTypes.Add( typeof( Root ).AssemblyQualifiedName! );
                c.EnsureDomains.Add( domainOpts );
            } );
        }
    }
}
