# CK.Observable.League

A league handles multiple named `ObservableDomain` by providing:

- Life cycle management (see [DomainLifeCycleOption](Coordinator/DomainLifeCycleOption.cs)).
- A central store for the binary serializations.
- A Coordinator domain that enables to list, create, configure and destroy the domains (the coordinator a little bit like a Sql Server master database).
- Concurrent loaders and concurrent safe accessors to the actual domains.

## Factory method

Leagues are created by the static factory method `ObservableLeague.LoadAsync`:

```csharp
public static async Task<ObservableLeague?> LoadAsync( IActivityMonitor monitor,
                                                       IStreamStore store,
                                                       IObservableDomainInitializer? initializer = null,
                                                       IServiceProvider? serviceProvider = null )
```

The [IStreamStore](StreamStore/IStreamStore.cs) that manages binary streams to load and save serialized domains
has, by default, one simple implementation that is the [DirectoryStreamStore](StreamStore/DirectoryStreamStore.cs).

The domain initializer is simple:

```csharp
    /// <summary>
    /// Optional long-lived object that a league created by ObservableLeague.LoadAsync
    /// will use to initialize a brand new ObservableDomain.
    /// </summary>
    public interface IObservableDomainInitializer
    {
        /// <summary>
        /// Does whatever is needed to initialize the newly created domain.
        /// The only piece of information available here to bind to/recover/lookup data required
        /// to populate the domain is the ObservableDomain.DomainName and this is intended:
        /// names matter.
        /// </summary>
        /// <param name="monitor">The monitor to use.</param>
        /// <param name="d">The domain to initialize.</param>
        void Initialize( IActivityMonitor monitor, ObservableDomain d );
    }
```

## Creating an ObservableDomain

To create or destroy a domain in a league, its Coordinator must be used.


## Accessing a domain

The league handles explicit load/unload of its domains: the first access
layer is the [IObservableDomainLoader](IObservableDomainLoader.cs) that can be obtained from the league:

```csharp
  /// <summary>
  /// Finds an existing domain.
  /// </summary>
  /// <param name="domainName">The domain name to find.</param>
  /// <returns>The managed domain or null if not found.</returns>
  public IObservableDomainLoader? Find( string domainName ) => _domains.TryGetValue( domainName, out Shell shell ) ? shell : null;
```

This domain loader supports strongly typed `LoadAsync` of domains with up to 4 roots as well as a `DomainChanged` event
and a `GetTransactionEvents` that enables the domain to be watched.

The `LoadAsync` method return a [IObservableDomainShell](IObservableDomainShell.cs) (up to
the [IObservableDomainShell&lt;out T1, out T2, out T3, out T4&gt;](IObservableDomainShellTTTT.cs) with 4 roots)
that will keep the domain loaded in memory until it is disposed.

This shell expose `ModifyAsync`, `ModifyThrowAsync` and `TryModifyAsync` methods to alter a domain
and `TryRead` methods to read it.

