using System;
using System.Diagnostics;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using CK.Core;
using CK.Monitoring;
using CK.Ng.ObservableDomain.WebSocket.Tests.Drivers;
using CK.Observable.WebSocketWatcher;
using CK.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NUnit.Framework;
using Shouldly;

using static CK.Testing.MonitorTestHelper;

namespace CK.Ng.ObservableDomain.WebSocket.Tests;

/// <summary>
/// Regression tests for the host-shutdown WebSocket drain bug: an open SimpleR read loop is never
/// aborted on <c>ApplicationStopping</c>, so Kestrel drains it for the whole
/// <see cref="HostOptions.ShutdownTimeout"/> and <see cref="IHost.StopAsync"/> blocks for that long.
/// </summary>
[TestFixture]
public class HostShutdownDrainTests
{
    // Short enough to keep the test fast, long enough that the drain bug (5s) is unambiguously slower than a prompt stop.
    static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds( 5 );
    // A correct stop is near-instant; below ShutdownTimeout with headroom so the bug fails clearly.
    static readonly TimeSpan PromptStop = TimeSpan.FromSeconds( 2 );

    static async Task<IStObjMap> BuildMapAsync()
    {
        // Same composition as NgObservableDomainTests, minus the sample configurators (their logs aren't asserted here).
        var targetProjectPath = TestHelper.GetTypeScriptInlineTargetProjectPath();
        var configuration = TestHelper.CreateDefaultEngineConfiguration();
        configuration.FirstBinPath.Path = TestHelper.BinFolder;
        configuration.FirstBinPath.Assemblies.AddRange( ["CK.Ng.ObservableDomain.WebSocket", "CK.Ng.Cris.AspNet"] );
        configuration.FirstBinPath.Types.Add( typeof( SampleDomainDriver ) );
        configuration.FirstBinPath.EnsureTypeScriptConfigurationAspect( targetProjectPath );
        return (await configuration.RunSuccessfullyAsync()).LoadMap();
    }

    static async Task<(WebApplication App, Uri WsUri)> StartHostAsync( IStObjMap map )
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.AddApplicationIdentityServiceConfiguration();
        builder.AddObservableDomainWatching();
        // Bound the drain so the bug surfaces as a 5s hang instead of the default 30s.
        builder.Services.Configure<HostOptions>( o => o.ShutdownTimeout = ShutdownTimeout );

        var app = builder.CKBuild( map );
        app.Urls.Add( "http://127.0.0.1:0" ); // Random free port.
        app.UseRouting();
        app.UseObservableDomainWatching();
        await app.StartAsync();

        var server = app.Services.GetRequiredService<IServer>();
        var address = server.Features.Get<IServerAddressesFeature>()!.Addresses.First();
        var wsUri = new Uri( "ws" + address.Substring( "http".Length ).TrimEnd( '/' ) + "/ws/observable" );
        return (app, wsUri);
    }

    // Connects and awaits the {"connectionId":...} reply: guarantees OnConnectedAsync ran and the server is parked in SimpleR's read loop.
    static async Task ConnectAndAwaitAckAsync( ClientWebSocket ws, Uri wsUri )
    {
        await ws.ConnectAsync( wsUri, CancellationToken.None );
        using var cts = new CancellationTokenSource( TimeSpan.FromSeconds( 5 ) );
        var received = await ws.ReceiveAsync( new byte[1024], cts.Token );
        received.Count.ShouldBeGreaterThan( 0, "Expected the connectionId acknowledgement from the server." );
    }

    [Test]
    public async Task host_stops_promptly_when_no_client_is_connected_Async()
    {
        // Control case: nothing to drain, so the host must stop immediately. Isolates the open connection as the cause.
        var map = await BuildMapAsync();
        var (app, _) = await StartHostAsync( map );

        var sw = Stopwatch.StartNew();
        await app.StopAsync();
        sw.Stop();
        await app.DisposeAsync();

        TestHelper.Monitor.Info( $"Stop with no client connected took {sw.ElapsedMilliseconds} ms." );
        sw.Elapsed.ShouldBeLessThan( PromptStop );
    }

    [Test]
    public async Task host_stops_promptly_when_a_websocket_client_is_connected_Async()
    {
        var map = await BuildMapAsync();
        var (app, wsUri) = await StartHostAsync( map );

        using var ws = new ClientWebSocket();
        await ConnectAndAwaitAckAsync( ws, wsUri );

        // Stop while connected and collect logs: the bug both hangs for ShutdownTimeout and crashes with an OperationCanceledException.
        long elapsedMs;
        using( var collector = GrandOutput.Default!.CreateMemoryCollector( 256 ) )
        {
            var sw = Stopwatch.StartNew();
            await app.StopAsync();
            elapsedMs = sw.ElapsedMilliseconds;
            await app.DisposeAsync();

            await collector.UpdateCachedEntriesAsync();
            var canceled = collector.CachedEntries
                                    .Where( e => e.Exception?.ExceptionTypeName.Contains( "OperationCanceledException" ) == true )
                                    .ToList();
            canceled.ShouldBeEmpty( "No OperationCanceledException must escape the shutdown path." );
        }

        TestHelper.Monitor.Info( $"Stop with one connected client took {elapsedMs} ms." );
        TimeSpan.FromMilliseconds( elapsedMs ).ShouldBeLessThan( PromptStop,
            "A connected WebSocket client must not delay host shutdown. If this fails near ShutdownTimeout, "
            + "the watcher is not aborting the SimpleR connection on ApplicationStopping." );
    }

    [Test]
    public async Task host_stops_promptly_while_a_client_keeps_reconnecting_Async()
    {
        var map = await BuildMapAsync();
        var (app, wsUri) = await StartHostAsync( map );

        using var ws = new ClientWebSocket();
        await ConnectAndAwaitAckAsync( ws, wsUri );

        // Spam reconnects so an attempt lands in the post-ApplicationStopping window (Kestrel still listening).
        // The fix must refuse these; if any were accepted it would re-arm the full ShutdownTimeout drain.
        using var loopStop = new CancellationTokenSource();
        var reconnectLoop = ReconnectLoopAsync( wsUri, loopStop.Token );

        var sw = Stopwatch.StartNew();
        await app.StopAsync();
        sw.Stop();

        await loopStop.CancelAsync();
        await reconnectLoop;
        await app.DisposeAsync();

        TestHelper.Monitor.Info( $"Stop with a reconnecting client took {sw.ElapsedMilliseconds} ms." );
        sw.Elapsed.ShouldBeLessThan( PromptStop,
            "Connections accepted after ApplicationStopping must not re-arm the drain." );
    }

    static async Task ReconnectLoopAsync( Uri wsUri, CancellationToken stop )
    {
        while( !stop.IsCancellationRequested )
        {
            try
            {
                using var ws = new ClientWebSocket();
                await ws.ConnectAsync( wsUri, stop );
                await ws.ReceiveAsync( new byte[1024], stop );
            }
            catch when( !stop.IsCancellationRequested )
            {
                // Connect/receive fails as the host stops: keep trying until cancelled.
            }
            catch( OperationCanceledException )
            {
                break;
            }
        }
    }
}
