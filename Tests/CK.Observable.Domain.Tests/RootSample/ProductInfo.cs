using CK.Core;
using System.Collections.Generic;

namespace CK.Observable.Domain.Tests.RootSample;

[SerializationVersion(0)]
public readonly struct ProductInfo : BinarySerialization.ICKSlicedSerializable
{
    public ProductInfo( string name, int power )
    {
        Throw.CheckNotNullArgument( name );
        Name = name;
        Power = power;
        ExtraData = new Dictionary<string, string>();
    }

    public ProductInfo( BinarySerialization.IBinaryDeserializer d, BinarySerialization.ITypeReadInfo info )
    {
        Name = d.Reader.ReadString();
        Power = d.Reader.ReadInt32();
        ExtraData = d.ReadObject<IDictionary<string, string>>();
    }

    public static void Write( BinarySerialization.IBinarySerializer s, in ProductInfo o )
    {
        s.Writer.Write( o.Name );
        s.Writer.Write( o.Power );
        s.WriteObject( o.ExtraData );
    }

    public string Name { get; }

    public int Power { get; }

    public IDictionary<string,string> ExtraData { get; }
}
