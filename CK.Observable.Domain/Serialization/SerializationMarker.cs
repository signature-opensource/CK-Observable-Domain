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

        Reference = 253,
        EmptyObject = 254,
        Object = 255
    }
}
