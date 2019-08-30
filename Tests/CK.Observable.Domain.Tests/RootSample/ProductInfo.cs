using System.Collections.Generic;

namespace CK.Observable.Domain.Tests.RootSample
{
    [SerializationVersion(0)]
    public struct ProductInfo
    {
        public ProductInfo( string n, int p )
        {
            Name = n;
            Power = p;
            ExtraData = new Dictionary<string, string>();
        }

        public ProductInfo( IBinaryDeserializerContext d )
        {
            var r = d.StartReading();
            Name = r.ReadNullableString();
            Power = r.ReadInt32();
            ExtraData = (IDictionary<string, string>)r.ReadObject();
        }

        void Write( BinarySerializer b )
        {
            b.WriteNullableString( Name );
            b.Write( Power );
            b.WriteObject( ExtraData );
        }

        public string Name { get; }

        public int Power { get; }

        public IDictionary<string,string> ExtraData { get; }
    }
}
