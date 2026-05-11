using CK.BinarySerialization;
using CK.Core;

namespace CK.Observable.ServerSample.App;

[SerializationVersion( 0 )]
public sealed class SampleSingleton : ObservableObject, IObservableDomainSingleton
{
    readonly ObservableList<SampleItem> _items;

    public float Slider { get; set; }

    public IObservableReadOnlyList<SampleItem> Items => _items;

    internal SampleSingleton()
    {
        _items = new ObservableList<SampleItem>();
    }

    public void AddItem( string name, int value )
    {
        _items.Add( new SampleItem( name, value ) );
    }

    private SampleSingleton( IBinaryDeserializer r, ITypeReadInfo info )
        : base( Sliced.Instance )
    {
        Slider = r.ReadValue<float>();
        _items = r.ReadObject<ObservableList<SampleItem>>();
    }

    public static void Write( IBinarySerializer w, in SampleSingleton o )
    {
        w.WriteValue( o.Slider );
        w.WriteObject( o._items );
    }
}
