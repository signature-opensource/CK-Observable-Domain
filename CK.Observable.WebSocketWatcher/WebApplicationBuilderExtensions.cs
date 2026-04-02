using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace CK.Observable.WebSocketWatcher;

/// <summary>
/// Extension methods for registering and mapping the observable domain WebSocket watcher.
/// </summary>
public static class WebApplicationBuilderExtensions
{
    /// <summary>
    /// Registers the SimpleR services required by the observable domain watcher.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    /// <returns>The <paramref name="builder"/> for chaining.</returns>
    public static WebApplicationBuilder AddObservableDomainWatching( this WebApplicationBuilder builder )
    {
        builder.Services.AddSimpleR();
        return builder;
    }

    /// <summary>
    /// Maps the <c>/ws/observable</c> WebSocket endpoint using the <see cref="ObservableDomainWatcherDispatcher"/>.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The <paramref name="app"/> for chaining.</returns>
    public static IApplicationBuilder UseObservableDomainWatching( this IApplicationBuilder app )
    {
        app.UseEndpoints( endpoints =>
        {
            endpoints.MapSimpleR<string, ReadOnlyMemory<byte>>( "/ws/observable", b =>
            {
                b.UseEndOfMessageDelimitedProtocol( new WatcherMessageProtocol() );
                b.UseDispatcher<ObservableDomainWatcherDispatcher>();
            } );
        } );

        return app;
    }
}
