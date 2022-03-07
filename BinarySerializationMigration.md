# Binary Serialization migration

The new CK.BinarySerialization and CK.BinarySerialization.Sliced packages replaces the 
initial serialization Observable serialization framework.

## "Sliced" replaces "RevertSerialization"

The new serialization is based on the same mechanism as the old one that has been renamed.
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
- For a non sealed inherited objects the required deserialization constructor for specialized 
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

The serializer API has changed on 2 different aspects:
- First for basic writes: before the serializer was extending the `ICKBinaryWriter`. 
This is now composed, the basic writer is exposed by the `Writer` property (you can see this above 
on the writing of the `IsRunning` boolean).
- Second for complex object support: nullable reference types and value types are now explicitly 
supported whereas before a single `WriteObject` method handled all kind of objects. The serializer API 
now offers:

```c#
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






