using CK.Core;
using CK.Observable.ServerSample.App;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace CK.Observable.ServerSample
{
    public class Program
    {
        public static async Task Main( string[] args )
        {
            // Set default ActivityMonitor filter
            ActivityMonitor.DefaultFilter = LogFilter.Debug;
            IHost host;

            // Note that GrandOutput initialization is NOT EFFECTIVE until host.RunAsync() below.
            // This monitor will NOT be logged.
            ActivityMonitor m = new ActivityMonitor();
            m.Output.RegisterClient( new ActivityMonitorConsoleClient() );
            m.Info( "Preparing host." );
            try
            {
                // Run as Console from either Debugger is attached or --console is passed
                var isService = !(Debugger.IsAttached || args.Contains( "--console" ));
                var builder = CreateHostBuilder( isService, args.Where( arg => arg != "--console" ).ToArray() );

                Assembly callingAssembly = Assembly.GetExecutingAssembly();

                m.Info( $"Running as console. Using default content root: {Environment.CurrentDirectory}" );

                builder.ConfigureAppConfiguration( ( context, configuration ) =>
                {
                    configuration
                        .AddJsonFile( "appsettings.json",
                            optional: false, reloadOnChange: true )
                        .AddJsonFile( $"appsettings.{context.HostingEnvironment.EnvironmentName}.json",
                            optional: true, reloadOnChange: true )
                        .AddJsonFile( "appsettings.local.json",
                            optional: true, reloadOnChange: true )
                        .AddJsonFile(
                            Environment.ExpandEnvironmentVariables( "%AppProgramData%/appsettings.local.json" ),
                            optional: true, reloadOnChange: true );
                } );

                m.Info( "Building host..." );
                host = builder.Build();
            }
            catch( Exception ex )
            {
                m.Fatal( ex );
                throw;
            }

            m.MonitorEnd( "Host preparation complete. Starting up..." );
            await host.RunAsync().ConfigureAwait( false );
        }
        static IHostBuilder CreateHostBuilder( bool isService, string[] args )
        {
            var host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder( args );
            host = host
                .UseCKMonitoring()
                .ConfigureWebHostDefaults( webBuilder =>
                {
                    webBuilder
                        .UseKestrel()
                    .UseIISIntegration()
                        .UseStartup<Startup>();
                } );

            return host;
        }
    }
}
