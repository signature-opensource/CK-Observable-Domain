using CK.Core;
using CK.MQTT.Server;
using CK.Observable.League;
using CK.Observable.ServerSample.App;
using CK.Observable.SignalRWatcher;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CK.AppIdentity;

namespace CK.Observable.ServerSample
{
    public static class Program
    {
        public static async Task Main( string[] args )
        {
            var host = WebApplication.CreateBuilder( args );
            var monitor = host.Host.GetBuilderMonitor();
            host.Services.RemoveAll<ILoggerProvider>();
            host.AddScopedHttpContext();
            host.Host.UseCKAppIdentity();
            host.Host.UseCKMonitoring();

            host.Services.Configure<MQTTDemiServerConfig>( host.Configuration.GetSection( "MQTTDemiServerConfig" ) );

            host.Services.Configure<DefaultObservableLeagueOptions>( c =>
            {
                c.StorePath = host.Configuration["CK-ObservableLeague:StorePath"];
                var domainOpts = new EnsureDomainOptions
                {
                    DomainName = "Test-Domain",
                    CreateSnapshotSaveDelay = TimeSpan.FromSeconds( 10 ),
                    CreateLifeCycleOption = DomainLifeCycleOption.Always,
                    RootTypes = { typeof( Root ).AssemblyQualifiedName! }
                };
                c.EnsureDomains.Add( domainOpts );
            } );
            host.Services.AddAuthentication().AddWebFrontAuth();
            host.Services.AddCors
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
            host.Services.AddSignalR();

            host.Services.AddStObjMap( monitor, Assembly.GetEntryAssembly()! );

            var app = host.Build();
            app.UseGuardRequestMonitor();
            app.UseScopedHttpContext();
            app.UseCors();
            app.UseCris();
            app.UseRouting();
            app.UseEndpoints( endpoints =>
            {
                endpoints.MapHub<ObservableAppHub>( "/hub/league" );
            } );

            app.UseDefaultFiles();
            app.UseStaticFiles();

            await app.RunAsync().ConfigureAwait( false );
        }
    }
}
