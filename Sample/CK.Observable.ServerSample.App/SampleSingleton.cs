using CK.BinarySerialization;
using CK.Core;

namespace CK.Observable.ServerSample.App;

[SerializationVersion( 0 )]
public sealed class SampleSingleton : ObservableObject, IObservableDomainSingleton
{
    public float Slider { get; set; }

    internal SampleSingleton()
    {
    }

    private SampleSingleton( IBinaryDeserializer r, ITypeReadInfo info )
        : base( Sliced.Instance )
    {
        Slider = r.ReadValue<float>();
    }

    public static void Write( IBinarySerializer w, in SampleSingleton o )
    {
        w.WriteValue( o.Slider );
    }
}
