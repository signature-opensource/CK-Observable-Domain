using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        EmptyObject = 251,
        StructBinaryFormatter = 252,
        ObjectBinaryFormatter = 253,
        Struct = 254,
        Object = 255
    }
}
