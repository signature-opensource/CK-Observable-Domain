using CK.MQTT;
using CK.MQTT.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using CK.Observable.DefaultMQTTObservableServer;
using CK.Observable.MQTTWatcher;
using CK.Observable.SignalRWatcher;

namespace CK.Observable.ServerSample.App
{
    public class Startup : StartupApp
    {
        private readonly IWebHostEnvironment _env;

        public Startup( IConfiguration configuration, IWebHostEnvironment env )
            : base( configuration, env )
        {
            _env = env;
        }

        public override void ConfigureServices( IServiceCollection services )
        {
            services.Configure<MQTTDemiServerConfig>( Configuration.GetSection( "MQTTDemiServerConfig" ) );
            base.ConfigureServices( services );
            services.AddAuthentication()
                .AddWebFrontAuth();
            services.AddCors
            (
                o =>
                {
                    o.AddDefaultPolicy
                    (
                        builder =>
                        {
                            builder
                               .AllowAnyMethod()
                               .AllowAnyHeader()
                               .AllowCredentials()
                               .SetIsOriginAllowed( _ => true );
                        }
                    );
                }
            );
            services.AddSignalR();
        }

        public void Configure( IApplicationBuilder app )
        {
            if( _env.IsDevelopment() )
            {
                app.UseDeveloperExceptionPage();
            }
            app.UseCors();
            app.UseCris();
            app.UseAuthorization();
            app.UseRouting();
            app.UseEndpoints( endpoints =>
            {
                endpoints.MapHub<ObservableAppHub>( "/hub/league" );
            } );

            app.UseDefaultFiles()
               .UseStaticFiles();
        }
    }
}
