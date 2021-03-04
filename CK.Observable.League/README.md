# CK.Observable.League

A league handles multiple named `ObservableDomain` by providing:

- Life cycle management (see [DomainLifeCycleOption](Coordinator/DomainLifeCycleOption.cs)).
- A central store for the binary serializations.
- A Coordinator domain that enables to list, create, configure and destroy the domains as a Sql Server master database.
- Concurrent safe accessors and loaders to the actual domains.

## Factory method

Leagues are created by the static factory method `ObservableLeague.LoadAsync`:

```csharp
Task<ObservableLeague?> LoadAsync( IActivityMonitor monitor,
                                   IStreamStore store,
                                   IServiceProvider? serviceProvider = null )
```

The [IStreamStore](StreamStore/IStreamStore.cs) that manages binary streams to load and save serialized domains
has, by default, one simple implementation that is the [DirectoryStreamStore](StreamStore/DirectoryStreamStore.cs).

