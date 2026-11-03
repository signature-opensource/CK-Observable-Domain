# CK.Observable.ServerSample.Host

A sample application that shows an `ObservableDomain` of a server in an Angular application: the
browser listens to the domain over a WebSocket and modifies it with Cris commands.

## The projects

| Project | Role |
|---|---|
| `CK.Observable.ServerSample.App` | The application: the `SampleDriver` (a transient domain driver of the "Test-Domain" domain), its `SampleSingleton`, and the Cris commands `ISliderCommand` and `IMultiEventCommand`. |
| `CK.Observable.ServerSample.Builder` | Generates the code of the host (`$StObjGen`) and the `ck-gen` of the client application, from its `CKSetup.xml`. |
| `CK.Observable.ServerSample.Host` | The host: a party with the "HttpEndpoint" feature, bootstrapped by CK.AppHost (its `Program.cs` is `await CK.Core.CKomposableAppHost.RunAsync( args );`). |

The client application is the Angular project `Clients/ObservableDomainNgSample` (the `ObservableDomainNgSample`
`<TypeScript>` configuration of the builder, `Kind="DefaultApp"`): the host serves it at `/`.

## The configuration

The `appsettings.json` of the host configures its party, `CK-Observable/$ServerSample`, and its HTTP endpoint:

```jsonc
"CK-AppIdentity": {
  "FullName": "CK-Observable/$ServerSample",
  "Local": {
    "AllowFeatures": "HttpEndpoint",
    "HttpEndpoint": { "Main": { "Urls": "http://localhost:5600" } }
  }
}
```

The packages of the App configure the endpoint: Cris (`/.cris`), the WebSocket channel (`/ws`) and the client
application (CK.TS.AspNet.Clients).

## Running it

Build the solution, then run `#Dev/Start.ps1` at the root of the repository (or start the host in its folder with
`DOTNET_ENVIRONMENT=Development`) and open `http://localhost:5600/`.

In Development, the host starts the dev server of the client application (`ng serve` on port 4600) and forwards
to it what it doesn't handle: the host stays the origin. Otherwise, it serves the built files
(`Clients/ObservableDomainNgSample/dist/ObservableDomainNgSample/browser`).
