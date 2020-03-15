#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace CK.Observable
{
    enum SerializationMarker : byte
    {
        Null = 0,
        Reference = 1,
        String,
        Int32,
        Double,
        Char,
        Boolean,
        UInt32,
        Float,
        DateTime,
        Guid,
        TimeSpan,
        DateTimeOffset,
        Type,

        EmptyObject = 251,
        StructBinaryFormatter = 252,
        ObjectBinaryFormatter = 253,
        Struct = 254,
        Object = 255
    }
}
