using CK.BinarySerialization;
using CK.Core;

namespace CK.Observable.ServerSample.App;

[SerializationVersion( 0 )]
public sealed class SampleItem : ObservableObject
{
    public string Name { get; set; }
    public int Value { get; set; }

    public SampleItem( string name, int value )
    {
        Name = name;
        Value = value;
    }

    private SampleItem( IBinaryDeserializer r, ITypeReadInfo info )
        : base( Sliced.Instance )
    {
        Name = r.Reader.ReadString();
        Value = r.Reader.ReadInt32();
    }

    public static void Write( IBinarySerializer w, in SampleItem o )
    {
        w.Writer.Write( o.Name );
        w.Writer.Write( o.Value );
    }
}
