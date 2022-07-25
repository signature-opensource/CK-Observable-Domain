using CK.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace CK.Observable.ServerSample
{
    public class Program
    {
        public static Task Main( string[] args )
        {
            var m = new ActivityMonitor( "App Startup" );

            var host = new HostBuilder()
                .UseContentRoot( Directory.GetCurrentDirectory() )
                .ConfigureHostConfiguration( config =>
                {
                    config.AddEnvironmentVariables( prefix: "DOTNET_" );
                    config.AddCommandLine( args );
                } )
                .ConfigureAppConfiguration( ( hostingContext, config ) =>
                {
                    config.AddJsonFile( "appsettings.json", optional: true, reloadOnChange: true )
                          .AddJsonFile( $"appsettings.{hostingContext.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true );

                    // Configuration coming from the environment variables are considered safer than appsettings configuration.
                    // We add them after the appsettings.
                    config.AddEnvironmentVariables();

                    // Finally comes the configuration values from the command line: these configurations override
                    // any previous ones.
                    config.AddCommandLine( args );
                } )
                .UseCKMonitoring()
                .ConfigureServices( ( context, s ) =>
                {
                    s.AddStObjMap( m, Assembly.GetExecutingAssembly() );
                } ).Build();

            return host.RunAsync();
        }
    }
}
