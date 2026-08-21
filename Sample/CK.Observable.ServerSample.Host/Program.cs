using CK.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using CK.AspNet.WebSocketChannel;

var builder = WebApplication.CreateSlimBuilder();
var monitor = builder.GetBuilderMonitor();
builder.UseCKMonitoring();
builder.AddApplicationIdentityServiceConfiguration();

builder.Services.AddCors();
builder.AddWebSocketChannel();

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
app.UseWebSocketChannel();

app.UseSpa( ( b ) =>
{
    if( builder.Environment.IsDevelopment() )
    {
        b.UseProxyToSpaDevelopmentServer( "http://localhost:4200" );
    }
} );

await app.RunAsync().ConfigureAwait( false );

