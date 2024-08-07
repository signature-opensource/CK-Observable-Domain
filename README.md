# CK-Observable-Domain

Observable framework for application states.
Observable now uses the new CK.BinarySerialization library for its serialization. Migrations
steps are detailed here: [BinarySerializationMigration.md](BinarySerializationMigration.md).

## Build status

| Description | Build Status |
|-------------|--------------|
| Last build | [![Build status](https://ci.appveyor.com/api/projects/status/gyaeyym5btkavttn?svg=true)](https://ci.appveyor.com/project/Signature-OpenSource/ck-observable-domain) |
| `master` | [![Build status](https://ci.appveyor.com/api/projects/status/gyaeyym5btkavttn/branch/master?svg=true)](https://ci.appveyor.com/project/Signature-OpenSource/ck-observable-domain/branch/master) |
| `develop` | [![Build status](https://ci.appveyor.com/api/projects/status/gyaeyym5btkavttn/branch/develop?svg=true)](https://ci.appveyor.com/project/Signature-OpenSource/ck-observable-domain/branch/develop) |

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

    public MyApplicationRoot()
    {
      MyStateProperty = "I'm new!";
    }

    MyApplicationRoot( IBinaryDeserializer r, TypeReadInfo? info )
       : base( RevertSerialization.Default )
    {
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
    // Such strongly typed domain can have up to 4 roots.
    using var d = new ObservableDomain<MyApplicationRoot>();

    // When using Modify(), you hold a write lock, and can change properties.
    TransactionResult tr = d.Modify( () =>
    {
        d.Root.MyStateProperty = "Hello world";
    } );
    // At the end of Modify, a Transaction is created, with all the events that happened inside it.
    // In modifier above, the value of property "MyStateProperty" changed: there is an event for it.
    Debug.Assert( tr.Success, "The transaction succeeded." );

    // If you want to read safely from your objects, you can acquire and release a disposable read-only lock.
    using( d.AcquireReadLock() )
    {
        Debug.Assert( d.Root.MyStateProperty == "Hello world" );
    }
}
```

## Clients and persistence

`ObservableDomain` instances are created with an optional `IObservableDomainClient`, which can be chained with other clients using the [Chain-of-responsibility pattern](https://en.wikipedia.org/wiki/Chain-of-responsibility_pattern).

The client processes all events and errors from the `ObservableDomain`, and can alter the `ObservableDomain` during its creation.

This opens up features like `ObservableDomain` persistence and event transmission to potential remote clients.

### File persistence: `FileTransactionProviderClient`

This client loads and saves the `ObservableDomain` from/to a single file.

It also provides an automatic roll-back feature: If an error happens while calling `Modify()`, the entire `ObservableDomain` will be reloaded from the last successful call to `Modify()`.

```csharp
public void Main()
{
    // This file contains or will contain the ObservableDomain objects.
    string path = @"C:\ObservableDomain.bin";
    int fileSaveMs = 5000; // Save file every 5 seconds minimum

    // The fileClient will load the domain, if applicable, and will save the
    // domain after you call Modify(), every 5 seconds.
    var fileClient = new FileTransactionProviderClient( path, fileSaveMs );
    var observableDomain = new ObservableDomain<MyApplicationRoot>( fileClient );

    // If you don't plan on calling Modify() and still want to write
    // the file (eg. on a clean shutdown), you can call Flush() before
    // closing the application.
    fileClient.Flush();
}
```
## PropertyChanged event, PropertyChanged.Fody & Safe events

An `ObservableObject` implements the `System.ComponentModel.INotifyPropertyChanged` that is the standard .Net way to track property changes.
However we use it only because we support (and recommend) the use of PropertyChanged.Fody in any project that implements Observable objects:

```xml
  <ItemGroup>
    <PackageReference Include="Fody" Version="6.6.0" PrivateAssets="all" />
    <PackageReference Include="PropertyChanged.Fody" Version="4.0.0" PrivateAssets="all" />
  </ItemGroup>
```

This Fody weaver automatically calls the OnPropertyChanged method when a property setter is called (you don't have to write this boilerplate code again and again).
From here, the `ObservableObject` implementation takes the control and raises our *safe* event instead of the standard `INotifyPropertyChanged.PropertyChanged` event:
this standard event MUST not be used!

Below is the code of the `ObservableObject` of the 2 property changed events: the exposed, public one that is safe and the "condemned" one:

```csharp
/// <summary>
/// Generic property changed safe event that can be used to track any change on observable properties (by name).
/// This uses the standard <see cref="PropertyChangedEventArgs"/> event.
/// </summary>
public event SafeEventHandler<PropertyChangedEventArgs> PropertyChanged
{
    add
    {
        this.CheckDestroyed();
        _propertyChanged.Add( value );
    }
    remove => _propertyChanged.Remove( value );
}

event PropertyChangedEventHandler? INotifyPropertyChanged.PropertyChanged
{
    add
    {
        throw new NotSupportedException( "INotifyPropertyChanged is supported only because PropertyChanged.Fody requires it. It must not be used." );
    }
    remove
    {
        throw new NotSupportedException( "INotifyPropertyChanged is supported only because PropertyChanged.Fody requires it. It must not be used." );
    }
}
```
Here, to locate the property that has changed, `PropertyChangedEventArgs.PropertyName` must be used which can be boring.
If, for some (important) property, you want the developer to easily track any of its change, you can expose a specific named event. Below is the full
code of a property and its associated safe event (we are using PropertyChanged.Fody, so the property itself is minimalist):

```csharp
[SerializationVersion(0)]
public class Car : ObservableObject
{
    ObservableEventHandler<ObservableDomainEventArgs> _testSpeedChanged;

    public int TestSpeed { get; set; }

    public event SafeEventHandler<ObservableDomainEventArgs> TestSpeedChanged
    {
        add => _testSpeedChanged.Add( value );
        remove => _testSpeedChanged.Remove( value );
    }
```

Defining the event is enough: it will be automatically fired whenever TestSpeed has changed. However, it is important to notice:
- The private field MUST be a `ObservableEventHandler`, a `ObservableEventHandler<EventMonitoredArgs>` or a `ObservableEventHandler<ObservableDomainEventArgs>` exactly named **`_[propName]Changed`**.
- This event is raised before the generic `ObservableObject.PropertyChanged` event.
- Don't forget the serialization support of the event!

```csharp
Car( IBinaryDeserializer d, ITypeReadInfo info )
: base( Sliced.Instance )
{
    Name = d.Reader.ReadString();
    TestSpeed = d.Reader.ReadInt32();
    _position = d.ReadValue<Position>();
    _testSpeedChanged = new ObservableEventHandler<ObservableDomainEventArgs>( d );
}

public static void Write( IBinarySerializer s, in Car o )
{
    s.Writer.Write( o.Name );
    s.Writer.Write( o.TestSpeed );
    s.WriteValue( o._position );
    o._testSpeedChanged.Write( s );
}
```



