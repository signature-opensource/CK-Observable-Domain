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




