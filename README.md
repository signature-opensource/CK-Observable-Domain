# CK-Observable-Domain

Observable framework for application states.

## Quick start

### Install the `CK.Observable.Domain` NuGet package

```ps1
Install-Package CK.Observable.Domain
```

### Create an `ObservableRootObject` holding your application state

```csharp
[SerializationVersion( 0 )]
public class MyApplicationRoot : ObservableRootObject
{
    public string MyStateProperty { get; set; }

    public MyApplicationRoot( ObservableDomain domain ) : base( domain )
    {
    }

    public MyApplicationRoot( IBinaryDeserializerContext d ) : base( d )
    {
        var r = d.StartReading();
        // Deserialize your properties here. Mind the order!
        MyStateProperty = r.ReadNullableString();
    }

    void Write( BinarySerializer s )
    {
        // Serialize your properties here. Mind the order!
        s.WriteNullableString( MyStateProperty );
    }
}
```

### Create an `ObservableDomain`, and interact with your properties in it

```csharp
public void Main()
{
    // The generic ObservableDomain takes a class type - that is your ObservableRootObject.
    var observableDomain = new ObservableDomain<MyApplicationRoot>();
    // When using Modify(), you hold a write lock, and can change properties.
    observableDomain.Modify( () =>
    {
        observableDomain.Root.MyStateProperty = "Hello world";
    } );
    // At the end of Modify), a Transaction is created, with all the events that happened inside it.
    // In this case, the value of property "MyStateProperty" changed. That is an event.

    // If you want to read safely from your objects, you can acquire and release a disposable read-only lock.
    using( observableDomain.AcquireReadLock() )
    {
        Debug.Assert( observableDomain.Root.MyStateProperty == "Hello world" );
    }
}
```

## Clients and persistence

`ObservableDomain` instances are created with an optional `IObservableDomainClient`, which can be chained with other clients using the [Chain-of-responsibility pattern](https://en.wikipedia.org/wiki/Chain-of-responsibility_pattern).

The client processes all events and errors from the `ObservableDomain`, and can alter the `ObservableDomain` during its creation.

This opens up features like `ObservableDomain` persistence and event transmission to potential remote clients.

### File persistence: `FileTransactionProviderClient`

This client loads and saves the `ObservableDomain` to a single file.

It also provides an automatic roll-back feature: If an error happens while calling `Modify()`, the entire `ObservableDomain` will be reloaded from the last successful call to `Modify()`.

```csharp
public void Main()
{
    string path = @"C:\ObservableDomain.bin"; // This file contains or will contain the ObservableDomain objects
    int fileSaveMs = 5000; // Save file every 5 seconds minimum

    // The fileClient will load the domain, if applicable, and save the domain after you call Modify(), every 5 seconds.
    var fileClient = new FileTransactionProviderClient( path, fileSaveMs );
    var observableDomain = new ObservableDomain<MyApplicationRoot>( fileClient );

    // If you don't plan on calling Modify() and still want to write the file (eg. on a clean shutdown),
    // you can call Flush() before closing the application.
    fileClient.Flush();
}
```
