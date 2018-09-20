using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable
{
    enum SerializationMarker : byte
    {
        Null,
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

        Reference = 250,
        EmptyObject = 251,
        StructBinaryFormatter = 252,
        ObjectBinaryFormatter = 253,
        Struct = 254,
        Object = 255
    }
}
