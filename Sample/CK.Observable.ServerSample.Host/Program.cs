using CK.Core;
using CK.Observable.League;
using CK.Observable.ServerSample.App;
using CK.Observable.SignalRWatcher;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;

var builder = WebApplication.CreateSlimBuilder();
var monitor = builder.GetBuilderMonitor();
builder.UseCKMonitoring();
builder.AddApplicationIdentityServiceConfiguration();

builder.Services.AddCors();
builder.Services.AddSignalR();

// Bad!
builder.Services.Configure<DefaultObservableLeagueOptions>( c =>
{
    c.StorePath = builder.Configuration["CK-ObservableLeague:StorePath"];
    var domainOpts = new EnsureDomainOptions
    {
        DomainName = "Test-Domain",
        CreateSnapshotSaveDelay = TimeSpan.FromSeconds( 10 ),
        CreateLifeCycleOption = DomainLifeCycleOption.Always,
        RootTypes = { typeof( Root ).AssemblyQualifiedName! }
    };
    c.EnsureDomains.Add( domainOpts );
} );
// /Bad!

// The following line requires having a G0.cs.
// This would be the goal, removing reflection from map loading.
//var map = new CK.StObj.GeneratedRootContext( monitor );
var map = StObjContextRoot.Load( System.Reflection.Assembly.GetExecutingAssembly(), monitor );
var app = builder.CKBuild( map );

app.UseForwardedHeaders();
app.UseCors( c =>
        c.SetIsOriginAllowed( host => true )
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials() );
app.UseRouting();
app.UseCris();
#pragma warning disable ASP0014 // Suggest using top level route registrations
app.UseEndpoints( endpoints =>
{
    endpoints.MapHub<ObservableAppHub>( "/hub/league", ( c ) =>
    {
        c.WebSockets.CloseTimeout = TimeSpan.Zero; // Don't wait for connection close on stop.
    } );
} );
#pragma warning restore ASP0014 // Suggest using top level route registrations

app.UseSpa( ( b ) =>
{
    if( builder.Environment.IsDevelopment() )
    {
        b.UseProxyToSpaDevelopmentServer( "http://localhost:4200" );
    }
} );

await app.RunAsync().ConfigureAwait( false );

