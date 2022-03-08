# Binary Serialization migration

The new CK.BinarySerialization and CK.BinarySerialization.Sliced packages replaces the 
initial serialization Observable serialization framework.

## "Sliced" replaces "RevertSerialization"

The new serialization is based on the same mechanism as the previous one that has been renamed.
- There is no more implicit handling of serialized types: the `ICKSlicedSerializable`
marker interface is require.
- The `SerializationVersion` attribute is in CK.Core namespace.
- For a root object (no base class), there is no more call to `RevertSerialization.OnRootDeserialized`
(this was only implementing a check and has been removed).

**Before:**
```c#
[SerializationVersion( 0 )]
public class K0010Alarm
{
#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
    protected K0010Alarm( RevertSerialization _ )
    {
        RevertSerialization.OnRootDeserialized( this );
    }
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
    // ...
}
``` 
**After:**
```c#
using CK.Core;
using CK.BinarySerialization;

[SerializationVersion( 0 )]
public class K0010Alarm  : ICKSlicedSerializable
{
#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
    protected K0010Alarm( Sliced _ ) { }
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.
    // ...
}
``` 
- For non sealed inherited objects the required deserialization constructor for specialized types
must use the Sliced type.

**Before:**
```c#
    protected Person( RevertSerialization _ ) : base( _ ) { }
``` 
**After:**
```c#
using CK.BinarySerialization;

    protected Person( Sliced _ ) : base( _ ) { }
``` 

## Migration implementation

The serialization version doesn't need to be changed. Until all serialized domains have been 
upgraded, there must be the 2 deserializer constructors (the legacy and the new one) and 
the new `Write` function that is now `public static`.

Declaring the new CK.BinarySerialization namespace can be the first step:
`using CK.BinarySerialization;`

Then the `[SerializationVersion( 42 )]` attribute is now in CK.Core (namespace for the 
moment, the type itself will be in CK.Core assembly in Net6): make sure you are `using CK.Core;`.

It's time to implement the actual migration.

### The Write static method
Sliced serializers call a public static Write method in each type (each "slice") with the 
serializer and the `in` instance to write.

There must be only one Write method: the new one. All Write methods must be rewritten:

**Before:**
```c#
        void Write( BinarySerializer s )
        {
            s.WriteObject( _alarms );
            s.WriteObject( Configuration );
            s.WriteObject( _clock );

            s.Write( IsRunning );
            s.Write( DynamicallyApplyExitConfigurations );
        }
``` 
**After:**
```c#
        public static void Write( IBinarySerializer s, in ObservableK0010 o )
        {
            s.WriteObject( o._alarms );
            s.WriteObject( o.Configuration );
            s.WriteObject( o._clock );

            s.Writer.Write( o.IsRunning );
            s.Writer.Write( o.DynamicallyApplyExitConfigurations );
        }
``` 
> The `in` keyword is NOT an option. It is required and enables value types to be serialized 
> without boxing.

The serializer API has changed on 2 different aspects:
- First for basic writes: before the serializer was extending the ``. 
This is now composed, the basic writer is exposed by the `Writer` property (you can see this above 
on the writing of the `IsRunning` boolean).
- Second for complex object support: nullable reference types and value types are now explicitly 
supported whereas before a single `WriteObject` method handled all kind of objects. The serializer API 
now offers:

```c#

    // Gets the basic writer.
    ICKBinaryWriter Writer { get; }
    
    // Writes a nullable object or value type.
    bool WriteAnyNullable( object? o );

    // Writes a non null object or value type.
    bool WriteAny( object o );

    // Writes a non null object reference.
    bool WriteObject<T>( T o ) where T : class;

    // Writes a nullable object reference.
    bool WriteNullableObject<T>( T? o ) where T : class;

    // Writes a non null value type.
    void WriteValue<T>( in T value ) where T : struct;

    // Writes a nullable value type.
    void WriteNullableValue<T>( in T? value ) where T : struct;
``` 
Notes: 
- The `WriteObject` and `WriteAny` methods return true if the value/object has been written, 
false if it has already been written and only a reference has been written.
- The `WriteAny` is no that useful except in scenario where you actually handle object whose type
is unknown at runtime.

### The deserialization constructors

As long as there are previous versions of serialized domains that exist, the 2 deserialization 
constructors must exist. The **Net6 version will NO MORE handle the old serialization**, so simply 
wait for the Net6 migration to clean up these ones (they won't compile anyway).

#### Legacy constructor
The existing one must be kept as-is, except that:
- the `CK.Observable.IBinaryDeserializer` must use its namespace;
- the call to base must be ` : base( Sliced.Instance )`.

Do not hesitate to add a `// Legacy` comment above:
```c#
    // Legacy
    ObservableK0010( CK.Observable.IBinaryDeserializer r, TypeReadInfo? info )
        : base( Sliced.Instance )
    {
        _alarms = (ObservableChannel<K0010Alarm>)r.ReadObject()!;
        Configuration = (ObservableK0010Configuration)r.ReadObject()!;
        _clock = (SuspendableClock)r.ReadObject()!;

        IsRunning = r.ReadBoolean();
        DynamicallyApplyExitConfigurations = r.ReadBoolean();
    }
```

#### New constructor

The new constructor looks like the legacy one, except that:
- the `CK.BinarySerialization.IBinaryDeserializer` must be used;
- it is a non nullable `ITypeReadInfo` interface instead of the previous `TypeReadInfo` class.

Inside, it's a little bit different because of API changes similar to the serializer ones:
- The deserializer is no more the `ICKBinaryReader`: its `Reader` property exposes it.
- The deserializer `ReadXXX` and `ReadNullableXXX` methods parallel the `Any`, `Object` and `ValueType` 
serializer ones AND are generic methods with the type to read.

```c#
    // New
    ObservableK0010( CK.BinarySerialization.IBinaryDeserializer d, ITypeReadInfo info )
        : base( Sliced.Instance )
    {
        _alarms = d.ReadObject<ObservableChannel<K0010Alarm>>();
        Configuration = d.ReadObject<ObservableK0010Configuration>();
        _clock = d.ReadObject<SuspendableClock>();

        IsRunning = d.Reader.ReadBoolean();
        DynamicallyApplyExitConfigurations = d.Reader.ReadBoolean();
    }
```

Note that thanks to the correct nullable support, there's no '!' after the reads (and if you miss a nullable, you'll 
have the warnings).

For the moment, the type parameter is a simple cast but the plan is to actually use it to support automatic mutations
(for instance a `d.ReadObject<List<Person>>()` will be able to read back a previously saved `HashSet<Person>` automatically).

Please try to follow these conventions:
- Serializer is **s**: `IBinarySerializer s`
- Writer is **w**: `ICKBinaryWriter w`
- Deserializer is **d**: `IBinaryDeserializer d`
- Reader is **r**: `ICKBinaryReader r`

## Expected errors you'll have to deal with

> System.InvalidOperationException : Type 'Artemis.App.CoreService.Root' requires a constructor with (IBinaryDeserializer d, ITypeReadInfo info) parameters.

Check the constructor. Make sure you use `ITypeReadInfo`.
```c#
    Root( CK.BinarySerialization.IBinaryDeserializer r, ITypeReadInfo info )
        : base( Sliced.Instance )
```
