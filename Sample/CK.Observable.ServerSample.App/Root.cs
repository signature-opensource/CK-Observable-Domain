using CK.BinarySerialization;
using CK.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CK.Observable.ServerSample.App;

[SerializationVersion( 0 )]
public class Root : ObservableRootObject
{
    public float Slider { get; set; }

    public Root()
    {
    }

    Root( CK.BinarySerialization.IBinaryDeserializer r, ITypeReadInfo info )
        : base( Sliced.Instance )
    {
        Slider = r.ReadValue<float>();
    }

    public static void Write(IBinarySerializer w, in Root o)
    {
        w.WriteValue( o.Slider );
    }

}
