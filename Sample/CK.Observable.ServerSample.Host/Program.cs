using CK.Core;
using CK.MQTT.Server;
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

//builder.AddUnsafeAllowAllCors();
builder.Services.AddSignalR();
builder.AddApplicationIdentityServiceConfiguration();

// Bad!
builder.Services.Configure<MQTTDemiServerConfig>( builder.Configuration.GetSection( "MQTTDemiServerConfig" ) );
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

var map = StObjContextRoot.Load( System.Reflection.Assembly.GetExecutingAssembly(), monitor );
var app = builder.CKBuild( map );
app.UseCris();
app.MapHub<ObservableAppHub>( "/hub/league" );


await app.RunAsync().ConfigureAwait( false );

